using System;
using System.Collections.Generic;
using AlaaRep.Spreadsheet.Core.Models;

namespace AlaaRep.Spreadsheet.Core.Services
{
    public class SetCellValueCommand : IUndoCommand
    {
        private readonly SheetDataModel _sheet;
        private readonly int _row;
        private readonly int _col;
        private readonly object? _oldValue;
        private readonly object? _newValue;

        public string Description => $"Set cell {CellCoordinate.Format(_row, _col)} value";

        public SetCellValueCommand(SheetDataModel sheet, int row, int col, object? oldValue, object? newValue)
        {
            _sheet = sheet;
            _row = row;
            _col = col;
            _oldValue = oldValue;
            _newValue = newValue;
        }

        public void Execute()
        {
            _sheet.SetCellValue(_row, _col, _newValue);
        }

        public void Undo()
        {
            _sheet.SetCellValue(_row, _col, _oldValue);
        }
    }

    public class SetColumnHeaderCommand : IUndoCommand
    {
        private readonly SheetDataModel _sheet;
        private readonly int _col;
        private readonly string _oldTitle;
        private readonly string _newTitle;

        public string Description => $"Set column {_col} title";

        public SetColumnHeaderCommand(SheetDataModel sheet, int col, string oldTitle, string newTitle)
        {
            _sheet = sheet;
            _col = col;
            _oldTitle = oldTitle;
            _newTitle = newTitle;
        }

        public void Execute()
        {
            _sheet.SetColumnHeader(_col, _newTitle);
        }

        public void Undo()
        {
            _sheet.SetColumnHeader(_col, _oldTitle);
        }
    }

    public class RangeFormatCommand : IUndoCommand
    {
        private readonly SheetDataModel _sheet;
        private readonly CellRange _range;
        private readonly Dictionary<CellCoordinate, CellFormat> _oldFormats = new();
        private readonly Func<CellFormat, CellFormat> _transform;

        public string Description => $"Format range {_range}";

        public RangeFormatCommand(SheetDataModel sheet, CellRange range, Func<CellFormat, CellFormat> transform)
        {
            _sheet = sheet;
            _range = range;
            _transform = transform;

            for (int r = range.Top; r <= range.Bottom; r++)
            {
                for (int c = range.Left; c <= range.Right; c++)
                {
                    _oldFormats[new CellCoordinate(r, c)] = sheet.GetCellFormat(r, c);
                }
            }
        }

        public void Execute()
        {
            _sheet.ApplyFormatToRange(_range, _transform);
        }

        public void Undo()
        {
            foreach (var kvp in _oldFormats)
            {
                _sheet.SetCellFormat(kvp.Key.Row, kvp.Key.Column, kvp.Value, notify: false);
            }
            _sheet.SetCellValue(_range.Top, _range.Left, _sheet.GetCellValue(_range.Top, _range.Left), notify: true);
        }
    }

    public class ClearRangeCommand : IUndoCommand
    {
        private readonly SheetDataModel _sheet;
        private readonly CellRange _range;
        private readonly Dictionary<CellCoordinate, (object? Value, CellFormat Format)> _savedCells = new();
        private readonly bool _clearValues;
        private readonly bool _clearFormats;

        public string Description => $"Clear range {_range}";

        public ClearRangeCommand(SheetDataModel sheet, CellRange range, bool clearValues, bool clearFormats)
        {
            _sheet = sheet;
            _range = range;
            _clearValues = clearValues;
            _clearFormats = clearFormats;

            for (int r = range.Top; r <= range.Bottom; r++)
            {
                for (int c = range.Left; c <= range.Right; c++)
                {
                    var cell = sheet.GetCell(r, c);
                    if (cell != null)
                    {
                        _savedCells[new CellCoordinate(r, c)] = (cell.Value, cell.Format);
                    }
                }
            }
        }

        public void Execute()
        {
            _sheet.ClearRange(_range, _clearValues, _clearFormats);
        }

        public void Undo()
        {
            foreach (var kvp in _savedCells)
            {
                var cell = _sheet.GetOrCreateCell(kvp.Key.Row, kvp.Key.Column);
                cell.Value = kvp.Value.Value;
                cell.Format = kvp.Value.Format;
            }
            _sheet.RecalculateBounds();
            _sheet.SetCellValue(_range.Top, _range.Left, _sheet.GetCellValue(_range.Top, _range.Left), notify: true);
        }
    }

    public class InsertRowCommand : IUndoCommand
    {
        private readonly SheetDataModel _sheet;
        private readonly int _row;

        public string Description => $"Insert row at {_row + 1}";

        public InsertRowCommand(SheetDataModel sheet, int row)
        {
            _sheet = sheet;
            _row = row;
        }

        public void Execute()
        {
            _sheet.InsertRow(_row);
        }

        public void Undo()
        {
            _sheet.DeleteRow(_row);
        }
    }

    public class DeleteRowCommand : IUndoCommand
    {
        private readonly SheetDataModel _sheet;
        private readonly int _row;
        private readonly Dictionary<int, CellData> _deletedRow = new();
        private readonly double _deletedHeight;

        public string Description => $"Delete row {_row + 1}";

        public DeleteRowCommand(SheetDataModel sheet, int row)
        {
            _sheet = sheet;
            _row = row;
            _deletedHeight = sheet.GetRowHeight(row);
            for (int c = 0; c <= Math.Max(sheet.MaxOccupiedColumn, 0); c++)
            {
                var cell = sheet.GetCell(row, c);
                if (cell != null)
                {
                    _deletedRow[c] = cell.Clone();
                }
            }
        }

        public void Execute()
        {
            _sheet.DeleteRow(_row);
        }

        public void Undo()
        {
            _sheet.InsertRow(_row);
            _sheet.SetRowHeight(_row, _deletedHeight);
            foreach (var kvp in _deletedRow)
            {
                var cell = _sheet.GetOrCreateCell(_row, kvp.Key);
                cell.Value = kvp.Value.Value;
                cell.Format = kvp.Value.Format;
            }
            _sheet.RecalculateBounds();
            _sheet.SetCellValue(_row, 0, _sheet.GetCellValue(_row, 0), notify: true);
        }
    }

    public class InsertColumnCommand : IUndoCommand
    {
        private readonly SheetDataModel _sheet;
        private readonly int _col;

        public string Description => $"Insert column at {CellCoordinate.ColumnIndexToLetters(_col)}";

        public InsertColumnCommand(SheetDataModel sheet, int col)
        {
            _sheet = sheet;
            _col = col;
        }

        public void Execute()
        {
            _sheet.InsertColumn(_col);
        }

        public void Undo()
        {
            _sheet.DeleteColumn(_col);
        }
    }

    public class DeleteColumnCommand : IUndoCommand
    {
        private readonly SheetDataModel _sheet;
        private readonly int _col;
        private readonly Dictionary<int, CellData> _deletedCol = new();
        private readonly double _deletedWidth;
        private readonly string _deletedHeader;

        public string Description => $"Delete column {CellCoordinate.ColumnIndexToLetters(_col)}";

        public DeleteColumnCommand(SheetDataModel sheet, int col)
        {
            _sheet = sheet;
            _col = col;
            _deletedWidth = sheet.GetColumnWidth(col);
            _deletedHeader = sheet.GetColumnHeader(col);
            for (int r = 0; r <= Math.Max(sheet.MaxOccupiedRow, 0); r++)
            {
                var cell = sheet.GetCell(r, col);
                if (cell != null)
                {
                    _deletedCol[r] = cell.Clone();
                }
            }
        }

        public void Execute()
        {
            _sheet.DeleteColumn(_col);
        }

        public void Undo()
        {
            _sheet.InsertColumn(_col);
            _sheet.SetColumnWidth(_col, _deletedWidth);
            _sheet.SetColumnHeader(_col, _deletedHeader);
            foreach (var kvp in _deletedCol)
            {
                var cell = _sheet.GetOrCreateCell(kvp.Key, _col);
                cell.Value = kvp.Value.Value;
                cell.Format = kvp.Value.Format;
            }
            _sheet.RecalculateBounds();
            _sheet.SetCellValue(0, _col, _sheet.GetCellValue(0, _col), notify: true);
        }
    }

    public class PasteRangeCommand : IUndoCommand
    {
        private readonly SheetDataModel _sheet;
        private readonly CellCoordinate _target;
        private readonly string _text;
        private readonly Dictionary<CellCoordinate, (object? Value, CellFormat Format)> _savedPrevious = new();
        private CellRange _pastedRange;

        public string Description => $"Paste to {_target}";

        public PasteRangeCommand(SheetDataModel sheet, CellCoordinate target, string text)
        {
            _sheet = sheet;
            _target = target;
            _text = text;
        }

        public void Execute()
        {
            var clipboard = new ClipboardService();
            // Estimate pasted bounds to save previous state
            _pastedRange = clipboard.PasteTextToSheet(_sheet, _target, _text);
        }

        public void Undo()
        {
            _sheet.ClearRange(_pastedRange, clearValues: true, clearFormats: false);
            foreach (var kvp in _savedPrevious)
            {
                _sheet.SetCellValue(kvp.Key.Row, kvp.Key.Column, kvp.Value.Value, notify: false);
                _sheet.SetCellFormat(kvp.Key.Row, kvp.Key.Column, kvp.Value.Format, notify: false);
            }
            _sheet.RecalculateBounds();
            _sheet.SetCellValue(_target.Row, _target.Column, _sheet.GetCellValue(_target.Row, _target.Column), notify: true);
        }
    }
}
