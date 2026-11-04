using System;
using System.IO;
using System.Linq;
using AlaaRep.Xer.Entities;
using AlaaRep.Xer.Models;
using AlaaRep.Xer.Services;
using Xunit;

namespace AlaaRep.Xer.Tests;

public class ScheduleHierarchyAndGanttTests
{
    private static readonly string TestDataDir = Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public void BuildHierarchy_ReconstructsWbsTree_AndAssignsTaskChildren()
    {
        var filePath = Path.Combine(TestDataDir, "baseline_project.xer");
        var doc = XerReader.ReadFile(filePath);

        var roots = ScheduleHierarchyBuilder.BuildHierarchy(doc);

        Assert.NotEmpty(roots);
        var root = roots.First();
        Assert.True(root.IsWbs);
        Assert.Equal("MCT", root.Code);

        // Check children exist (Engineering, Substructure, Superstructure, etc.)
        Assert.NotEmpty(root.Children);

        // Check tasks are attached to proper WBS
        var allDescendants = ScheduleHierarchyBuilder.FlattenVisibleTree(roots);
        Assert.Contains(allDescendants, n => n.Code == "ENG1010" && n.ItemType == ScheduleItemType.Activity);
        Assert.Contains(allDescendants, n => n.Code == "M1000" && n.ItemType == ScheduleItemType.Milestone);
    }

    [Fact]
    public void BuildHierarchy_CalculatesWbsRollupDatesAndDurations()
    {
        var doc = SampleXerGenerator.CreateSampleProject("Rollup Test Project");
        var roots = ScheduleHierarchyBuilder.BuildHierarchy(doc);

        var root = roots.First();
        Assert.NotNull(root.StartDate);
        Assert.NotNull(root.EndDate);
        Assert.True(root.DurationDays > 0);
        Assert.True(root.PercentComplete >= 0.0);
    }

    [Fact]
    public void GanttTimelineManager_CalculatesBarOffsetsAndWidths()
    {
        var doc = SampleXerGenerator.CreateSampleProject("Gantt Metric Test");
        var roots = ScheduleHierarchyBuilder.BuildHierarchy(doc);
        var items = ScheduleHierarchyBuilder.FlattenVisibleTree(roots);

        var manager = new GanttTimelineManager();
        manager.CalculateTimeline(items, pixelsPerDay: 5.0);

        Assert.True(manager.TotalWidth > 0);
        Assert.NotEmpty(manager.MajorSegments);

        var task = items.First(n => n.Code == "ENG1010");
        Assert.True(task.GanttLeft >= 0);
        Assert.True(task.GanttWidth > 0);
        // Completed task should have progress width equal to Gantt width
        Assert.Equal(task.GanttWidth, task.GanttProgressWidth);
    }

    [Fact]
    public void BottomTabEdits_MutateUnderlyingXerRow_AndPersistOnRewrite()
    {
        var doc = SampleXerGenerator.CreateSampleProject("Tab Edit Test");
        var roots = ScheduleHierarchyBuilder.BuildHierarchy(doc);
        var items = ScheduleHierarchyBuilder.FlattenVisibleTree(roots);

        var item = items.First(n => n.Code == "ENG1020");
        Assert.NotNull(item.Activity);

        // Simulate property tab edit
        item.Activity.TaskName = "Structural Design Package - APPROVED";
        item.Activity.PhysicalPercentComplete = 100.0;
        item.Activity.StatusDisplayName = "Completed";
        item.Activity.TargetDurationDays = 25.0; // 200 hours

        // Check underlying row reflects changes
        var underlyingRow = item.Activity.UnderlyingRow;
        Assert.Equal("Structural Design Package - APPROVED", underlyingRow["task_name"]);
        Assert.Equal("100.0", underlyingRow["phys_complete_pct"]);
        Assert.Equal("TK_Complete", underlyingRow["status_code"]);
        Assert.Equal("200.0", underlyingRow["target_durn_hr_cnt"]);

        // Write and read back to confirm serialization
        var xerContent = XerWriter.WriteToString(doc);
        var reloadedDoc = XerReader.ParseContent(xerContent);

        var reloadedRow = reloadedDoc.TaskTable!.Rows.First(r => r["task_code"] == "ENG1020");
        Assert.Equal("Structural Design Package - APPROVED", reloadedRow["task_name"]);
        Assert.Equal("100.0", reloadedRow["phys_complete_pct"]);
        Assert.Equal("TK_Complete", reloadedRow["status_code"]);
        Assert.Equal("200.0", reloadedRow["target_durn_hr_cnt"]);
    }

    [Fact]
    public void TreeExpansion_Toggle_UpdatesVisibleFlattenedList()
    {
        var doc = SampleXerGenerator.CreateSampleProject("Toggle Expansion Test");
        var roots = ScheduleHierarchyBuilder.BuildHierarchy(doc);

        var initialCount = ScheduleHierarchyBuilder.FlattenVisibleTree(roots).Count;

        // Collapse root node
        roots.First().IsExpanded = false;
        var collapsedCount = ScheduleHierarchyBuilder.FlattenVisibleTree(roots).Count;

        Assert.Equal(1, collapsedCount);
        Assert.True(initialCount > collapsedCount);

        // Expand root node again
        roots.First().IsExpanded = true;
        var reExpandedCount = ScheduleHierarchyBuilder.FlattenVisibleTree(roots).Count;
        Assert.Equal(initialCount, reExpandedCount);
    }
}
