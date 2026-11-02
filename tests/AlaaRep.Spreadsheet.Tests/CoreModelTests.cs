using System;
using System.IO;
using AlaaRep.Spreadsheet.Core.Models;
using AlaaRep.Spreadsheet.Core.Services;
using Xunit;

namespace AlaaRep.Spreadsheet.Tests
{
    public class CoreModelTests
    {
        [Fact]
        public void CellCoordinate_ConvertsBothWays()
        {
            Assert.Equal("A1", CellCoordinate.Format(0, 0));
            Assert.Equal("B5", CellCoordinate.Format(4, 1));
            Assert.Equal("Z1", CellCoordinate.Format(0, 25));
            Assert.Equal("AA1", CellCoordinate.Format(0, 26));
            Assert.Equal("AB1", CellCoordinate.Format(0, 27));

            var coord = CellCoordinate.Parse("A1");
            Assert.Equal(0, coord.Row);
            Assert.Equal(0, coord.Column);

            var coord2 = CellCoordinate.Parse("AA10");
            Assert.Equal(9, coord2.Row);
            Assert.Equal(26, coord2.Column);
        }

        [Fact]
        public void CellRange_CalculatesBoundsAndContains()
        {
            var range = new CellRange(new CellCoordinate(1, 2), new CellCoordinate(4, 5));
            Assert.Equal(1, range.Top);
            Assert.Equal(4, range.Bottom);
            Assert.Equal(2, range.Left);
            Assert.Equal(5, range.Right);
            Assert.Equal(4, range.RowCount);
            Assert.Equal(4, range.ColumnCount);
            Assert.Equal(16, range.TotalCells);

            Assert.True(range.Contains(2, 3));
            Assert.False(range.Contains(0, 3));
        }

        [Fact]
        public void SheetDataModel_SparseStorageWorksCorrectly()
        {
            var sheet = new SheetDataModel("TestSheet");
            sheet.SetCellValue(100, 50, "Hello 100,50");
            sheet.SetCellValue(500000, 200, 12345.67);

            Assert.Equal("Hello 100,50", sheet.GetCellValue(100, 50));
            Assert.Equal(12345.67, sheet.GetCellValue(500000, 200));
            Assert.Null(sheet.GetCellValue(0, 0));
            Assert.Equal(500000, sheet.MaxOccupiedRow);
            Assert.Equal(200, sheet.MaxOccupiedColumn);
        }

        [Fact]
        public void CellFormat_FormatsNumbersAndCurrency()
        {
            var formatNumber = new CellFormat { FormatType = NumberFormatType.Number };
            Assert.Equal("1,234.56", formatNumber.FormatValue(1234.56));

            var formatCurrency = new CellFormat { FormatType = NumberFormatType.Currency };
            Assert.Contains("1,234.56", formatCurrency.FormatValue(1234.56));

            var formatPercent = new CellFormat { FormatType = NumberFormatType.Percentage };
            Assert.Equal("25.00%", formatPercent.FormatValue(0.25));
        }

        [Fact]
        public void ExcelImportService_CsvImportAndExportWorks()
        {
            var service = new ExcelImportService();
            string tempCsv = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempCsv, "ColA,ColB,ColC\nVal1,100,200.5\nVal2,300,\"Quoted, Text\"\n");

                var doc = service.LoadFromCsv(tempCsv);
                Assert.Single(doc.Sheets);
                var sheet = doc.ActiveSheet;

                Assert.Equal("ColA", sheet.GetCellValue(0, 0));
                Assert.Equal("ColB", sheet.GetCellValue(0, 1));
                Assert.Equal("ColC", sheet.GetCellValue(0, 2));

                Assert.Equal("Val1", sheet.GetCellValue(1, 0));
                Assert.Equal(100.0, sheet.GetCellValue(1, 1));
                Assert.Equal(200.5, sheet.GetCellValue(1, 2));

                Assert.Equal("Val2", sheet.GetCellValue(2, 0));
                Assert.Equal(300.0, sheet.GetCellValue(2, 1));
                Assert.Equal("Quoted, Text", sheet.GetCellValue(2, 2));

                string exportCsv = Path.GetTempFileName();
                try
                {
                    service.ExportToCsv(sheet, exportCsv);
                    string exportedContent = File.ReadAllText(exportCsv);
                    Assert.Contains("ColA,ColB,ColC", exportedContent);
                    Assert.Contains("\"Quoted, Text\"", exportedContent);
                }
                finally
                {
                    if (File.Exists(exportCsv)) File.Delete(exportCsv);
                }
            }
            finally
            {
                if (File.Exists(tempCsv)) File.Delete(tempCsv);
            }
        }
    }
}
