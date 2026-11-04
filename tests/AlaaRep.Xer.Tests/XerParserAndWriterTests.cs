using System;
using System.IO;
using System.Linq;
using AlaaRep.Xer.Entities;
using AlaaRep.Xer.Models;
using AlaaRep.Xer.Services;
using Xunit;

namespace AlaaRep.Xer.Tests;

public class XerParserAndWriterTests
{
    private static readonly string TestDataDir = Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public void ReadFile_ParsesBaselineProject_Successfully()
    {
        var filePath = Path.Combine(TestDataDir, "baseline_project.xer");
        var doc = XerReader.ReadFile(filePath);

        Assert.NotNull(doc);
        Assert.StartsWith("ERMHDR", doc.HeaderLine);
        Assert.True(doc.Tables.Count >= 4);

        var projectTable = doc.ProjectTable;
        Assert.NotNull(projectTable);
        Assert.Single(projectTable.Rows);
        Assert.Equal("Marina Commercial Tower - Baseline", projectTable.Rows[0]["proj_short_name"]);

        var taskTable = doc.TaskTable;
        Assert.NotNull(taskTable);
        Assert.True(taskTable.Rows.Count >= 20);

        var firstTask = taskTable.Rows[0];
        Assert.Equal("M1000", firstTask["task_code"]);
        Assert.Equal("Notice to Proceed (NTP)", firstTask["task_name"]);
    }

    [Fact]
    public void ReadFile_ParsesUpdateProject_WithProgressData()
    {
        var filePath = Path.Combine(TestDataDir, "update_week12.xer");
        var doc = XerReader.ReadFile(filePath);

        Assert.NotNull(doc);
        var taskTable = doc.TaskTable;
        Assert.NotNull(taskTable);

        // Find completed and in-progress activities
        var completedTasks = taskTable.Rows.Where(r => r["status_code"] == "TK_Complete").ToList();
        Assert.NotEmpty(completedTasks);

        var inProgressTasks = taskTable.Rows.Where(r => r["status_code"] == "TK_Active").ToList();
        Assert.NotEmpty(inProgressTasks);
    }

    [Fact]
    public void TaskActivity_WrapperReflectsAndMutatesValues()
    {
        var doc = SampleXerGenerator.CreateSampleProject("Test Tower");
        var taskTable = doc.TaskTable!;
        var row = taskTable.Rows.First(r => r["task_code"] == "ENG1020");

        var activity = new TaskActivity(row);
        Assert.Equal("ENG1020", activity.TaskCode);
        Assert.Equal("Structural Design Package", activity.TaskName);
        Assert.Equal(75.0, activity.PhysicalPercentComplete);

        // Mutate through strongly typed wrapper
        activity.TaskName = "Structural Design Package - Revised";
        activity.PhysicalPercentComplete = 90.0;
        activity.StatusDisplayName = "Completed";

        // Check underlying row updated
        Assert.Equal("Structural Design Package - Revised", row["task_name"]);
        Assert.Equal("90.0", row["phys_complete_pct"]);
        Assert.Equal("TK_Complete", row["status_code"]);
    }

    [Fact]
    public void RoundTrip_ParseModifyWriteParse_PreservesIntegrity()
    {
        var sourceFilePath = Path.Combine(TestDataDir, "baseline_project.xer");
        var originalDoc = XerReader.ReadFile(sourceFilePath);

        var originalTaskCount = originalDoc.TaskTable!.Rows.Count;
        var originalTableCount = originalDoc.Tables.Count;

        // Perform modifications
        // 1. Modify project name
        originalDoc.PrimaryProject!["proj_short_name"] = "Marina Tower Renovation 2026";

        // 2. Modify an activity date and name
        var activityRow = originalDoc.TaskTable.Rows.First(r => r["task_code"] == "ENG1010");
        activityRow["task_name"] = "Site Survey & Deep Scanning";
        activityRow["target_start_date"] = "2024-01-10 09:00";
        activityRow["phys_complete_pct"] = "35.0";

        // 3. Add a brand new activity
        var newRow = originalDoc.TaskTable.CreateRow();
        newRow["task_id"] = "999";
        newRow["proj_id"] = "1001";
        newRow["wbs_id"] = "2";
        newRow["task_code"] = "ENG1050";
        newRow["task_name"] = "Environmental Impact Study";
        newRow["target_durn_hr_cnt"] = "80.0";
        newRow["status_code"] = "TK_NotStart";

        // Serialize to string
        var xerText = XerWriter.WriteToString(originalDoc);

        // Parse back the serialized text
        var reloadedDoc = XerReader.ParseContent(xerText);

        // Verify integrity
        Assert.Equal(originalTableCount, reloadedDoc.Tables.Count);
        Assert.Equal(originalTaskCount + 1, reloadedDoc.TaskTable!.Rows.Count);

        // Verify project modification persisted
        Assert.Equal("Marina Tower Renovation 2026", reloadedDoc.PrimaryProject!["proj_short_name"]);

        // Verify activity modifications persisted
        var reloadedActivity = reloadedDoc.TaskTable.Rows.First(r => r["task_code"] == "ENG1010");
        Assert.Equal("Site Survey & Deep Scanning", reloadedActivity["task_name"]);
        Assert.Equal("2024-01-10 09:00", reloadedActivity["target_start_date"]);
        Assert.Equal("35.0", reloadedActivity["phys_complete_pct"]);

        // Verify newly added activity persisted
        var reloadedNewActivity = reloadedDoc.TaskTable.Rows.First(r => r["task_code"] == "ENG1050");
        Assert.Equal("999", reloadedNewActivity["task_id"]);
        Assert.Equal("Environmental Impact Study", reloadedNewActivity["task_name"]);
        Assert.Equal("80.0", reloadedNewActivity["target_durn_hr_cnt"]);
    }

    [Fact]
    public void FileIo_RoundTrip_WritesToDiskAndReadsBack()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"xer_test_{Guid.NewGuid()}.xer");
        try
        {
            var originalDoc = SampleXerGenerator.CreateSampleProject("File I/O Test Project");
            XerWriter.WriteFile(originalDoc, tempFile);

            Assert.True(File.Exists(tempFile));
            var reloadedDoc = XerReader.ReadFile(tempFile);

            Assert.Equal("File I/O Test Project", reloadedDoc.PrimaryProject!["proj_short_name"]);
            Assert.Equal(originalDoc.TaskTable!.Rows.Count, reloadedDoc.TaskTable!.Rows.Count);
            Assert.Equal(originalDoc.Tables.Count, reloadedDoc.Tables.Count);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void XerDocument_AddAndRemoveRows_WorksCorrectly()
    {
        var doc = SampleXerGenerator.CreateSampleProject("Crud Project");
        var taskTable = doc.TaskTable!;
        var initialCount = taskTable.Rows.Count;

        var rowToRemove = taskTable.Rows.First(r => r["task_code"] == "SUB1010");
        bool removed = taskTable.RemoveRow(rowToRemove);
        Assert.True(removed);
        Assert.Equal(initialCount - 1, taskTable.Rows.Count);
        Assert.DoesNotContain(taskTable.Rows, r => r["task_code"] == "SUB1010");
    }

    [Fact]
    public void XerDocument_HandlesMissingFields_Gracefully()
    {
        const string rawXer = "ERMHDR\t8.3\t2025-01-01\tPROJECT\n" +
                              "%T\tDEMO\n" +
                              "%F\tcol1\tcol2\tcol3\n" +
                              "%R\tval1\tval2\n" + // col3 is missing
                              "%E\n";

        var doc = XerReader.ParseContent(rawXer);
        var table = doc.GetTable("DEMO");
        Assert.NotNull(table);
        Assert.Single(table.Rows);
        Assert.Equal("val1", table.Rows[0]["col1"]);
        Assert.Equal("val2", table.Rows[0]["col2"]);
        Assert.Equal(string.Empty, table.Rows[0]["col3"]);
    }
}
