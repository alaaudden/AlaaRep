using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using AlaaRep.Xer.Models;

namespace AlaaRep.Xer.Entities;

public enum ScheduleItemType
{
    Wbs,
    Activity,
    Milestone
}

/// <summary>
/// Represents a hierarchical schedule row in the Primavera P6-style TreeGrid and Gantt view.
/// Can be either a WBS Summary Node or an Activity/Milestone Leaf Node.
/// </summary>
public class ScheduleItemNode : INotifyPropertyChanged
{
    private bool _isExpanded = true;
    private double _ganttLeft;
    private double _ganttWidth;
    private double _ganttProgressWidth;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ScheduleItemType ItemType { get; set; } = ScheduleItemType.Activity;

    public bool IsWbs => ItemType == ScheduleItemType.Wbs;
    public bool IsActivity => ItemType == ScheduleItemType.Activity;
    public bool IsMilestone => ItemType == ScheduleItemType.Milestone;

    public int IndentLevel { get; set; }

    /// <summary>
    /// Left margin for indentation in tree grid (e.g. IndentLevel * 18).
    /// </summary>
    public double IndentMargin => IndentLevel * 18.0;

    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WbsId { get; set; } = string.Empty;
    public string WbsCode { get; set; } = string.Empty;
    public string WbsName { get; set; } = string.Empty;

    public string StatusCode { get; set; } = "TK_NotStart";
    public string StatusDisplayName { get; set; } = "Not Started";

    public double DurationDays { get; set; }
    public double RemainingDays { get; set; }
    public double PercentComplete { get; set; }
    public double TotalFloatHours { get; set; }
    public bool IsCritical { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? EarlyStartDate { get; set; }
    public DateTime? EarlyEndDate { get; set; }
    public DateTime? LateStartDate { get; set; }
    public DateTime? LateEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    public string FormattedStartDate => StartDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";
    public string FormattedEndDate => EndDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";

    /// <summary>
    /// Reference to the underlying TaskActivity (if activity) or null (if WBS node).
    /// </summary>
    public TaskActivity? Activity { get; set; }

    /// <summary>
    /// Reference to the underlying raw XerRow.
    /// </summary>
    public XerRow? RawRow { get; set; }

    /// <summary>
    /// Parent WBS node, if any.
    /// </summary>
    public ScheduleItemNode? Parent { get; set; }

    /// <summary>
    /// Direct children (sub-WBS and tasks).
    /// </summary>
    public ObservableCollection<ScheduleItemNode> Children { get; } = new();

    public bool HasChildren => Children.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Visual position on Gantt Canvas (pixels from origin).
    /// </summary>
    public double GanttLeft
    {
        get => _ganttLeft;
        set
        {
            if (Math.Abs(_ganttLeft - value) > 0.01)
            {
                _ganttLeft = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Visual width of Gantt bar in pixels.
    /// </summary>
    public double GanttWidth
    {
        get => _ganttWidth;
        set
        {
            if (Math.Abs(_ganttWidth - value) > 0.01)
            {
                _ganttWidth = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Visual width of Progress portion in pixels.
    /// </summary>
    public double GanttProgressWidth
    {
        get => _ganttProgressWidth;
        set
        {
            if (Math.Abs(_ganttProgressWidth - value) > 0.01)
            {
                _ganttProgressWidth = value;
                OnPropertyChanged();
            }
        }
    }

    public void NotifyVisualsChanged()
    {
        OnPropertyChanged(nameof(FormattedStartDate));
        OnPropertyChanged(nameof(FormattedEndDate));
        OnPropertyChanged(nameof(DurationDays));
        OnPropertyChanged(nameof(PercentComplete));
        OnPropertyChanged(nameof(StatusDisplayName));
        OnPropertyChanged(nameof(IsCritical));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Code));
        OnPropertyChanged(nameof(IndentMargin));
        OnPropertyChanged(nameof(HasChildren));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
