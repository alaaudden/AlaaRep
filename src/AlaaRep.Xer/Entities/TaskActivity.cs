using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using AlaaRep.Xer.Models;

namespace AlaaRep.Xer.Entities;

/// <summary>
/// Strongly-typed wrapper around a TASK %R row in an XER document.
/// Provides rich property access and change notifications for WPF UI bindings.
/// </summary>
public class TaskActivity : INotifyPropertyChanged
{
    private readonly XerRow _row;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TaskActivity(XerRow row)
    {
        _row = row ?? throw new ArgumentNullException(nameof(row));
        _row.PropertyChanged += (s, e) =>
        {
            PropertyChanged?.Invoke(this, e);
        };
    }

    public XerRow UnderlyingRow => _row;

    public string TaskId
    {
        get => _row.GetValue("task_id");
        set => _row.SetValue("task_id", value);
    }

    public string TaskCode
    {
        get => _row.GetValue("task_code");
        set => _row.SetValue("task_code", value);
    }

    public string TaskName
    {
        get => _row.GetValue("task_name");
        set => _row.SetValue("task_name", value);
    }

    public string TaskType
    {
        get => _row.GetValue("task_type", "TT_Task");
        set => _row.SetValue("task_type", value);
    }

    /// <summary>
    /// P6 Status: TK_NotStart, TK_Active, TK_Complete
    /// </summary>
    public string StatusCode
    {
        get => _row.GetValue("status_code", "TK_NotStart");
        set => _row.SetValue("status_code", value);
    }

    public string StatusDisplayName
    {
        get => StatusCode switch
        {
            "TK_NotStart" => "Not Started",
            "TK_Active" => "In Progress",
            "TK_Complete" => "Completed",
            _ => StatusCode
        };
        set
        {
            StatusCode = value switch
            {
                "Not Started" => "TK_NotStart",
                "In Progress" => "TK_Active",
                "Completed" => "TK_Complete",
                _ => value
            };
            OnPropertyChanged();
        }
    }

    public string WbsId
    {
        get => _row.GetValue("wbs_id");
        set => _row.SetValue("wbs_id", value);
    }

    public double TargetDurationHours
    {
        get => ParseDouble(_row.GetValue("target_durn_hr_cnt"));
        set => _row.SetValue("target_durn_hr_cnt", value.ToString("0.0", CultureInfo.InvariantCulture));
    }

    public double TargetDurationDays
    {
        get => Math.Round(TargetDurationHours / 8.0, 2);
        set => TargetDurationHours = value * 8.0;
    }

    public double RemainingDurationHours
    {
        get => ParseDouble(_row.GetValue("remain_durn_hr_cnt"));
        set => _row.SetValue("remain_durn_hr_cnt", value.ToString("0.0", CultureInfo.InvariantCulture));
    }

    public double ActualDurationHours
    {
        get => ParseDouble(_row.GetValue("act_durn_hr_cnt"));
        set => _row.SetValue("act_durn_hr_cnt", value.ToString("0.0", CultureInfo.InvariantCulture));
    }

    public double PhysicalPercentComplete
    {
        get => ParseDouble(_row.GetValue("phys_complete_pct"));
        set => _row.SetValue("phys_complete_pct", Math.Clamp(value, 0.0, 100.0).ToString("0.0", CultureInfo.InvariantCulture));
    }

    public double TotalFloatHours
    {
        get => ParseDouble(_row.GetValue("total_float_hr_cnt"));
        set => _row.SetValue("total_float_hr_cnt", value.ToString("0.0", CultureInfo.InvariantCulture));
    }

    public string TargetStartDate
    {
        get => _row.GetValue("target_start_date");
        set => _row.SetValue("target_start_date", value);
    }

    public string TargetEndDate
    {
        get => _row.GetValue("target_end_date");
        set => _row.SetValue("target_end_date", value);
    }

    public string ActualStartDate
    {
        get => _row.GetValue("act_start_date");
        set => _row.SetValue("act_start_date", value);
    }

    public string ActualEndDate
    {
        get => _row.GetValue("act_end_date");
        set => _row.SetValue("act_end_date", value);
    }

    public string EarlyStartDate
    {
        get => _row.GetValue("early_start_date");
        set => _row.SetValue("early_start_date", value);
    }

    public string EarlyEndDate
    {
        get => _row.GetValue("early_end_date");
        set => _row.SetValue("early_end_date", value);
    }

    private static double ParseDouble(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0.0;
        return double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0.0;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
