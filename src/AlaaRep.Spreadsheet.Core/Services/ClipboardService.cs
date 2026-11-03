using System;
using System.IO;
using System.Text;
using AlaaRep.Spreadsheet.Core.Models;

namespace AlaaRep.Spreadsheet.Core.Services
{
    public class ClipboardService
    {
        /// <summary>
        /// Converts the selected range of cells into tab-delimited text (standard clipboard format for Excel).
        /// </summary>
        public string CopyRangeToText(SheetDataModel sheet, CellRange range)
        {
            var sb = new StringBuilder();

            for (int r = range.Top; r <= range.Bottom; r++)
            {
                for (int c = range.Left; c <= range.Right; c++)
                {
                    var cell = sheet.GetCell(r, c);
                    string text = cell?.GetFormattedText() ?? string.Empty;
                    // Escape tabs or newlines if any
                    if (text.Contains("\t") || text.Contains("\n") || text.Contains("\""))
                    {
                        text = "\"" + text.Replace("\"", "\"\"") + "\"";
                    }
                    sb.Append(text);
                    if (c < range.Right)
                    {
                        sb.Append('\t');
                    }
                }
                if (r < range.Bottom)
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Pastes tab-delimited text starting at the specified target cell.
        /// </summary>
        public CellRange PasteTextToSheet(SheetDataModel sheet, CellCoordinate targetCell, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new CellRange(targetCell);
            }

            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            // If last line is empty (common in clipboard), remove it
            int lineCount = lines.Length;
            if (lineCount > 1 && string.IsNullOrEmpty(lines[lineCount - 1]))
            {
                lineCount--;
            }

            int endRow = targetCell.Row;
            int endCol = targetCell.Column;

            for (int i = 0; i < lineCount; i++)
            {
                int r = targetCell.Row + i;
                if (r > endRow) endRow = r;

                string[] cells = lines[i].Split('\t');
                for (int j = 0; j < cells.Length; j++)
                {
                    int c = targetCell.Column + j;
                    if (c > endCol) endCol = c;

                    string cellText = cells[j].Trim();
                    // Strip surrounding quotes if present
                    if (cellText.StartsWith("\"") && cellText.EndsWith("\"") && cellText.Length >= 2)
                    {
                        cellText = cellText.Substring(1, cellText.Length - 2).Replace("\"\"", "\"");
                    }

                    if (double.TryParse(cellText, out double dVal))
                    {
                        sheet.SetCellValue(r, c, dVal, notify: false);
                    }
                    else
                    {
                        sheet.SetCellValue(r, c, string.IsNullOrEmpty(cellText) ? null : cellText, notify: false);
                    }
                }
            }

            sheet.RecalculateBounds();
            // Fire data changed once
            sheet.SetCellValue(targetCell.Row, targetCell.Column, sheet.GetCellValue(targetCell.Row, targetCell.Column), notify: true);

            return new CellRange(targetCell.Row, targetCell.Column, endRow, endCol);
        }
    }
}
