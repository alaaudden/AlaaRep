using System;
using AlaaRep.Spreadsheet.Core.Models;
using Xunit;

namespace AlaaRep.Spreadsheet.Tests
{
    public class MultiRowColumnSelectionTests
    {
        [Fact]
        public void SelectionModel_MultiRowSelection()
        {
            var sel = new SelectionModel();

            // Select single row
            sel.SelectRow(2, maxCol: 50);
            Assert.Equal(SelectionModeType.Rows, sel.Mode);
            Assert.Equal(2, sel.SelectionRange.Top);
            Assert.Equal(2, sel.SelectionRange.Bottom);
            Assert.Equal(0, sel.SelectionRange.Left);
            Assert.Equal(50, sel.SelectionRange.Right);
            Assert.True(sel.IsRowSelected(2));
            Assert.False(sel.IsRowSelected(3));

            // Extend row selection to row 5 (multi-row 2..5)
            sel.ExtendRowSelection(5, maxCol: 50);
            Assert.Equal(2, sel.SelectionRange.Top);
            Assert.Equal(5, sel.SelectionRange.Bottom);
            Assert.True(sel.IsRowSelected(2));
            Assert.True(sel.IsRowSelected(3));
            Assert.True(sel.IsRowSelected(4));
            Assert.True(sel.IsRowSelected(5));
            Assert.False(sel.IsRowSelected(1));
            Assert.False(sel.IsRowSelected(6));

            // Extend backward to row 0 (multi-row 0..2)
            sel.ExtendRowSelection(0, maxCol: 50);
            Assert.Equal(0, sel.SelectionRange.Top);
            Assert.Equal(2, sel.SelectionRange.Bottom);
            Assert.True(sel.IsRowSelected(0));
            Assert.True(sel.IsRowSelected(1));
            Assert.True(sel.IsRowSelected(2));
            Assert.False(sel.IsRowSelected(3));
        }

        [Fact]
        public void SelectionModel_MultiColumnSelection()
        {
            var sel = new SelectionModel();

            // Select column 3
            sel.SelectColumn(3, maxRow: 100);
            Assert.Equal(SelectionModeType.Columns, sel.Mode);
            Assert.Equal(3, sel.SelectionRange.Left);
            Assert.Equal(3, sel.SelectionRange.Right);
            Assert.Equal(0, sel.SelectionRange.Top);
            Assert.Equal(100, sel.SelectionRange.Bottom);
            Assert.True(sel.IsColumnSelected(3));
            Assert.False(sel.IsColumnSelected(2));

            // Extend column selection to col 7 (multi-column 3..7)
            sel.ExtendColumnSelection(7, maxRow: 100);
            Assert.Equal(3, sel.SelectionRange.Left);
            Assert.Equal(7, sel.SelectionRange.Right);
            Assert.True(sel.IsColumnSelected(3));
            Assert.True(sel.IsColumnSelected(5));
            Assert.True(sel.IsColumnSelected(7));
            Assert.False(sel.IsColumnSelected(2));
            Assert.False(sel.IsColumnSelected(8));
        }
    }
}
