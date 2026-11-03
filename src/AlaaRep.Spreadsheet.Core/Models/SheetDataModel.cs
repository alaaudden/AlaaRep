using System;
using System.Collections.Generic;

namespace AlaaRep.Spreadsheet.Core.Models
{
    /// <summary>
    /// Sparse data model for a worksheet.
    /// Uses row-level dictionaries so that empty rows and empty cells occupy 0 memory.
    /// Can seamlessly scale to 1,000,000+ rows and 16,384+ columns.
    /// </summary>
    public class SheetDataModel
    {
        // row -> (col -> CellData)
        private readonly Dictionary<int, Dictionary<int, CellData>> _rows = new();

        // Custom row heights and column widths
        private readonly Dictionary<int, double> _customRowHeights = new();
        private readonly Dictionary<int, double> _customColWidths = new();

        // Custom column header titles (col -> title)
        private readonly Dictionary<int, string> _customColumnHeaders = new();

        public string Name { get; set; } = "Sheet1";

        public double DefaultRowHeight { get; set; } = 24.0;
        public double DefaultColumnWidth { get; set; } = 80.0;

        // Virtual bounds: Excel defaults to 1,048,576 rows and 16,384 columns
        public int TotalRowCount { get; set; } = 1_000_000;
        public int TotalColumnCount { get; set; } = 16_384;

        // Cached occupied bounds
        public int MaxOccupiedRow { get; private set; } = -1;
        public int MaxOccupiedColumn { get; private set; } = -1;

        public event EventHandler? DataChanged;
        public event EventHandler? LayoutChanged;

        public SheetDataModel(string name = "Sheet1")
        {
            Name = name;
        }

        public double GetRowHeight(int row)
        {
            return _customRowHeights.TryGetValue(row, out double h) ? h : DefaultRowHeight;
        }

        public void SetRowHeight(int row, double height)
        {
            if (height < 10) height = 10;
            _customRowHeights[row] = height;
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        public double GetColumnWidth(int col)
        {
            return _customColWidths.TryGetValue(col, out double w) ? w : DefaultColumnWidth;
        }

        public void SetColumnWidth(int col, double width)
        {
            if (width < 15) width = 15;
            _customColWidths[col] = width;
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        public string GetColumnHeader(int col)
        {
            if (_customColumnHeaders.TryGetValue(col, out string? custom) && !string.IsNullOrWhiteSpace(custom))
            {
                return custom;
            }
            return CellCoordinate.ColumnIndexToLetters(col);
        }

        public void SetColumnHeader(int col, string? title)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Trim() == CellCoordinate.ColumnIndexToLetters(col))
            {
                _customColumnHeaders.Remove(col);
            }
            else
            {
                _customColumnHeaders[col] = title.Trim();
            }
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        public CellData? GetCell(int row, int col)
        {
            if (_rows.TryGetValue(row, out var rowDict) && rowDict.TryGetValue(col, out var cell))
            {
                return cell;
            }
            return null;
        }

        public CellData GetOrCreateCell(int row, int col)
        {
            if (!_rows.TryGetValue(row, out var rowDict))
            {
                rowDict = new Dictionary<int, CellData>();
                _rows[row] = rowDict;
            }

            if (!rowDict.TryGetValue(col, out var cell))
            {
                cell = new CellData();
                rowDict[col] = cell;
                UpdateBounds(row, col);
            }

            return cell;
        }

        public void SetCellValue(int row, int col, object? value, bool notify = true)
        {
            if (value == null || (value is string s && string.IsNullOrEmpty(s)))
            {
                // Clear cell value if format is default, otherwise just clear value
                if (_rows.TryGetValue(row, out var rowDict) && rowDict.TryGetValue(col, out var existing))
                {
                    if (existing.Format == CellFormat.Default)
                    {
                        rowDict.Remove(col);
                        if (rowDict.Count == 0) _rows.Remove(row);
                    }
                    else
                    {
                        existing.Value = null;
                    }
                }
            }
            else
            {
                var cell = GetOrCreateCell(row, col);
                cell.Value = value;
                UpdateBounds(row, col);
            }

            if (notify)
            {
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public object? GetCellValue(int row, int col)
        {
            return GetCell(row, col)?.Value;
        }

        public CellFormat GetCellFormat(int row, int col)
        {
            return GetCell(row, col)?.Format ?? CellFormat.Default;
        }

        public void SetCellFormat(int row, int col, CellFormat format, bool notify = true)
        {
            if (format == CellFormat.Default && GetCellValue(row, col) == null)
            {
                if (_rows.TryGetValue(row, out var rowDict))
                {
                    rowDict.Remove(col);
                    if (rowDict.Count == 0) _rows.Remove(row);
                }
            }
            else
            {
                var cell = GetOrCreateCell(row, col);
                cell.Format = format;
            }

            if (notify)
            {
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ApplyFormatToRange(CellRange range, Func<CellFormat, CellFormat> transform)
        {
            for (int r = range.Top; r <= range.Bottom; r++)
            {
                for (int c = range.Left; c <= range.Right; c++)
                {
                    var currentFormat = GetCellFormat(r, c);
                    var newFormat = transform(currentFormat);
                    SetCellFormat(r, c, newFormat, notify: false);
                }
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearRange(CellRange range, bool clearValues = true, bool clearFormats = true)
        {
            for (int r = range.Top; r <= range.Bottom; r++)
            {
                if (!_rows.TryGetValue(r, out var rowDict)) continue;

                for (int c = range.Left; c <= range.Right; c++)
                {
                    if (rowDict.TryGetValue(c, out var cell))
                    {
                        if (clearValues && clearFormats)
                        {
                            rowDict.Remove(c);
                        }
                        else if (clearValues)
                        {
                            cell.Value = null;
                        }
                        else if (clearFormats)
                        {
                            cell.Format = CellFormat.Default;
                        }
                    }
                }

                if (rowDict.Count == 0)
                {
                    _rows.Remove(r);
                }
            }
            RecalculateBounds();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void BatchLoad(IEnumerable<(int Row, int Col, object? Value, CellFormat? Format)> items)
        {
            foreach (var item in items)
            {
                if (item.Value != null || item.Format != null)
                {
                    var cell = GetOrCreateCell(item.Row, item.Col);
                    cell.Value = item.Value;
                    if (item.Format != null)
                    {
                        cell.Format = item.Format;
                    }
                    UpdateBounds(item.Row, item.Col);
                }
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearAll()
        {
            _rows.Clear();
            _customRowHeights.Clear();
            _customColWidths.Clear();
            _customColumnHeaders.Clear();
            MaxOccupiedRow = -1;
            MaxOccupiedColumn = -1;
            DataChanged?.Invoke(this, EventArgs.Empty);
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateBounds(int row, int col)
        {
            if (row > MaxOccupiedRow) MaxOccupiedRow = row;
            if (col > MaxOccupiedColumn) MaxOccupiedColumn = col;
        }

        public void RecalculateBounds()
        {
            int maxR = -1;
            int maxC = -1;

            foreach (var kvpRow in _rows)
            {
                if (kvpRow.Value.Count > 0)
                {
                    if (kvpRow.Key > maxR) maxR = kvpRow.Key;
                    foreach (var kvpCol in kvpRow.Value)
                    {
                        if (kvpCol.Key > maxC) maxC = kvpCol.Key;
                    }
                }
            }

            MaxOccupiedRow = maxR;
            MaxOccupiedColumn = maxC;
        }

        public IEnumerable<KeyValuePair<CellCoordinate, CellData>> GetAllOccupiedCells()
        {
            foreach (var rowKvp in _rows)
            {
                foreach (var colKvp in rowKvp.Value)
                {
                    yield return new KeyValuePair<CellCoordinate, CellData>(
                        new CellCoordinate(rowKvp.Key, colKvp.Key),
                        colKvp.Value
                    );
                }
            }
        }
    }
}
