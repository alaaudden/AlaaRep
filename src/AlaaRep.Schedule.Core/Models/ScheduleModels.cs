using System;
using System.Collections.Generic;

namespace AlaaRep.Schedule.Core.Models
{
    public class ActivityScheduleItem
    {
        public string ActivityId { get; set; } = string.Empty;
        public string ActivityName { get; set; } = string.Empty;
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedFinishDate { get; set; }
        public DateTime? ForecastStartDate { get; set; }
        public DateTime? ForecastFinishDate { get; set; }

        public double BudgetAtCompletion { get; set; } // BAC (Total planned value)
        public double EarnedValueToDate { get; set; } // Total EV recorded so far
        public double PlannedValueToDate { get; set; } // Total PV recorded so far

        public double PercentComplete => BudgetAtCompletion > 0
            ? Math.Round((EarnedValueToDate / BudgetAtCompletion) * 100.0, 2)
            : 0.0;

        public int? PlannedDurationDays => (PlannedStartDate.HasValue && PlannedFinishDate.HasValue)
            ? (int)(PlannedFinishDate.Value.Date - PlannedStartDate.Value.Date).TotalDays
            : null;

        public int? ForecastDurationDays => (ForecastStartDate.HasValue && ForecastFinishDate.HasValue)
            ? (int)(ForecastFinishDate.Value.Date - ForecastStartDate.Value.Date).TotalDays
            : null;

        // Schedule variance in days: Positive means finished/forecasted earlier than planned, negative means delayed
        public int? FinishVarianceDays => (PlannedFinishDate.HasValue && ForecastFinishDate.HasValue)
            ? (int)(PlannedFinishDate.Value.Date - ForecastFinishDate.Value.Date).TotalDays
            : null;

        public ActivityStatus Status
        {
            get
            {
                if (PercentComplete >= 100.0) return ActivityStatus.Completed;
                if (EarnedValueToDate > 0)
                {
                    if (FinishVarianceDays.HasValue && FinishVarianceDays.Value < 0)
                        return ActivityStatus.Delayed;
                    return ActivityStatus.InProgress;
                }
                if (ForecastFinishDate.HasValue && DateTime.Today > ForecastFinishDate.Value)
                    return ActivityStatus.Delayed;
                return ActivityStatus.NotStarted;
            }
        }

        // Weekly value breakdown for this activity: Week Key -> WeeklyValueItem
        public List<WeeklyValueItem> WeeklyValues { get; set; } = new();
    }

    public enum ActivityStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Delayed
    }

    public class WeeklyValueItem
    {
        public string WeekLabel { get; set; } = string.Empty;
        public DateTime? WeekEndingDate { get; set; }
        public int WeekNumber { get; set; }
        public double PlannedValue { get; set; }
        public double EarnedValue { get; set; }
    }

    public class WeeklyAggregatePoint
    {
        public string WeekLabel { get; set; } = string.Empty;
        public DateTime? WeekEndingDate { get; set; }
        public int WeekNumber { get; set; }
        public double WeeklyPlannedValue { get; set; }
        public double WeeklyEarnedValue { get; set; }
        public double CumulativePlannedValue { get; set; }
        public double CumulativeEarnedValue { get; set; }
        public double ScheduleVariance => CumulativeEarnedValue - CumulativePlannedValue;
        public double SchedulePerformanceIndex => CumulativePlannedValue > 0
            ? Math.Round(CumulativeEarnedValue / CumulativePlannedValue, 3)
            : 1.0;
    }

    public class ScheduleProjectSummary
    {
        public string ProjectTitle { get; set; } = "Schedule & Earned Value Update";
        public DateTime? DataDate { get; set; }
        public int TotalActivities { get; set; }
        public int NotStartedCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletedCount { get; set; }
        public int DelayedCount { get; set; }

        public double TotalBudgetAtCompletion { get; set; } // Total BAC
        public double TotalPlannedValue { get; set; } // Current Cumulative PV
        public double TotalEarnedValue { get; set; } // Current Cumulative EV
        public double ScheduleVariance => TotalEarnedValue - TotalPlannedValue; // SV = EV - PV
        public double ScheduleVariancePercent => TotalPlannedValue > 0
            ? Math.Round((ScheduleVariance / TotalPlannedValue) * 100.0, 2)
            : 0.0;
        public double SchedulePerformanceIndex => TotalPlannedValue > 0
            ? Math.Round(TotalEarnedValue / TotalPlannedValue, 3)
            : 1.0; // SPI = EV / PV

        public double OverallProgressPercent => TotalBudgetAtCompletion > 0
            ? Math.Round((TotalEarnedValue / TotalBudgetAtCompletion) * 100.0, 2)
            : 0.0;

        public DateTime? ProjectPlannedStart { get; set; }
        public DateTime? ProjectPlannedFinish { get; set; }
        public DateTime? ProjectForecastStart { get; set; }
        public DateTime? ProjectForecastFinish { get; set; }
        public int ProjectVarianceDays => (ProjectPlannedFinish.HasValue && ProjectForecastFinish.HasValue)
            ? (int)(ProjectPlannedFinish.Value.Date - ProjectForecastFinish.Value.Date).TotalDays
            : 0;

        public List<ActivityScheduleItem> Activities { get; set; } = new();
        public List<WeeklyAggregatePoint> WeeklySeries { get; set; } = new();
    }
}
