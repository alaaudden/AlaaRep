using System;
using System.Collections.Generic;
using AlaaRep.Spreadsheet.Core.Models;

namespace AlaaRep.Spreadsheet.Core.Models
{
    public class SelectionModel
    {
        public CellCoordinate ActiveCell { get; private set; } = new CellCoordinate(0, 0);
        public CellRange SelectionRange { get; private set; } = new CellRange(0, 0, 0, 0);

        public event EventHandler? SelectionChanged;

        public void SelectCell(int row, int col)
        {
            ActiveCell = new CellCoordinate(row, col);
            SelectionRange = new CellRange(row, col, row, col);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ExtendSelection(int row, int col)
        {
            SelectionRange = new CellRange(ActiveCell.Row, ActiveCell.Column, row, col);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectRow(int row, int maxCol)
        {
            ActiveCell = new CellCoordinate(row, 0);
            SelectionRange = new CellRange(row, 0, row, maxCol);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectColumn(int col, int maxRow)
        {
            ActiveCell = new CellCoordinate(0, col);
            SelectionRange = new CellRange(0, col, maxRow, col);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectAll(int maxRow, int maxCol)
        {
            ActiveCell = new CellCoordinate(0, 0);
            SelectionRange = new CellRange(0, 0, maxRow, maxCol);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool IsSelected(int row, int col)
        {
            return SelectionRange.Contains(row, col);
        }
    }
}
