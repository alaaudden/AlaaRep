using System;
using System.Data;
using System.IO;
using System.Text;
using ExcelDataReader;
using AlaaRep.Spreadsheet.Core.Models;

namespace AlaaRep.Spreadsheet.Core.Services
{
    /// <summary>
    /// Service for importing Excel files (.xlsx, .xls) and CSV files into the high-performance Spreadsheet model,
    /// and exporting to CSV.
    /// </summary>
    public class ExcelImportService
    {
        static ExcelImportService()
        {
            // Register encoding provider for ExcelDataReader code page support (e.g. windows-1252)
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Reads an Excel file (.xlsx or .xls) or CSV and populates a SpreadsheetDocument.
        /// Uses reader streaming to minimize memory usage for huge datasets.
        /// </summary>
        public SpreadsheetDocument LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".csv")
            {
                return LoadFromCsv(filePath);
            }
            else if (ext == ".xlsx" || ext == ".xls")
            {
                return LoadFromExcel(filePath);
            }
            else
            {
                throw new NotSupportedException($"Unsupported file extension: {ext}");
            }
        }

        public SpreadsheetDocument LoadFromExcel(string filePath)
        {
            var document = new SpreadsheetDocument();
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
                    int fieldCount = reader.FieldCount;
                    for (int colIndex = 0; colIndex < fieldCount; colIndex++)
                    {
                        object? val = reader.GetValue(colIndex);
                        if (val != null && !(val is string s && string.IsNullOrEmpty(s)))
                        {
                            sheet.SetCellValue(rowIndex, colIndex, val, notify: false);
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

        public SpreadsheetDocument LoadFromCsv(string filePath)
        {
            var document = new SpreadsheetDocument();
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
                        // Try convert number
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

            if (maxRow < 0 || maxCol < 0)
            {
                // Empty sheet
                return;
            }

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
            var result = new System.Collections.Generic.List<string>();
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
                        i++; // Skip escaped quote
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
