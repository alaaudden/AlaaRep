using System;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using AlaaRep.Spreadsheet.Core.Models;
using AlaaRep.Spreadsheet.Core.Services;
using Xunit;

namespace AlaaRep.Spreadsheet.Tests
{
    public class ExcelFormattingImportTests
    {
        [Fact]
        public void ExcelImportService_ImportsFormattedXlsxFileCorrectly()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");

            try
            {
                // Create a styled XLSX using OpenXML
                using (var document = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Create(tempFile, SpreadsheetDocumentType.Workbook))
                {
                    var workbookPart = document.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();

                    var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                    var stylesheet = new Stylesheet();

                    // Fonts
                    var fonts = new Fonts();
                    fonts.Append(new Font()); // 0: Default
                    var boldFont = new Font(new Bold(), new Color() { Rgb = "FFFF0000" }, new FontName() { Val = "Arial" }, new FontSize() { Val = 14 });
                    fonts.Append(boldFont); // 1: Bold red Arial 14
                    stylesheet.Fonts = fonts;

                    // Fills
                    var fills = new Fills();
                    fills.Append(new Fill(new PatternFill() { PatternType = PatternValues.None })); // 0: None
                    fills.Append(new Fill(new PatternFill() { PatternType = PatternValues.Gray125 })); // 1: Gray125
                    var yellowFill = new Fill(new PatternFill(new ForegroundColor() { Rgb = "FFFFFF00" }) { PatternType = PatternValues.Solid });
                    fills.Append(yellowFill); // 2: Yellow fill
                    stylesheet.Fills = fills;

                    // Borders
                    var borders = new Borders();
                    borders.Append(new Border());
                    stylesheet.Borders = borders;

                    // CellFormats
                    var cellFormats = new CellFormats();
                    cellFormats.Append(new DocumentFormat.OpenXml.Spreadsheet.CellFormat()); // 0: Default
                    var customFormat = new DocumentFormat.OpenXml.Spreadsheet.CellFormat()
                    {
                        FontId = 1,
                        FillId = 2,
                        NumberFormatId = 4, // #,##0.00
                        ApplyFont = true,
                        ApplyFill = true,
                        ApplyNumberFormat = true,
                        Alignment = new Alignment() { WrapText = true, Horizontal = HorizontalAlignmentValues.Right }
                    };
                    cellFormats.Append(customFormat); // 1: Custom
                    stylesheet.CellFormats = cellFormats;

                    stylesPart.Stylesheet = stylesheet;
                    stylesPart.Stylesheet.Save();

                    // Worksheet
                    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    var sheetData = new SheetData();

                    // Column widths
                    var columns = new Columns();
                    columns.Append(new Column() { Min = 1, Max = 1, Width = 25, CustomWidth = true });
                    var worksheet = new Worksheet();
                    worksheet.Append(columns);

                    // Row with custom height & cell with custom style
                    var row = new Row() { RowIndex = 1, Height = 35, CustomHeight = true };
                    var cell = new Cell()
                    {
                        CellReference = "A1",
                        DataType = CellValues.Number,
                        CellValue = new CellValue("1234.56"),
                        StyleIndex = 1
                    };
                    row.Append(cell);
                    sheetData.Append(row);
                    worksheet.Append(sheetData);

                    worksheetPart.Worksheet = worksheet;
                    worksheetPart.Worksheet.Save();

                    var sheets = document.WorkbookPart.Workbook.AppendChild(new Sheets());
                    sheets.Append(new Sheet()
                    {
                        Id = document.WorkbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1,
                        Name = "StyledSheet"
                    });
                    workbookPart.Workbook.Save();
                }

                // Import using ExcelImportService
                var importService = new ExcelImportService();
                var importedDoc = importService.LoadFromFile(tempFile);

                Assert.Single(importedDoc.Sheets);
                var sheetModel = importedDoc.ActiveSheet;
                Assert.Equal("StyledSheet", sheetModel.Name);

                // Verify value
                Assert.Equal(1234.56, sheetModel.GetCellValue(0, 0));

                // Verify dimensions
                Assert.True(sheetModel.GetColumnWidth(0) > 100); // 25 char width scaled to px
                Assert.True(sheetModel.GetRowHeight(0) > 30);    // 35 pt height scaled to px

                // Verify basic formatting
                var fmt = sheetModel.GetCellFormat(0, 0);
                Assert.True(fmt.IsBold);
                Assert.Equal("Arial", fmt.FontFamily);
                Assert.Equal(14, fmt.FontSize);
                Assert.Equal("#FFFF0000", fmt.ForegroundColorHex);
                Assert.Equal("#FFFFFF00", fmt.BackgroundColorHex);
                Assert.True(fmt.WrapText);
                Assert.Equal(CellHorizontalAlignment.Right, fmt.HorizontalAlignment);
                Assert.Equal(NumberFormatType.Number, fmt.FormatType);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
