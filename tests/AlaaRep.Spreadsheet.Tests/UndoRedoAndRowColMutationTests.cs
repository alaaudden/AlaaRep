using System;
using AlaaRep.Spreadsheet.Core.Models;
using AlaaRep.Spreadsheet.Core.Services;
using Xunit;

namespace AlaaRep.Spreadsheet.Tests
{
    public class UndoRedoAndRowColMutationTests
    {
        [Fact]
        public void SheetDataModel_InsertAndDeleteRow_ShiftsDataAndHeights()
        {
            var sheet = new SheetDataModel("MutationTest");
            sheet.SetCellValue(0, 0, "Row0");
            sheet.SetCellValue(1, 0, "Row1");
            sheet.SetCellValue(2, 0, "Row2");
            sheet.SetRowHeight(1, 45.0);

            // Insert row at index 1
            sheet.InsertRow(1);

            Assert.Equal("Row0", sheet.GetCellValue(0, 0));
            Assert.Null(sheet.GetCellValue(1, 0)); // Newly inserted empty row
            Assert.Equal("Row1", sheet.GetCellValue(2, 0)); // Shifted down
            Assert.Equal("Row2", sheet.GetCellValue(3, 0)); // Shifted down
            Assert.Equal(45.0, sheet.GetRowHeight(2)); // Row height shifted

            // Delete row at index 1
            sheet.DeleteRow(1);

            Assert.Equal("Row0", sheet.GetCellValue(0, 0));
            Assert.Equal("Row1", sheet.GetCellValue(1, 0));
            Assert.Equal("Row2", sheet.GetCellValue(2, 0));
            Assert.Equal(45.0, sheet.GetRowHeight(1));
        }

        [Fact]
        public void SheetDataModel_InsertAndDeleteColumn_ShiftsDataWidthsAndHeaders()
        {
            var sheet = new SheetDataModel("MutationColTest");
            sheet.SetCellValue(0, 0, "Col0");
            sheet.SetCellValue(0, 1, "Col1");
            sheet.SetColumnWidth(1, 130.0);
            sheet.SetColumnHeader(1, "Activity");

            // Insert column at 1
            sheet.InsertColumn(1);

            Assert.Equal("Col0", sheet.GetCellValue(0, 0));
            Assert.Null(sheet.GetCellValue(0, 1));
            Assert.Equal("Col1", sheet.GetCellValue(0, 2));
            Assert.Equal(130.0, sheet.GetColumnWidth(2));
            Assert.Equal("Activity", sheet.GetColumnHeader(2));

            // Delete column at 1
            sheet.DeleteColumn(1);

            Assert.Equal("Col0", sheet.GetCellValue(0, 0));
            Assert.Equal("Col1", sheet.GetCellValue(0, 1));
            Assert.Equal(130.0, sheet.GetColumnWidth(1));
            Assert.Equal("Activity", sheet.GetColumnHeader(1));
        }

        [Fact]
        public void UndoRedoManager_UndoRedoCellValueAndHeader()
        {
            var sheet = new SheetDataModel("UndoSheet");
            var undoManager = new UndoRedoManager();

            // 1. Cell Value Command
            var cellCmd = new SetCellValueCommand(sheet, 0, 0, null, "FirstVal");
            undoManager.ExecuteCommand(cellCmd);
            Assert.Equal("FirstVal", sheet.GetCellValue(0, 0));

            var cellCmd2 = new SetCellValueCommand(sheet, 0, 0, "FirstVal", "SecondVal");
            undoManager.ExecuteCommand(cellCmd2);
            Assert.Equal("SecondVal", sheet.GetCellValue(0, 0));

            // Undo
            undoManager.Undo();
            Assert.Equal("FirstVal", sheet.GetCellValue(0, 0));

            // Redo
            undoManager.Redo();
            Assert.Equal("SecondVal", sheet.GetCellValue(0, 0));

            // 2. Header Title Command
            var headerCmd = new SetColumnHeaderCommand(sheet, 0, "A", "Activity");
            undoManager.ExecuteCommand(headerCmd);
            Assert.Equal("Activity", sheet.GetColumnHeader(0));

            undoManager.Undo();
            Assert.Equal("A", sheet.GetColumnHeader(0));

            undoManager.Redo();
            Assert.Equal("Activity", sheet.GetColumnHeader(0));
        }

        [Fact]
        public void UndoRedoManager_UndoRedoInsertAndDeleteRow()
        {
            var sheet = new SheetDataModel("UndoRowSheet");
            sheet.SetCellValue(0, 0, "R0");
            sheet.SetCellValue(1, 0, "R1");
            var undoManager = new UndoRedoManager();

            // Insert row
            var insertCmd = new InsertRowCommand(sheet, 1);
            undoManager.ExecuteCommand(insertCmd);
            Assert.Null(sheet.GetCellValue(1, 0));
            Assert.Equal("R1", sheet.GetCellValue(2, 0));

            undoManager.Undo();
            Assert.Equal("R1", sheet.GetCellValue(1, 0));

            undoManager.Redo();
            Assert.Null(sheet.GetCellValue(1, 0));
            Assert.Equal("R1", sheet.GetCellValue(2, 0));

            // Delete row
            var deleteCmd = new DeleteRowCommand(sheet, 2);
            undoManager.ExecuteCommand(deleteCmd);
            Assert.Null(sheet.GetCellValue(2, 0));

            undoManager.Undo();
            Assert.Equal("R1", sheet.GetCellValue(2, 0));
        }
    }
}
