using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExcelDataReader;
using AppSpreadsheetDoc = AlaaRep.Spreadsheet.Core.Models.SpreadsheetDocument;
using AppCellFormat = AlaaRep.Spreadsheet.Core.Models.CellFormat;
using OpenXmlSpreadsheetDoc = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument;
using OpenXmlCellFormat = DocumentFormat.OpenXml.Spreadsheet.CellFormat;
using AlaaRep.Spreadsheet.Core.Models;

namespace AlaaRep.Spreadsheet.Core.Services
{
    /// <summary>
    /// Service for importing Excel files (.xlsx, .xls) and CSV files into the high-performance Spreadsheet model,
    /// matching basic formatting (colors, font, wrapping, alignment, number formats, column widths, row heights).
    /// </summary>
    public class ExcelImportService
    {
        static ExcelImportService()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public AppSpreadsheetDoc LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".csv")
            {
                return LoadFromCsv(filePath);
            }
            else if (ext == ".xlsx")
            {
                return LoadFromXlsxOpenXml(filePath);
            }
            else if (ext == ".xls")
            {
                return LoadFromXlsBiff(filePath);
            }
            else
            {
                throw new NotSupportedException($"Unsupported file extension: {ext}");
            }
        }

        /// <summary>
        /// Reads .xlsx file using OpenXML to capture full basic formatting (fonts, colors, wrap, widths, heights, numbers).
        /// </summary>
        public AppSpreadsheetDoc LoadFromXlsxOpenXml(string filePath)
        {
            var document = new AppSpreadsheetDoc();
            document.Sheets.Clear();

            using var spreadSheetDoc = OpenXmlSpreadsheetDoc.Open(filePath, false);
            var workbookPart = spreadSheetDoc.WorkbookPart;
            if (workbookPart == null) return document;

            // Shared String Table
            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
            var stringList = sharedStrings != null ? sharedStrings.Elements<SharedStringItem>().Select(s => s.InnerText).ToList() : new List<string>();

            // Stylesheet
            var stylesPart = workbookPart.WorkbookStylesPart;
            var stylesheet = stylesPart?.Stylesheet;
            var cellFormats = stylesheet?.CellFormats?.Elements<OpenXmlCellFormat>().ToList() ?? new List<OpenXmlCellFormat>();
            var fonts = stylesheet?.Fonts?.Elements<Font>().ToList() ?? new List<Font>();
            var fills = stylesheet?.Fills?.Elements<Fill>().ToList() ?? new List<Fill>();
            var numFormats = stylesheet?.NumberingFormats?.Elements<NumberingFormat>().ToDictionary(n => (int)n.NumberFormatId!.Value, n => n.FormatCode?.Value ?? string.Empty) ?? new Dictionary<int, string>();

            if (workbookPart.Workbook?.Sheets == null) return document;

            foreach (var sheetRef in workbookPart.Workbook.Sheets.Elements<Sheet>())
            {
                string sheetName = sheetRef.Name?.Value ?? $"Sheet{document.Sheets.Count + 1}";
                string relId = sheetRef.Id?.Value ?? string.Empty;
                if (string.IsNullOrEmpty(relId) || workbookPart.GetPartById(relId) is not WorksheetPart worksheetPart)
                    continue;

                var worksheet = worksheetPart.Worksheet;
                if (worksheet == null) continue;

                var sheetModel = new SheetDataModel(sheetName);

                // Column Widths
                var columns = worksheet.Elements<Columns>().FirstOrDefault();
                if (columns != null)
                {
                    foreach (var col in columns.Elements<Column>())
                    {
                        if (col.Width != null && col.Min != null && col.Max != null)
                        {
                            // Excel character width to pixels approximation: width * 7 + 12
                            double pxWidth = Math.Max(20, col.Width.Value * 7.5 + 12);
                            for (uint c = col.Min.Value; c <= col.Max.Value; c++)
                            {
                                sheetModel.SetColumnWidth((int)c - 1, pxWidth);
                            }
                        }
                    }
                }

                // Rows and Cells
                var sheetData = worksheet.Elements<SheetData>().FirstOrDefault();
                if (sheetData != null)
                {
                    foreach (var row in sheetData.Elements<Row>())
                    {
                        int rowIndex = row.RowIndex != null ? (int)row.RowIndex.Value - 1 : 0;

                        if (row.Height != null)
                        {
                            double pxHeight = Math.Max(15, row.Height.Value * 1.333);
                            sheetModel.SetRowHeight(rowIndex, pxHeight);
                        }

                        foreach (var cell in row.Elements<Cell>())
                        {
                            if (cell.CellReference?.Value == null) continue;

                            var coord = CellCoordinate.Parse(cell.CellReference.Value);
                            object? val = GetCellValue(cell, stringList);

                            // Extract CellFormat
                            var formatModel = ExtractCellFormat(cell, cellFormats, fonts, fills, numFormats);

                            if (val != null || formatModel != AppCellFormat.Default)
                            {
                                sheetModel.SetCellValue(coord.Row, coord.Column, val, notify: false);
                                if (formatModel != AppCellFormat.Default)
                                {
                                    sheetModel.SetCellFormat(coord.Row, coord.Column, formatModel, notify: false);
                                }
                            }
                        }
                    }
                }

                sheetModel.RecalculateBounds();
                document.Sheets.Add(sheetModel);
            }

            if (document.Sheets.Count == 0)
            {
                document.AddSheet("Sheet1");
            }

            document.ActiveSheet = document.Sheets[0];
            return document;
        }

        private static object? GetCellValue(Cell cell, List<string> sharedStrings)
        {
            if (cell.CellValue == null && cell.InlineString == null) return null;

            string raw = cell.CellValue?.Text ?? cell.InlineString?.Text?.Text ?? string.Empty;

            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                if (int.TryParse(raw, out int idx) && idx >= 0 && idx < sharedStrings.Count)
                {
                    return sharedStrings[idx];
                }
                return raw;
            }

            if (cell.DataType != null && cell.DataType.Value == CellValues.Boolean)
            {
                return raw == "1" || string.Equals(raw, "TRUE", StringComparison.OrdinalIgnoreCase);
            }

            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
            {
                return num;
            }

            return string.IsNullOrEmpty(raw) ? null : raw;
        }

        private static AppCellFormat ExtractCellFormat(
            Cell cell,
            List<OpenXmlCellFormat> cellFormats,
            List<Font> fonts,
            List<Fill> fills,
            Dictionary<int, string> numFormats)
        {
            if (cell.StyleIndex == null || (int)cell.StyleIndex.Value >= cellFormats.Count)
            {
                return AppCellFormat.Default;
            }

            var xf = cellFormats[(int)cell.StyleIndex.Value];
            string fontFamily = "Segoe UI";
            double fontSize = 12.0;
            bool isBold = false;
            bool isItalic = false;
            bool isUnderline = false;
            string? foreHex = "#FF000000";
            string? bgHex = null;
            bool wrapText = false;
            var hAlign = CellHorizontalAlignment.Left;
            var vAlign = CellVerticalAlignment.Center;
            var formatType = NumberFormatType.General;

            // Alignment & Wrap
            if (xf.Alignment != null)
            {
                if (xf.Alignment.WrapText != null && xf.Alignment.WrapText.Value)
                {
                    wrapText = true;
                }
                if (xf.Alignment.Horizontal != null)
                {
                    if (xf.Alignment.Horizontal.Value == HorizontalAlignmentValues.Center) hAlign = CellHorizontalAlignment.Center;
                    else if (xf.Alignment.Horizontal.Value == HorizontalAlignmentValues.Right) hAlign = CellHorizontalAlignment.Right;
                }
                if (xf.Alignment.Vertical != null)
                {
                    if (xf.Alignment.Vertical.Value == VerticalAlignmentValues.Top) vAlign = CellVerticalAlignment.Top;
                    else if (xf.Alignment.Vertical.Value == VerticalAlignmentValues.Bottom) vAlign = CellVerticalAlignment.Bottom;
                }
            }

            // Font
            if (xf.FontId != null && (int)xf.FontId.Value < fonts.Count)
            {
                var font = fonts[(int)xf.FontId.Value];
                if (font.FontName?.Val?.Value != null) fontFamily = font.FontName.Val.Value;
                if (font.FontSize?.Val?.Value != null) fontSize = font.FontSize.Val.Value;
                if (font.Bold != null) isBold = true;
                if (font.Italic != null) isItalic = true;
                if (font.Underline != null) isUnderline = true;
                if (font.Color?.Rgb?.Value != null)
                {
                    foreHex = "#" + font.Color.Rgb.Value;
                }
            }

            // Fill (Background Color)
            if (xf.FillId != null && (int)xf.FillId.Value < fills.Count)
            {
                var fill = fills[(int)xf.FillId.Value];
                var patternFill = fill.PatternFill;
                if (patternFill != null && patternFill.PatternType?.Value != PatternValues.None)
                {
                    if (patternFill.ForegroundColor?.Rgb?.Value != null)
                    {
                        bgHex = "#" + patternFill.ForegroundColor.Rgb.Value;
                    }
                    else if (patternFill.BackgroundColor?.Rgb?.Value != null)
                    {
                        bgHex = "#" + patternFill.BackgroundColor.Rgb.Value;
                    }
                }
            }

            // Number Format
            if (xf.NumberFormatId != null)
            {
                int numId = (int)xf.NumberFormatId.Value;
                if (numId == 1 || numId == 2 || numId == 3 || numId == 4) formatType = NumberFormatType.Number;
                else if (numId == 5 || numId == 6 || numId == 7 || numId == 8 || numId == 44) formatType = NumberFormatType.Currency;
                else if (numId == 9 || numId == 10) formatType = NumberFormatType.Percentage;
                else if (numId >= 14 && numId <= 22) formatType = NumberFormatType.ShortDate;
                else if (numFormats.TryGetValue(numId, out string? fmtCode))
                {
                    if (fmtCode.Contains("$") || fmtCode.Contains("€") || fmtCode.Contains("£")) formatType = NumberFormatType.Currency;
                    else if (fmtCode.Contains("%")) formatType = NumberFormatType.Percentage;
                    else if (fmtCode.Contains("yy") || fmtCode.Contains("mm") || fmtCode.Contains("dd")) formatType = NumberFormatType.ShortDate;
                    else if (fmtCode.Contains("0") || fmtCode.Contains("#")) formatType = NumberFormatType.Number;
                }
            }

            return new AppCellFormat
            {
                FontFamily = fontFamily,
                FontSize = fontSize,
                IsBold = isBold,
                IsItalic = isItalic,
                IsUnderline = isUnderline,
                ForegroundColorHex = foreHex,
                BackgroundColorHex = bgHex,
                WrapText = wrapText,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                FormatType = formatType
            };
        }

        /// <summary>
        /// Reads Excel 97-2003 .xls files using ExcelDataReader with column width, row height, and number formatting.
        /// </summary>
        public AppSpreadsheetDoc LoadFromXlsBiff(string filePath)
        {
            var document = new AppSpreadsheetDoc();
            document.Sheets.Clear();

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);

            do
            {
                string sheetName = reader.Name ?? $"Sheet{document.Sheets.Count + 1}";
                var sheet = new SheetDataModel(sheetName);

                int rowIndex = 0;
                while (reader.Read())
                {
                    if (reader.RowHeight > 0)
                    {
                        sheet.SetRowHeight(rowIndex, Math.Max(15, reader.RowHeight * 1.333));
                    }

                    int fieldCount = reader.FieldCount;
                    for (int colIndex = 0; colIndex < fieldCount; colIndex++)
                    {
                        double colWidth = reader.GetColumnWidth(colIndex);
                        if (colWidth > 0)
                        {
                            sheet.SetColumnWidth(colIndex, Math.Max(20, colWidth * 7.5 + 12));
                        }

                        object? val = reader.GetValue(colIndex);
                        if (val != null && !(val is string s && string.IsNullOrEmpty(s)))
                        {
                            string numFmt = reader.GetNumberFormatString(colIndex) ?? string.Empty;
                            AppCellFormat? fmt = null;

                            if (!string.IsNullOrEmpty(numFmt))
                            {
                                var type = NumberFormatType.General;
                                if (numFmt.Contains("$") || numFmt.Contains("€")) type = NumberFormatType.Currency;
                                else if (numFmt.Contains("%")) type = NumberFormatType.Percentage;
                                else if (numFmt.Contains("yy") || numFmt.Contains("mm") || numFmt.Contains("dd")) type = NumberFormatType.ShortDate;
                                else if (numFmt.Contains("0") || numFmt.Contains("#")) type = NumberFormatType.Number;

                                if (type != NumberFormatType.General)
                                {
                                    fmt = new AppCellFormat { FormatType = type };
                                }
                            }

                            sheet.SetCellValue(rowIndex, colIndex, val, notify: false);
                            if (fmt != null)
                            {
                                sheet.SetCellFormat(rowIndex, colIndex, fmt, notify: false);
                            }
                        }
                    }
                    rowIndex++;
                }

                sheet.RecalculateBounds();
                document.Sheets.Add(sheet);

            } while (reader.NextResult());

            if (document.Sheets.Count == 0)
            {
                document.AddSheet("Sheet1");
            }

            document.ActiveSheet = document.Sheets[0];
            return document;
        }

        public AppSpreadsheetDoc LoadFromCsv(string filePath)
        {
            var document = new AppSpreadsheetDoc();
            document.Sheets.Clear();

            string sheetName = Path.GetFileNameWithoutExtension(filePath);
            var sheet = new SheetDataModel(sheetName);

            using var reader = new StreamReader(filePath, Encoding.UTF8);
            string? line;
            int rowIndex = 0;

            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = ParseCsvLine(line);
                for (int colIndex = 0; colIndex < parts.Length; colIndex++)
                {
                    string cellText = parts[colIndex];
                    if (!string.IsNullOrEmpty(cellText))
                    {
                        if (double.TryParse(cellText, out double dVal))
                        {
                            sheet.SetCellValue(rowIndex, colIndex, dVal, notify: false);
                        }
                        else
                        {
                            sheet.SetCellValue(rowIndex, colIndex, cellText, notify: false);
                        }
                    }
                }
                rowIndex++;
            }

            sheet.RecalculateBounds();
            document.Sheets.Add(sheet);
            document.ActiveSheet = sheet;
            return document;
        }

        public void ExportToCsv(SheetDataModel sheet, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            int maxRow = sheet.MaxOccupiedRow;
            int maxCol = sheet.MaxOccupiedColumn;

            if (maxRow < 0 || maxCol < 0) return;

            for (int r = 0; r <= maxRow; r++)
            {
                var rowItems = new string[maxCol + 1];
                for (int c = 0; c <= maxCol; c++)
                {
                    var cell = sheet.GetCell(r, c);
                    string text = cell?.GetFormattedText() ?? string.Empty;
                    rowItems[c] = EscapeCsvField(text);
                }
                writer.WriteLine(string.Join(",", rowItems));
            }
        }

        public static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        current.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }
    }
}
