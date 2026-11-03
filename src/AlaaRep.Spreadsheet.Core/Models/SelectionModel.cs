using System;
using AlaaRep.Spreadsheet.Core.Models;

namespace AlaaRep.Spreadsheet.Core.Models
{
    public enum SelectionModeType
    {
        Cells,
        Rows,
        Columns
    }

    public class SelectionModel
    {
        public CellCoordinate ActiveCell { get; private set; } = new CellCoordinate(0, 0);
        public CellRange SelectionRange { get; private set; } = new CellRange(0, 0, 0, 0);
        public SelectionModeType Mode { get; private set; } = SelectionModeType.Cells;

        public int AnchorRow { get; private set; } = 0;
        public int AnchorColumn { get; private set; } = 0;

        public event EventHandler? SelectionChanged;

        public void SelectCell(int row, int col)
        {
            Mode = SelectionModeType.Cells;
            AnchorRow = row;
            AnchorColumn = col;
            ActiveCell = new CellCoordinate(row, col);
            SelectionRange = new CellRange(row, col, row, col);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ExtendSelection(int row, int col)
        {
            Mode = SelectionModeType.Cells;
            SelectionRange = new CellRange(AnchorRow, AnchorColumn, row, col);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectRow(int row, int maxCol)
        {
            Mode = SelectionModeType.Rows;
            AnchorRow = row;
            AnchorColumn = 0;
            ActiveCell = new CellCoordinate(row, 0);
            SelectionRange = new CellRange(row, 0, row, maxCol);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ExtendRowSelection(int targetRow, int maxCol)
        {
            Mode = SelectionModeType.Rows;
            int start = Math.Min(AnchorRow, targetRow);
            int end = Math.Max(AnchorRow, targetRow);
            SelectionRange = new CellRange(start, 0, end, maxCol);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectColumn(int col, int maxRow)
        {
            Mode = SelectionModeType.Columns;
            AnchorRow = 0;
            AnchorColumn = col;
            ActiveCell = new CellCoordinate(0, col);
            SelectionRange = new CellRange(0, col, maxRow, col);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ExtendColumnSelection(int targetCol, int maxRow)
        {
            Mode = SelectionModeType.Columns;
            int start = Math.Min(AnchorColumn, targetCol);
            int end = Math.Max(AnchorColumn, targetCol);
            SelectionRange = new CellRange(0, start, maxRow, end);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectAll(int maxRow, int maxCol)
        {
            Mode = SelectionModeType.Cells;
            AnchorRow = 0;
            AnchorColumn = 0;
            ActiveCell = new CellCoordinate(0, 0);
            SelectionRange = new CellRange(0, 0, maxRow, maxCol);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool IsSelected(int row, int col)
        {
            return SelectionRange.Contains(row, col);
        }

        public bool IsRowSelected(int row)
        {
            return Mode == SelectionModeType.Rows && row >= SelectionRange.Top && row <= SelectionRange.Bottom;
        }

        public bool IsColumnSelected(int col)
        {
            return Mode == SelectionModeType.Columns && col >= SelectionRange.Left && col <= SelectionRange.Right;
        }
    }
}
