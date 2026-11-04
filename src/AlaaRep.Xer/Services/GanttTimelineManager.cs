using System;
using System.Collections.Generic;
using System.Linq;
using AlaaRep.Xer.Entities;

namespace AlaaRep.Xer.Services;

public class GanttTimelineSegment
{
    public string HeaderText { get; set; } = string.Empty;
    public double Left { get; set; }
    public double Width { get; set; }
}

/// <summary>
/// Computes Gantt chart pixel coordinates, bar positions, and timeline date intervals.
/// </summary>
public class GanttTimelineManager
{
    public DateTime TimelineStart { get; private set; }
    public DateTime TimelineEnd { get; private set; }
    public double TotalWidth { get; private set; }
    public double PixelsPerDay { get; private set; } = 4.0; // Scalable zoom factor

    public List<GanttTimelineSegment> MajorSegments { get; } = new();
    public List<GanttTimelineSegment> MinorSegments { get; } = new();

    public void CalculateTimeline(IEnumerable<ScheduleItemNode> items, double pixelsPerDay = 4.0)
    {
        PixelsPerDay = Math.Max(1.0, pixelsPerDay);
        var itemList = items.ToList();

        DateTime minDate = DateTime.MaxValue;
        DateTime maxDate = DateTime.MinValue;

        foreach (var item in itemList)
        {
            if (item.StartDate.HasValue && item.StartDate.Value < minDate)
                minDate = item.StartDate.Value;
            if (item.EndDate.HasValue && item.EndDate.Value > maxDate)
                maxDate = item.EndDate.Value;
        }

        if (minDate == DateTime.MaxValue || maxDate == DateTime.MinValue || minDate >= maxDate)
        {
            minDate = DateTime.Today.AddDays(-7);
            maxDate = DateTime.Today.AddMonths(6);
        }

        // Add 14 days padding on each side for comfortable margin
        TimelineStart = new DateTime(minDate.Year, minDate.Month, 1).AddDays(-7);
        TimelineEnd = new DateTime(maxDate.Year, maxDate.Month, DateTime.DaysInMonth(maxDate.Year, maxDate.Month)).AddDays(14);

        var totalDays = Math.Max(1, (TimelineEnd - TimelineStart).TotalDays);
        TotalWidth = totalDays * PixelsPerDay;

        // Build Major (Year/Month) and Minor (Weeks / Month days) headers
        MajorSegments.Clear();
        MinorSegments.Clear();

        var cursor = TimelineStart;
        while (cursor < TimelineEnd)
        {
            var nextMonth = new DateTime(cursor.Year, cursor.Month, 1).AddMonths(1);
            if (nextMonth > TimelineEnd) nextMonth = TimelineEnd;

            var segDays = (nextMonth - cursor).TotalDays;
            var segWidth = segDays * PixelsPerDay;
            var segLeft = (cursor - TimelineStart).TotalDays * PixelsPerDay;

            MajorSegments.Add(new GanttTimelineSegment
            {
                HeaderText = cursor.ToString("MMM yyyy"),
                Left = segLeft,
                Width = segWidth
            });

            cursor = nextMonth;
        }

        // Update positions on every item
        foreach (var item in itemList)
        {
            UpdateItemGanttMetrics(item);
        }
    }

    public void UpdateItemGanttMetrics(ScheduleItemNode item)
    {
        if (!item.StartDate.HasValue || !item.EndDate.HasValue)
        {
            item.GanttLeft = 0;
            item.GanttWidth = 0;
            item.GanttProgressWidth = 0;
            return;
        }

        var startOffsetDays = (item.StartDate.Value - TimelineStart).TotalDays;
        var durationDays = Math.Max(0.5, (item.EndDate.Value - item.StartDate.Value).TotalDays);

        item.GanttLeft = Math.Max(0, startOffsetDays * PixelsPerDay);
        item.GanttWidth = Math.Max(item.IsMilestone ? 14 : 6, durationDays * PixelsPerDay);
        item.GanttProgressWidth = Math.Clamp(item.GanttWidth * (item.PercentComplete / 100.0), 0, item.GanttWidth);
    }
}
