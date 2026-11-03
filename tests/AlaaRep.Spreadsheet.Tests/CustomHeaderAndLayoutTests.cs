using System;
using AlaaRep.Spreadsheet.Core.Models;
using Xunit;

namespace AlaaRep.Spreadsheet.Tests
{
    public class CustomHeaderAndLayoutTests
    {
        [Fact]
        public void SheetDataModel_CustomColumnHeaders_DefaultAndCustom()
        {
            var sheet = new SheetDataModel("HeaderTest");

            // Default Excel letter headers
            Assert.Equal("A", sheet.GetColumnHeader(0));
            Assert.Equal("B", sheet.GetColumnHeader(1));
            Assert.Equal("Z", sheet.GetColumnHeader(25));
            Assert.Equal("AA", sheet.GetColumnHeader(26));

            // Set custom titles (Activity, Cost, Qty, per %, etc.)
            sheet.SetColumnHeader(0, "Activity");
            sheet.SetColumnHeader(1, "Cost");
            sheet.SetColumnHeader(2, "Qty");
            sheet.SetColumnHeader(3, "per %");

            Assert.Equal("Activity", sheet.GetColumnHeader(0));
            Assert.Equal("Cost", sheet.GetColumnHeader(1));
            Assert.Equal("Qty", sheet.GetColumnHeader(2));
            Assert.Equal("per %", sheet.GetColumnHeader(3));
            Assert.Equal("E", sheet.GetColumnHeader(4)); // Still default

            // Resetting header back to default
            sheet.SetColumnHeader(0, "");
            Assert.Equal("A", sheet.GetColumnHeader(0));
        }

        [Fact]
        public void VirtualLayoutManager_HandlesVariableColumnWidthsAndRowHeights()
        {
            var sheet = new SheetDataModel("LayoutTest")
            {
                DefaultRowHeight = 20.0,
                DefaultColumnWidth = 100.0
            };

            var layout = new VirtualLayoutManager(sheet);

            // With default sizes
            Assert.Equal(0, layout.GetColumnOffsetFromScroll(0, 0));
            Assert.Equal(100, layout.GetColumnOffsetFromScroll(0, 1));
            Assert.Equal(300, layout.GetColumnOffsetFromScroll(0, 3));

            // Custom column widths
            sheet.SetColumnWidth(0, 150); // Col 0 is 150
            sheet.SetColumnWidth(1, 200); // Col 1 is 200
            // Col 2 is default 100

            Assert.Equal(0, layout.GetColumnOffsetFromScroll(0, 0));
            Assert.Equal(150, layout.GetColumnOffsetFromScroll(0, 1));
            Assert.Equal(350, layout.GetColumnOffsetFromScroll(0, 2));
            Assert.Equal(450, layout.GetColumnOffsetFromScroll(0, 3));

            // Hit test column
            int hitCol1 = layout.GetColumnAtX(0, 100, out double x1, out double w1);
            Assert.Equal(0, hitCol1);
            Assert.Equal(0, x1);
            Assert.Equal(150, w1);

            int hitCol2 = layout.GetColumnAtX(0, 160, out double x2, out double w2);
            Assert.Equal(1, hitCol2);
            Assert.Equal(150, x2);
            Assert.Equal(200, w2);

            // Custom row heights
            sheet.SetRowHeight(0, 40); // Row 0 is 40
            sheet.SetRowHeight(1, 50); // Row 1 is 50
            // Row 2 is default 20

            Assert.Equal(0, layout.GetRowOffsetFromScroll(0, 0));
            Assert.Equal(40, layout.GetRowOffsetFromScroll(0, 1));
            Assert.Equal(90, layout.GetRowOffsetFromScroll(0, 2));

            int hitRow1 = layout.GetRowAtY(0, 30, out double y1, out double h1);
            Assert.Equal(0, hitRow1);
            Assert.Equal(0, y1);
            Assert.Equal(40, h1);

            int hitRow2 = layout.GetRowAtY(0, 60, out double y2, out double h2);
            Assert.Equal(1, hitRow2);
            Assert.Equal(40, y2);
            Assert.Equal(50, h2);
        }
    }
}
