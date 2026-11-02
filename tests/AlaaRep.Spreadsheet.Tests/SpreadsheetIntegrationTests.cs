using System;
using System.IO;
using System.Linq;
using AlaaRep.Spreadsheet.Core.Models;
using AlaaRep.Spreadsheet.Core.Services;
using Xunit;

namespace AlaaRep.Spreadsheet.Tests
{
    public class SpreadsheetIntegrationTests
    {
        [Fact]
        public void SpreadsheetDocument_SheetManagementWorks()
        {
            var doc = new SpreadsheetDocument();
            Assert.Single(doc.Sheets);
            Assert.Equal("Sheet1", doc.ActiveSheet.Name);

            var sheet2 = doc.AddSheet();
            Assert.Equal("Sheet2", sheet2.Name);
            Assert.Equal(2, doc.Sheets.Count);
            Assert.Same(sheet2, doc.ActiveSheet);

            var sheet3 = doc.AddSheet("CustomSheet");
            Assert.Equal("CustomSheet", sheet3.Name);
            Assert.Equal(3, doc.Sheets.Count);

            bool removed = doc.RemoveSheet(sheet2);
            Assert.True(removed);
            Assert.Equal(2, doc.Sheets.Count);

            // Removing down to 1 sheet
            doc.RemoveSheet(sheet3);
            Assert.Single(doc.Sheets);
            // Cannot remove last sheet
            bool removedLast = doc.RemoveSheet(doc.Sheets[0]);
            Assert.False(removedLast);
            Assert.Single(doc.Sheets);
        }

        [Fact]
        public void SheetDataModel_BatchLoadAndClearOperations()
        {
            var sheet = new SheetDataModel("LargeSheet");

            var items = Enumerable.Range(0, 10_000)
                .Select(i => (Row: i, Col: 0, Value: (object?)$"Item_{i}", Format: (CellFormat?)null));

            sheet.BatchLoad(items);

            Assert.Equal(9999, sheet.MaxOccupiedRow);
            Assert.Equal(0, sheet.MaxOccupiedColumn);
            Assert.Equal("Item_0", sheet.GetCellValue(0, 0));
            Assert.Equal("Item_9999", sheet.GetCellValue(9999, 0));

            // Apply formatting over range
            sheet.ApplyFormatToRange(new CellRange(0, 0, 10, 0), f => f with { IsBold = true });
            Assert.True(sheet.GetCellFormat(5, 0).IsBold);
            Assert.False(sheet.GetCellFormat(11, 0).IsBold);

            // Clear partial range
            sheet.ClearRange(new CellRange(0, 0, 5, 0));
            Assert.Null(sheet.GetCellValue(0, 0));
            Assert.Equal("Item_6", sheet.GetCellValue(6, 0));

            // Clear all
            sheet.ClearAll();
            Assert.Equal(-1, sheet.MaxOccupiedRow);
            Assert.Null(sheet.GetCellValue(6, 0));
        }

        [Fact]
        public void SelectionModel_SelectionNavigation()
        {
            var sel = new SelectionModel();
            Assert.Equal(new CellCoordinate(0, 0), sel.ActiveCell);

            sel.SelectCell(5, 5);
            Assert.Equal(5, sel.ActiveCell.Row);
            Assert.Equal(5, sel.ActiveCell.Column);
            Assert.Equal(new CellRange(5, 5, 5, 5), sel.SelectionRange);

            sel.ExtendSelection(10, 12);
            Assert.Equal(5, sel.ActiveCell.Row);
            Assert.Equal(5, sel.ActiveCell.Column);
            Assert.Equal(5, sel.SelectionRange.Top);
            Assert.Equal(10, sel.SelectionRange.Bottom);
            Assert.Equal(5, sel.SelectionRange.Left);
            Assert.Equal(12, sel.SelectionRange.Right);
            Assert.True(sel.IsSelected(7, 8));
            Assert.False(sel.IsSelected(4, 5));
        }

        [Fact]
        public void ExcelImportService_EscapedCsvHandling()
        {
            string line = "1,\"Hello, \"\"World\"\"!\",Normal,123.45";
            string[] parsed = ExcelImportService.ParseCsvLine(line);

            Assert.Equal(4, parsed.Length);
            Assert.Equal("1", parsed[0]);
            Assert.Equal("Hello, \"World\"!", parsed[1]);
            Assert.Equal("Normal", parsed[2]);
            Assert.Equal("123.45", parsed[3]);
        }
    }
}
