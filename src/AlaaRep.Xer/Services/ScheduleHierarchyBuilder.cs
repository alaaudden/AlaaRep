using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlaaRep.Xer.Entities;
using AlaaRep.Xer.Models;

namespace AlaaRep.Xer.Services;

/// <summary>
/// Reconstructs the Primavera P6 hierarchical tree from PROJWBS and TASK tables,
/// performing date/duration rollups for parent WBS levels and calculating Gantt bar metrics.
/// </summary>
public static class ScheduleHierarchyBuilder
{
    private static readonly string[] DateFormats = new[]
    {
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "MM/dd/yyyy HH:mm",
        "dd/MM/yyyy HH:mm",
        "yyyy/MM/dd HH:mm"
    };

    public static List<ScheduleItemNode> BuildHierarchy(XerDocument document)
    {
        if (document == null) return new List<ScheduleItemNode>();

        var wbsTable = document.WbsTable;
        var taskTable = document.TaskTable;

        // 1. Build map of WBS nodes
        var wbsNodes = new Dictionary<string, ScheduleItemNode>(StringComparer.OrdinalIgnoreCase);

        if (wbsTable != null)
        {
            foreach (var row in wbsTable.Rows)
            {
                var wbsId = row.GetValue("wbs_id");
                if (string.IsNullOrWhiteSpace(wbsId)) continue;

                var node = new ScheduleItemNode
                {
                    ItemType = ScheduleItemType.Wbs,
                    Id = wbsId,
                    Code = row.GetValue("wbs_short_name"),
                    Name = row.GetValue("wbs_name"),
                    WbsId = wbsId,
                    WbsCode = row.GetValue("wbs_short_name"),
                    WbsName = row.GetValue("wbs_name"),
                    RawRow = row,
                    IsExpanded = true
                };

                wbsNodes[wbsId] = node;
            }
        }

        // Fallback root if no WBS exists
        if (wbsNodes.Count == 0)
        {
            var fallbackRoot = new ScheduleItemNode
            {
                ItemType = ScheduleItemType.Wbs,
                Id = "ROOT",
                Code = "PROJ",
                Name = document.PrimaryProject?.GetValue("proj_short_name", "Project Schedule") ?? "Project Schedule",
                IsExpanded = true
            };
            wbsNodes["ROOT"] = fallbackRoot;
        }

        // Link parent-child WBS
        var rootNodes = new List<ScheduleItemNode>();
        if (wbsTable != null)
        {
            foreach (var row in wbsTable.Rows)
            {
                var wbsId = row.GetValue("wbs_id");
                if (!wbsNodes.TryGetValue(wbsId, out var node)) continue;

                var parentId = row.GetValue("parent_wbs_id");
                if (!string.IsNullOrWhiteSpace(parentId) && wbsNodes.TryGetValue(parentId, out var parentNode))
                {
                    node.Parent = parentNode;
                    parentNode.Children.Add(node);
                }
                else
                {
                    rootNodes.Add(node);
                }
            }
        }
        else
        {
            rootNodes.AddRange(wbsNodes.Values);
        }

        // 2. Attach Activities to their WBS parent
        if (taskTable != null)
        {
            foreach (var row in taskTable.Rows)
            {
                var activity = new TaskActivity(row);
                var wbsId = activity.WbsId;

                ScheduleItemNode? parentWbs = null;
                if (!string.IsNullOrWhiteSpace(wbsId) && wbsNodes.TryGetValue(wbsId, out var foundWbs))
                {
                    parentWbs = foundWbs;
                }
                else
                {
                    parentWbs = rootNodes.FirstOrDefault() ?? wbsNodes.Values.FirstOrDefault();
                }

                bool isMilestone = activity.TaskType.Contains("Mile", StringComparison.OrdinalIgnoreCase) ||
                                  activity.TargetDurationHours <= 0;

                var taskNode = new ScheduleItemNode
                {
                    ItemType = isMilestone ? ScheduleItemType.Milestone : ScheduleItemType.Activity,
                    Id = activity.TaskId,
                    Code = activity.TaskCode,
                    Name = activity.TaskName,
                    WbsId = wbsId,
                    WbsCode = parentWbs?.Code ?? "",
                    WbsName = parentWbs?.Name ?? "",
                    StatusCode = activity.StatusCode,
                    StatusDisplayName = activity.StatusDisplayName,
                    DurationDays = activity.TargetDurationDays,
                    RemainingDays = Math.Round(activity.RemainingDurationHours / 8.0, 2),
                    PercentComplete = activity.PhysicalPercentComplete,
                    TotalFloatHours = activity.TotalFloatHours,
                    IsCritical = activity.TotalFloatHours <= 0 && activity.StatusCode != "TK_Complete",
                    StartDate = ParseDate(activity.TargetStartDate) ?? ParseDate(activity.EarlyStartDate),
                    EndDate = ParseDate(activity.TargetEndDate) ?? ParseDate(activity.EarlyEndDate),
                    ActualStartDate = ParseDate(activity.ActualStartDate),
                    ActualEndDate = ParseDate(activity.ActualEndDate),
                    EarlyStartDate = ParseDate(activity.EarlyStartDate),
                    EarlyEndDate = ParseDate(activity.EarlyEndDate),
                    Activity = activity,
                    RawRow = row,
                    Parent = parentWbs
                };

                parentWbs?.Children.Add(taskNode);
            }
        }

        // 3. Roll up summary dates and durations on WBS levels
        foreach (var root in rootNodes)
        {
            CalculateWbsRollups(root, 0);
        }

        return rootNodes;
    }

    /// <summary>
    /// Flattens the visible tree nodes based on IsExpanded states of parent nodes.
    /// </summary>
    public static List<ScheduleItemNode> FlattenVisibleTree(IEnumerable<ScheduleItemNode> rootNodes)
    {
        var result = new List<ScheduleItemNode>();
        foreach (var root in rootNodes)
        {
            FlattenRecursive(root, result);
        }
        return result;
    }

    private static void FlattenRecursive(ScheduleItemNode node, List<ScheduleItemNode> result)
    {
        result.Add(node);
        if (node.IsExpanded && node.HasChildren)
        {
            foreach (var child in node.Children)
            {
                FlattenRecursive(child, result);
            }
        }
    }

    private static void CalculateWbsRollups(ScheduleItemNode node, int level)
    {
        node.IndentLevel = level;

        if (node.ItemType != ScheduleItemType.Wbs)
            return;

        DateTime? minStart = null;
        DateTime? maxEnd = null;
        double totalWeightedProgress = 0.0;
        double totalDuration = 0.0;
        bool hasCriticalChild = false;

        foreach (var child in node.Children)
        {
            CalculateWbsRollups(child, level + 1);

            if (child.StartDate.HasValue)
            {
                if (!minStart.HasValue || child.StartDate.Value < minStart.Value)
                    minStart = child.StartDate.Value;
            }
            if (child.EndDate.HasValue)
            {
                if (!maxEnd.HasValue || child.EndDate.Value > maxEnd.Value)
                    maxEnd = child.EndDate.Value;
            }

            if (child.IsCritical) hasCriticalChild = true;

            var weight = child.DurationDays > 0 ? child.DurationDays : 1.0;
            totalDuration += weight;
            totalWeightedProgress += (child.PercentComplete * weight);
        }

        node.StartDate = minStart;
        node.EndDate = maxEnd;
        node.DurationDays = (minStart.HasValue && maxEnd.HasValue)
            ? Math.Max(1.0, Math.Round((maxEnd.Value - minStart.Value).TotalDays, 1))
            : Math.Round(totalDuration, 1);

        node.PercentComplete = totalDuration > 0
            ? Math.Round(totalWeightedProgress / totalDuration, 1)
            : 0.0;

        node.IsCritical = hasCriticalChild;
        node.StatusDisplayName = node.PercentComplete >= 100 ? "Completed" :
                                 node.PercentComplete > 0 ? "In Progress" : "Not Started";

        node.NotifyVisualsChanged();
    }

    public static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        foreach (var fmt in DateFormats)
        {
            if (DateTime.TryParseExact(dateStr.Trim(), fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt;
            }
        }
        if (DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var generalDt))
        {
            return generalDt;
        }
        return null;
    }
}
