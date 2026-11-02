using System;
using AlaaRep.Spreadsheet.Core.Models;
using AlaaRep.Spreadsheet.Core.Services;
using Xunit;

namespace AlaaRep.Spreadsheet.Tests
{
    public class ClipboardTests
    {
        [Fact]
        public void ClipboardService_CopiesAndPastesTabDelimitedData()
        {
            var sheet = new SheetDataModel("TestSheet");
            sheet.SetCellValue(0, 0, "Name");
            sheet.SetCellValue(0, 1, "Age");
            sheet.SetCellValue(1, 0, "Alice");
            sheet.SetCellValue(1, 1, 30);
            sheet.SetCellValue(2, 0, "Bob");
            sheet.SetCellValue(2, 1, 25.5);

            var clipboard = new ClipboardService();
            string text = clipboard.CopyRangeToText(sheet, new CellRange(0, 0, 2, 1));

            Assert.Contains("Name\tAge", text);
            Assert.Contains("Alice\t30", text);
            Assert.Contains("Bob\t25.5", text);

            var targetSheet = new SheetDataModel("TargetSheet");
            var pastedRange = clipboard.PasteTextToSheet(targetSheet, new CellCoordinate(5, 5), text);

            Assert.Equal(5, pastedRange.Top);
            Assert.Equal(7, pastedRange.Bottom);
            Assert.Equal(5, pastedRange.Left);
            Assert.Equal(6, pastedRange.Right);

            Assert.Equal("Name", targetSheet.GetCellValue(5, 5));
            Assert.Equal("Age", targetSheet.GetCellValue(5, 6));
            Assert.Equal("Alice", targetSheet.GetCellValue(6, 5));
            Assert.Equal(30.0, targetSheet.GetCellValue(6, 6));
            Assert.Equal("Bob", targetSheet.GetCellValue(7, 5));
            Assert.Equal(25.5, targetSheet.GetCellValue(7, 6));
        }
    }
}
