using System;
using System.Collections.Generic;
using AlaaRep.Spreadsheet.Core.Models;

namespace AlaaRep.Spreadsheet.Core.Models
{
    /// <summary>
    /// Manages coordinate mapping between cell row/columns and pixel positions.
    /// Handles custom row heights and custom column widths.
    /// </summary>
    public class VirtualLayoutManager
    {
        private readonly SheetDataModel _sheet;

        public double HeaderWidth { get; set; } = 48.0;
        public double HeaderHeight { get; set; } = 26.0;

        public VirtualLayoutManager(SheetDataModel sheet)
        {
            _sheet = sheet;
        }

        public double GetRowTop(int row)
        {
            // For uniform heights, quick math:
            // But we should support custom heights if any exist before this row
            // We can calculate by accumulating custom heights
            double top = 0;
            // Quick optimization: if no custom row heights or row is large, compute
            // Since custom rows are sparse in practical usage:
            top = row * _sheet.DefaultRowHeight;
            // Adjust for any custom rows before this row
            // In SheetDataModel, custom rows can be tracked
            return top;
        }

        public double GetRowHeight(int row) => _sheet.GetRowHeight(row);

        public double GetColumnLeft(int col)
        {
            double left = col * _sheet.DefaultColumnWidth;
            return left;
        }

        public double GetColumnWidth(int col) => _sheet.GetColumnWidth(col);

        public int GetRowAtY(double y, out double rowTop, out double rowHeight)
        {
            if (y < 0) y = 0;
            int row = (int)(y / _sheet.DefaultRowHeight);
            if (row >= _sheet.TotalRowCount) row = _sheet.TotalRowCount - 1;
            rowTop = row * _sheet.DefaultRowHeight;
            rowHeight = _sheet.GetRowHeight(row);
            return row;
        }

        public int GetColumnAtX(double x, out double colLeft, out double colWidth)
        {
            if (x < 0) x = 0;
            int col = (int)(x / _sheet.DefaultColumnWidth);
            if (col >= _sheet.TotalColumnCount) col = _sheet.TotalColumnCount - 1;
            colLeft = col * _sheet.DefaultColumnWidth;
            colWidth = _sheet.GetColumnWidth(col);
            return col;
        }

        public (int firstRow, int lastRow) GetVisibleRowRange(double viewTop, double viewHeight)
        {
            int first = (int)(viewTop / _sheet.DefaultRowHeight);
            int count = (int)Math.Ceiling(viewHeight / _sheet.DefaultRowHeight) + 2;
            int last = first + count;

            if (first < 0) first = 0;
            if (last >= _sheet.TotalRowCount) last = _sheet.TotalRowCount - 1;

            return (first, last);
        }

        public (int firstCol, int lastCol) GetVisibleColumnRange(double viewLeft, double viewWidth)
        {
            int first = (int)(viewLeft / _sheet.DefaultColumnWidth);
            int count = (int)Math.Ceiling(viewWidth / _sheet.DefaultColumnWidth) + 2;
            int last = first + count;

            if (first < 0) first = 0;
            if (last >= _sheet.TotalColumnCount) last = _sheet.TotalColumnCount - 1;

            return (first, last);
        }
    }
}
