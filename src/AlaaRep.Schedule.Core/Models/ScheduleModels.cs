using System;
using System.Collections.Generic;

namespace AlaaRep.Schedule.Core.Models
{
    public class ActivityScheduleItem
    {
        public string ActivityId { get; set; } = string.Empty;
        public string ActivityName { get; set; } = string.Empty;
        public string WbsCode { get; set; } = string.Empty;
        public int Level { get; set; } = 0; // 0 = Project, 1 = Major Summary, 2 = WBS, 3 = Task
        public bool IsSummary { get; set; } = false;

        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedFinishDate { get; set; }
        public DateTime? ForecastStartDate { get; set; }
        public DateTime? ForecastFinishDate { get; set; }

        // Variance & percentages
        public int? VarianceDays { get; set; } // Schedule variance in days (Planned Finish - Forecast Finish)
        public double? SchedulePercent { get; set; } // Planned %
        public double? PerformancePercent { get; set; } // Earned/Actual %
        public bool IsCritical { get; set; } = false; // Critical Path activity (Total Float <= 0 or critical variance)

        // Cost-wise metrics
        public double BudgetAtCompletionCost { get; set; } // BAC Cost ($)
        public double EarnedCostToDate { get; set; } // EV Cost ($)
        public double PlannedCostToDate { get; set; } // PV Cost ($)

        // Unit-wise metrics (Labor units / Man-hours / Quantities)
        public double BudgetAtCompletionUnits { get; set; } // BAC Units
        public double EarnedUnitsToDate { get; set; } // EV Units
        public double PlannedUnitsToDate { get; set; } // PV Units

        // Backward compatibility properties for standard EVM
        public double BudgetAtCompletion
        {
            get => BudgetAtCompletionCost > 0 ? BudgetAtCompletionCost : BudgetAtCompletionUnits;
            set
            {
                if (BudgetAtCompletionCost == 0) BudgetAtCompletionCost = value;
                if (BudgetAtCompletionUnits == 0) BudgetAtCompletionUnits = value;
            }
        }

        public double EarnedValueToDate
        {
            get => EarnedCostToDate > 0 ? EarnedCostToDate : EarnedUnitsToDate;
            set
            {
                if (EarnedCostToDate == 0) EarnedCostToDate = value;
                if (EarnedUnitsToDate == 0) EarnedUnitsToDate = value;
            }
        }

        public double PlannedValueToDate
        {
            get => PlannedCostToDate > 0 ? PlannedCostToDate : PlannedUnitsToDate;
            set
            {
                if (PlannedCostToDate == 0) PlannedCostToDate = value;
                if (PlannedUnitsToDate == 0) PlannedUnitsToDate = value;
            }
        }

        public double PercentComplete
        {
            get
            {
                if (PerformancePercent.HasValue) return PerformancePercent.Value;
                if (BudgetAtCompletionUnits > 0)
                    return Math.Round((EarnedUnitsToDate / BudgetAtCompletionUnits) * 100.0, 2);
                if (BudgetAtCompletionCost > 0)
                    return Math.Round((EarnedCostToDate / BudgetAtCompletionCost) * 100.0, 2);
                return 0.0;
            }
        }

        public int? PlannedDurationDays => (PlannedStartDate.HasValue && PlannedFinishDate.HasValue)
            ? (int)(PlannedFinishDate.Value.Date - PlannedStartDate.Value.Date).TotalDays
            : null;

        public int? ForecastDurationDays => (ForecastStartDate.HasValue && ForecastFinishDate.HasValue)
            ? (int)(ForecastFinishDate.Value.Date - ForecastStartDate.Value.Date).TotalDays
            : null;

        public int? FinishVarianceDays => VarianceDays.HasValue
            ? VarianceDays.Value
            : ((PlannedFinishDate.HasValue && ForecastFinishDate.HasValue)
                ? (int)(PlannedFinishDate.Value.Date - ForecastFinishDate.Value.Date).TotalDays
                : null);

        public ActivityStatus Status
        {
            get
            {
                if (PercentComplete >= 100.0) return ActivityStatus.Completed;
                if (EarnedUnitsToDate > 0 || EarnedCostToDate > 0 || (PerformancePercent.HasValue && PerformancePercent.Value > 0))
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

        // Periodic value breakdown (can be monthly or weekly)
        public List<PeriodicValueItem> PeriodValues { get; set; } = new();

        // Weekly value breakdown maintained for backward compatibility
        public List<WeeklyValueItem> WeeklyValues { get; set; } = new();
    }

    public enum ActivityStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Delayed
    }

    public class PeriodicValueItem
    {
        public string PeriodLabel { get; set; } = string.Empty; // e.g. "1-Sep-26" or "Sep 2026"
        public DateTime? PeriodDate { get; set; }
        public int PeriodIndex { get; set; }
        public double PlannedUnits { get; set; }
        public double EarnedUnits { get; set; }
        public double PlannedCost { get; set; }
        public double EarnedCost { get; set; }

        // Backwards compatibility helpers
        public double PlannedValue => PlannedCost > 0 ? PlannedCost : PlannedUnits;
        public double EarnedValue => EarnedCost > 0 ? EarnedCost : EarnedUnits;
    }

    public class WeeklyValueItem
    {
        public string WeekLabel { get; set; } = string.Empty;
        public DateTime? WeekEndingDate { get; set; }
        public int WeekNumber { get; set; }
        public double PlannedValue { get; set; }
        public double EarnedValue { get; set; }
    }

    public class PeriodicAggregatePoint
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public DateTime? PeriodDate { get; set; }
        public int PeriodIndex { get; set; }

        // Unit-wise metrics
        public double PeriodicPlannedUnits { get; set; }
        public double PeriodicEarnedUnits { get; set; }
        public double CumulativePlannedUnits { get; set; }
        public double CumulativeEarnedUnits { get; set; }
        public double UnitsScheduleVariance => CumulativeEarnedUnits - CumulativePlannedUnits;
        public double UnitsSchedulePerformanceIndex => CumulativePlannedUnits > 0
            ? Math.Round(CumulativeEarnedUnits / CumulativePlannedUnits, 3)
            : 1.0;
        public double UnitsPercentComplete { get; set; }

        // Cost-wise metrics
        public double PeriodicPlannedCost { get; set; }
        public double PeriodicEarnedCost { get; set; }
        public double CumulativePlannedCost { get; set; }
        public double CumulativeEarnedCost { get; set; }
        public double CostScheduleVariance => CumulativeEarnedCost - CumulativePlannedCost;
        public double CostSchedulePerformanceIndex => CumulativePlannedCost > 0
            ? Math.Round(CumulativeEarnedCost / CumulativePlannedCost, 3)
            : 1.0;
        public double CostPercentComplete { get; set; }
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

    public class LookaheadScheduleItem
    {
        public string ActivityId { get; set; } = string.Empty;
        public string ActivityName { get; set; } = string.Empty;
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedFinishDate { get; set; }
        public DateTime? ForecastStartDate { get; set; }
        public DateTime? ForecastFinishDate { get; set; }
        public int? VarianceDays { get; set; }
        public double PercentComplete { get; set; }
        public double SchedulePercent { get; set; }

        public double TotalPlannedUnits { get; set; }
        public double EarnedUnits { get; set; }
        public double RemainingUnits => Math.Max(0, TotalPlannedUnits - EarnedUnits);

        public double TotalPlannedCost { get; set; }
        public double EarnedCost { get; set; }
        public double RemainingCost => Math.Max(0, TotalPlannedCost - EarnedCost);

        public double LookaheadPlannedUnits { get; set; }
        public double LookaheadPlannedCost { get; set; }

        public ActivityStatus Status { get; set; }
        public string LookaheadPhase { get; set; } = "Ongoing"; // Starting In Window, Ongoing, Finishing In Window
        public int DaysUntilForecastStart { get; set; }
        public int DaysUntilForecastFinish { get; set; }
        public bool IsDelayed => VarianceDays.HasValue && VarianceDays.Value < 0;
        public bool IsCritical { get; set; }
    }

    public class PeriodTaskItem
    {
        public string ActivityId { get; set; } = string.Empty;
        public string ActivityName { get; set; } = string.Empty;
        public string PeriodType { get; set; } = "LastPeriod"; // "LastPeriod" or "CurrentPeriod"
        public string PeriodLabel { get; set; } = string.Empty;
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedFinishDate { get; set; }
        public DateTime? ForecastStartDate { get; set; }
        public DateTime? ForecastFinishDate { get; set; }
        public int? VarianceDays { get; set; }
        public bool IsDelayed => (VarianceDays.HasValue && VarianceDays.Value < 0) || Status == ActivityStatus.Delayed;
        public bool IsCritical { get; set; }
        public double PlannedUnits { get; set; }
        public double EarnedUnits { get; set; }
        public double PlannedCost { get; set; }
        public double EarnedCost { get; set; }
        public double CumulativePercent { get; set; }
        public double SchedulePercent { get; set; }
        public ActivityStatus Status { get; set; }
        public string ActionRecommendation { get; set; } = string.Empty;
    }

    public class PeriodPerformanceSummary
    {
        public string LastPeriodLabel { get; set; } = string.Empty;
        public string CurrentPeriodLabel { get; set; } = string.Empty;

        // Last Period Accomplishment Aggregates
        public double LastPeriodPlannedUnits { get; set; }
        public double LastPeriodEarnedUnits { get; set; }
        public double LastPeriodUnitsVariance => LastPeriodEarnedUnits - LastPeriodPlannedUnits;
        public double LastPeriodUnitsSpi => LastPeriodPlannedUnits > 0
            ? Math.Round(LastPeriodEarnedUnits / LastPeriodPlannedUnits, 3)
            : 1.0;

        public double LastPeriodPlannedCost { get; set; }
        public double LastPeriodEarnedCost { get; set; }
        public double LastPeriodCostVariance => LastPeriodEarnedCost - LastPeriodPlannedCost;
        public double LastPeriodCostSpi => LastPeriodPlannedCost > 0
            ? Math.Round(LastPeriodEarnedCost / LastPeriodPlannedCost, 3)
            : 1.0;

        // Current Period Forecast Aggregates
        public double CurrentPeriodPlannedUnits { get; set; }
        public double CurrentPeriodPlannedCost { get; set; }

        public int DelayedTasksCount { get; set; }
        public int CriticalTasksCount { get; set; }
        public int LastPeriodActiveCount { get; set; }
        public int CurrentPeriodComingCount { get; set; }

        // Combined Tasks List
        public List<PeriodTaskItem> Tasks { get; set; } = new();
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

        // Unit-Wise Aggregates (Labor units)
        public double TotalUnitsBudgetAtCompletion { get; set; } // BAC Units
        public double TotalUnitsPlannedValue { get; set; } // Cumulative PV Units
        public double TotalUnitsEarnedValue { get; set; } // Cumulative EV Units
        public double UnitsScheduleVariance => TotalUnitsEarnedValue - TotalUnitsPlannedValue;
        public double UnitsScheduleVariancePercent => TotalUnitsPlannedValue > 0
            ? Math.Round((UnitsScheduleVariance / TotalUnitsPlannedValue) * 100.0, 2)
            : 0.0;
        public double UnitsSchedulePerformanceIndex => TotalUnitsPlannedValue > 0
            ? Math.Round(TotalUnitsEarnedValue / TotalUnitsPlannedValue, 3)
            : 1.0;
        public double OverallUnitsProgressPercent => TotalUnitsBudgetAtCompletion > 0
            ? Math.Round((TotalUnitsEarnedValue / TotalUnitsBudgetAtCompletion) * 100.0, 2)
            : 0.0;

        // Cost-Wise Aggregates ($)
        public double TotalCostBudgetAtCompletion { get; set; } // BAC Cost
        public double TotalCostPlannedValue { get; set; } // Cumulative PV Cost
        public double TotalCostEarnedValue { get; set; } // Cumulative EV Cost
        public double CostScheduleVariance => TotalCostEarnedValue - TotalCostPlannedValue;
        public double CostScheduleVariancePercent => TotalCostPlannedValue > 0
            ? Math.Round((CostScheduleVariance / TotalCostPlannedValue) * 100.0, 2)
            : 0.0;
        public double CostSchedulePerformanceIndex => TotalCostPlannedValue > 0
            ? Math.Round(TotalCostEarnedValue / TotalCostPlannedValue, 3)
            : 1.0;
        public double OverallCostProgressPercent => TotalCostBudgetAtCompletion > 0
            ? Math.Round((TotalCostEarnedValue / TotalCostBudgetAtCompletion) * 100.0, 2)
            : 0.0;

        // Backward compatibility properties
        public double TotalBudgetAtCompletion
        {
            get => TotalCostBudgetAtCompletion > 0 ? TotalCostBudgetAtCompletion : TotalUnitsBudgetAtCompletion;
            set { if (TotalCostBudgetAtCompletion == 0) TotalCostBudgetAtCompletion = value; }
        }
        public double TotalPlannedValue
        {
            get => TotalCostPlannedValue > 0 ? TotalCostPlannedValue : TotalUnitsPlannedValue;
            set { if (TotalCostPlannedValue == 0) TotalCostPlannedValue = value; }
        }
        public double TotalEarnedValue
        {
            get => TotalCostEarnedValue > 0 ? TotalCostEarnedValue : TotalUnitsEarnedValue;
            set { if (TotalCostEarnedValue == 0) TotalCostEarnedValue = value; }
        }
        public double ScheduleVariance => TotalEarnedValue - TotalPlannedValue;
        public double ScheduleVariancePercent => TotalPlannedValue > 0
            ? Math.Round((ScheduleVariance / TotalPlannedValue) * 100.0, 2)
            : 0.0;
        public double SchedulePerformanceIndex => TotalPlannedValue > 0
            ? Math.Round(TotalEarnedValue / TotalPlannedValue, 3)
            : 1.0;
        public double OverallProgressPercent => TotalBudgetAtCompletion > 0
            ? Math.Round((TotalEarnedValue / TotalBudgetAtCompletion) * 100.0, 2)
            : 0.0;

        // Dates & Variances
        public DateTime? ProjectPlannedStart { get; set; }
        public DateTime? ProjectPlannedFinish { get; set; }
        public DateTime? ProjectForecastStart { get; set; }
        public DateTime? ProjectForecastFinish { get; set; }
        public int ProjectVarianceDays => (ProjectPlannedFinish.HasValue && ProjectForecastFinish.HasValue)
            ? (int)(ProjectPlannedFinish.Value.Date - ProjectForecastFinish.Value.Date).TotalDays
            : 0;

        // Matrix & Time Series
        public List<string> TimelinePeriodHeaders { get; set; } = new();
        public List<PeriodicAggregatePoint> MonthlySeries { get; set; } = new();
        public List<WeeklyAggregatePoint> WeeklySeries { get; set; } = new();

        // Activities and Lookaheads
        public List<ActivityScheduleItem> Activities { get; set; } = new();
        public List<ActivityScheduleItem> DetailedActivities { get; set; } = new();
        public List<LookaheadScheduleItem> LookaheadActivities { get; set; } = new();

        // Period Summary: What happened last period combined with coming tasks in current period
        public PeriodPerformanceSummary PeriodSummary { get; set; } = new();
    }
}
