using System;
using AlaaRep.Spreadsheet.Core.Models;

namespace AlaaRep.Spreadsheet.Core.Models
{
    /// <summary>
    /// Manages coordinate mapping between cell row/columns and pixel positions.
    /// Supports dynamic and custom row heights, column widths, and zoom scaling.
    /// </summary>
    public class VirtualLayoutManager
    {
        private readonly SheetDataModel _sheet;

        public double HeaderWidth { get; set; } = 48.0;
        public double HeaderHeight { get; set; } = 26.0;

        public double ZoomFactor { get; set; } = 1.0;

        public VirtualLayoutManager(SheetDataModel sheet)
        {
            _sheet = sheet;
        }

        public double GetRowHeight(int row) => _sheet.GetRowHeight(row) * ZoomFactor;
        public double GetColumnWidth(int col) => _sheet.GetColumnWidth(col) * ZoomFactor;

        public double ScaledHeaderWidth => HeaderWidth;
        public double ScaledHeaderHeight => HeaderHeight;

        /// <summary>
        /// Calculates the screen X position (relative to header edge) for a given column
        /// considering all zoomed column widths starting from scrollCol.
        /// </summary>
        public double GetColumnOffsetFromScroll(int scrollCol, int targetCol)
        {
            if (targetCol == scrollCol) return 0;
            if (targetCol < scrollCol)
            {
                double off = 0;
                for (int c = targetCol; c < scrollCol; c++)
                {
                    off -= GetColumnWidth(c);
                }
                return off;
            }
            else
            {
                double off = 0;
                for (int c = scrollCol; c < targetCol; c++)
                {
                    off += GetColumnWidth(c);
                }
                return off;
            }
        }

        /// <summary>
        /// Calculates the screen Y position (relative to header edge) for a given row
        /// considering all zoomed row heights starting from scrollRow.
        /// </summary>
        public double GetRowOffsetFromScroll(int scrollRow, int targetRow)
        {
            if (targetRow == scrollRow) return 0;
            if (targetRow < scrollRow)
            {
                double off = 0;
                for (int r = targetRow; r < scrollRow; r++)
                {
                    off -= GetRowHeight(r);
                }
                return off;
            }
            else
            {
                double off = 0;
                for (int r = scrollRow; r < targetRow; r++)
                {
                    off += GetRowHeight(r);
                }
                return off;
            }
        }

        /// <summary>
        /// Finds the row at screen Y coordinate relative to the header edge.
        /// </summary>
        public int GetRowAtY(int scrollRow, double yOffset, out double rowY, out double rowH)
        {
            if (yOffset <= 0)
            {
                rowY = 0;
                rowH = GetRowHeight(scrollRow);
                return scrollRow;
            }

            double currentY = 0;
            int r = scrollRow;
            while (r < _sheet.TotalRowCount)
            {
                double h = GetRowHeight(r);
                if (currentY + h > yOffset)
                {
                    rowY = currentY;
                    rowH = h;
                    return r;
                }
                currentY += h;
                r++;
            }

            rowY = currentY;
            rowH = _sheet.DefaultRowHeight * ZoomFactor;
            return _sheet.TotalRowCount - 1;
        }

        /// <summary>
        /// Finds the column at screen X coordinate relative to the header edge.
        /// </summary>
        public int GetColumnAtX(int scrollCol, double xOffset, out double colX, out double colW)
        {
            if (xOffset <= 0)
            {
                colX = 0;
                colW = GetColumnWidth(scrollCol);
                return scrollCol;
            }

            double currentX = 0;
            int c = scrollCol;
            while (c < _sheet.TotalColumnCount)
            {
                double w = GetColumnWidth(c);
                if (currentX + w > xOffset)
                {
                    colX = currentX;
                    colW = w;
                    return c;
                }
                currentX += w;
                c++;
            }

            colX = currentX;
            colW = _sheet.DefaultColumnWidth * ZoomFactor;
            return _sheet.TotalColumnCount - 1;
        }
    }
}
