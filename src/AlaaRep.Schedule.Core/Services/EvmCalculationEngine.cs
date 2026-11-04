using System;
using System.Collections.Generic;
using System.Linq;
using AlaaRep.Schedule.Core.Models;

namespace AlaaRep.Schedule.Core.Services
{
    public class EvmCalculationEngine
    {
        public ScheduleProjectSummary CalculateSummary(List<ActivityScheduleItem> activities, string? projectTitle = null, DateTime? dataDate = null, int lookaheadDays = 30)
        {
            var summary = new ScheduleProjectSummary
            {
                ProjectTitle = projectTitle ?? "Project Time Schedule & EVM Dashboard",
                DataDate = dataDate,
                Activities = activities,
                TotalActivities = activities.Count
            };

            if (activities.Count == 0)
            {
                return summary;
            }

            // Calculate Activity status counts (exclude top-level project row if marked isSummary or parent)
            var leafActivities = activities.Where(a => !a.IsSummary).ToList();
            var targetActivities = leafActivities.Count > 0 ? leafActivities : activities;

            summary.NotStartedCount = targetActivities.Count(a => a.Status == ActivityStatus.NotStarted);
            summary.InProgressCount = targetActivities.Count(a => a.Status == ActivityStatus.InProgress);
            summary.CompletedCount = targetActivities.Count(a => a.Status == ActivityStatus.Completed);
            summary.DelayedCount = targetActivities.Count(a => a.Status == ActivityStatus.Delayed);

            // Calculate Project-level dates
            var plannedStarts = activities.Where(a => a.PlannedStartDate.HasValue).Select(a => a.PlannedStartDate!.Value).ToList();
            var plannedFinishes = activities.Where(a => a.PlannedFinishDate.HasValue).Select(a => a.PlannedFinishDate!.Value).ToList();
            var forecastStarts = activities.Where(a => a.ForecastStartDate.HasValue).Select(a => a.ForecastStartDate!.Value).ToList();
            var forecastFinishes = activities.Where(a => a.ForecastFinishDate.HasValue).Select(a => a.ForecastFinishDate!.Value).ToList();

            if (plannedStarts.Any()) summary.ProjectPlannedStart = plannedStarts.Min();
            if (plannedFinishes.Any()) summary.ProjectPlannedFinish = plannedFinishes.Max();
            if (forecastStarts.Any()) summary.ProjectForecastStart = forecastStarts.Min();
            if (forecastFinishes.Any()) summary.ProjectForecastFinish = forecastFinishes.Max();

            // Total BAC: Units and Cost
            summary.TotalUnitsBudgetAtCompletion = targetActivities.Sum(a => a.BudgetAtCompletionUnits);
            summary.TotalCostBudgetAtCompletion = targetActivities.Sum(a => a.BudgetAtCompletionCost);

            // Gather all period headers (dates/labels) across activities
            var allPeriodKeys = activities
                .SelectMany(a => a.PeriodValues)
                .GroupBy(p => p.PeriodLabel)
                .Select(g => g.OrderBy(x => x.PeriodDate ?? DateTime.MaxValue).First())
                .OrderBy(p => p.PeriodDate ?? DateTime.MaxValue)
                .ThenBy(p => p.PeriodIndex)
                .ToList();

            summary.TimelinePeriodHeaders = allPeriodKeys.Select(p => p.PeriodLabel).ToList();

            // Compute Periodic aggregates (Units & Cost)
            double cumPlannedUnits = 0;
            double cumEarnedUnits = 0;
            double cumPlannedCost = 0;
            double cumEarnedCost = 0;

            for (int i = 0; i < allPeriodKeys.Count; i++)
            {
                var periodKey = allPeriodKeys[i];
                double periodPvUnits = 0;
                double periodEvUnits = 0;
                double periodPvCost = 0;
                double periodEvCost = 0;

                foreach (var act in targetActivities)
                {
                    var match = act.PeriodValues.FirstOrDefault(p => p.PeriodLabel == periodKey.PeriodLabel);
                    if (match != null)
                    {
                        periodPvUnits += match.PlannedUnits;
                        periodEvUnits += match.EarnedUnits;
                        periodPvCost += match.PlannedCost;
                        periodEvCost += match.EarnedCost;
                    }
                }

                cumPlannedUnits += periodPvUnits;
                cumEarnedUnits += periodEvUnits;
                cumPlannedCost += periodPvCost;
                cumEarnedCost += periodEvCost;

                summary.MonthlySeries.Add(new PeriodicAggregatePoint
                {
                    PeriodIndex = i + 1,
                    PeriodLabel = periodKey.PeriodLabel,
                    PeriodDate = periodKey.PeriodDate,
                    PeriodicPlannedUnits = Math.Round(periodPvUnits, 2),
                    PeriodicEarnedUnits = Math.Round(periodEvUnits, 2),
                    CumulativePlannedUnits = Math.Round(cumPlannedUnits, 2),
                    CumulativeEarnedUnits = Math.Round(cumEarnedUnits, 2),
                    UnitsPercentComplete = summary.TotalUnitsBudgetAtCompletion > 0
                        ? Math.Round((cumEarnedUnits / summary.TotalUnitsBudgetAtCompletion) * 100.0, 2)
                        : 0,
                    PeriodicPlannedCost = Math.Round(periodPvCost, 2),
                    PeriodicEarnedCost = Math.Round(periodEvCost, 2),
                    CumulativePlannedCost = Math.Round(cumPlannedCost, 2),
                    CumulativeEarnedCost = Math.Round(cumEarnedCost, 2),
                    CostPercentComplete = summary.TotalCostBudgetAtCompletion > 0
                        ? Math.Round((cumEarnedCost / summary.TotalCostBudgetAtCompletion) * 100.0, 2)
                        : 0
                });
            }

            // Sync with WeeklySeries for backwards compatibility
            var allWeekLabels = activities
                .SelectMany(a => a.WeeklyValues)
                .Select(w => (w.WeekNumber, w.WeekLabel, w.WeekEndingDate))
                .Distinct()
                .OrderBy(w => w.WeekNumber)
                .ToList();

            if (allWeekLabels.Any())
            {
                double cumulativePv = 0;
                double cumulativeEv = 0;

                foreach (var week in allWeekLabels)
                {
                    double weekPv = 0;
                    double weekEv = 0;

                    foreach (var act in targetActivities)
                    {
                        var match = act.WeeklyValues.FirstOrDefault(w => w.WeekNumber == week.WeekNumber || w.WeekLabel == week.WeekLabel);
                        if (match != null)
                        {
                            weekPv += match.PlannedValue;
                            weekEv += match.EarnedValue;
                        }
                    }

                    cumulativePv += weekPv;
                    cumulativeEv += weekEv;

                    summary.WeeklySeries.Add(new WeeklyAggregatePoint
                    {
                        WeekNumber = week.WeekNumber,
                        WeekLabel = week.WeekLabel,
                        WeekEndingDate = week.WeekEndingDate,
                        WeeklyPlannedValue = Math.Round(weekPv, 2),
                        WeeklyEarnedValue = Math.Round(weekEv, 2),
                        CumulativePlannedValue = Math.Round(cumulativePv, 2),
                        CumulativeEarnedValue = Math.Round(cumulativeEv, 2)
                    });
                }
            }

            // Cumulative PV & EV totals
            if (summary.MonthlySeries.Any())
            {
                var lastPoint = summary.MonthlySeries.Last();
                // Find effective data date or last period with earned value
                var lastWithEv = summary.MonthlySeries.LastOrDefault(p => p.PeriodicEarnedUnits > 0 || p.PeriodicEarnedCost > 0);
                summary.TotalUnitsPlannedValue = lastWithEv != null ? lastWithEv.CumulativePlannedUnits : lastPoint.CumulativePlannedUnits;
                summary.TotalUnitsEarnedValue = lastWithEv != null ? lastWithEv.CumulativeEarnedUnits : lastPoint.CumulativeEarnedUnits;

                summary.TotalCostPlannedValue = lastWithEv != null ? lastWithEv.CumulativePlannedCost : lastPoint.CumulativePlannedCost;
                summary.TotalCostEarnedValue = lastWithEv != null ? lastWithEv.CumulativeEarnedCost : lastPoint.CumulativeEarnedCost;

                if (summary.TotalUnitsBudgetAtCompletion <= 0)
                {
                    summary.TotalUnitsBudgetAtCompletion = lastPoint.CumulativePlannedUnits;
                }
                if (summary.TotalCostBudgetAtCompletion <= 0)
                {
                    summary.TotalCostBudgetAtCompletion = lastPoint.CumulativePlannedCost;
                }
            }
            else if (summary.WeeklySeries.Any())
            {
                var last = summary.WeeklySeries.Last();
                summary.TotalCostPlannedValue = last.CumulativePlannedValue;
                summary.TotalCostEarnedValue = last.CumulativeEarnedValue;
                summary.TotalUnitsPlannedValue = last.CumulativePlannedValue;
                summary.TotalUnitsEarnedValue = last.CumulativeEarnedValue;
            }
            else
            {
                summary.TotalUnitsPlannedValue = Math.Round(targetActivities.Sum(a => a.PlannedUnitsToDate), 2);
                summary.TotalUnitsEarnedValue = Math.Round(targetActivities.Sum(a => a.EarnedUnitsToDate), 2);
                summary.TotalCostPlannedValue = Math.Round(targetActivities.Sum(a => a.PlannedCostToDate), 2);
                summary.TotalCostEarnedValue = Math.Round(targetActivities.Sum(a => a.EarnedCostToDate), 2);
            }

            // Determine critical path flags for activities
            IdentifyCriticalActivities(activities);

            // Calculate Lookahead Schedule
            summary.LookaheadActivities = CalculateLookahead(activities, summary.DataDate ?? DateTime.Today, lookaheadDays);

            // Calculate Period Performance Summary (Last Period + Current Period)
            summary.PeriodSummary = CalculatePeriodPerformanceSummary(activities, summary.MonthlySeries, summary.DataDate);

            return summary;
        }

        public void IdentifyCriticalActivities(List<ActivityScheduleItem> activities)
        {
            foreach (var act in activities)
            {
                // In standard P6 / construction management, an activity is critical if:
                // 1. It has negative variance (already delayed) or variance is 0 on the longest path.
                // 2. Or it is actively driving the project forecast completion.
                // Mark as critical if variance is negative (delayed) or zero with large weight/schedule impact
                if (act.VarianceDays.HasValue && act.VarianceDays.Value < 0)
                {
                    act.IsCritical = true;
                }
                else if (act.ActivityName.Contains("Foundation", StringComparison.OrdinalIgnoreCase) ||
                         act.ActivityName.Contains("Concrete", StringComparison.OrdinalIgnoreCase) ||
                         act.ActivityName.Contains("Structural", StringComparison.OrdinalIgnoreCase) ||
                         act.ActivityName.Contains("Mobilization", StringComparison.OrdinalIgnoreCase) ||
                         (act.VarianceDays.HasValue && act.VarianceDays.Value == 0 && act.Status == ActivityStatus.InProgress))
                {
                    act.IsCritical = true;
                }
            }
        }

        public PeriodPerformanceSummary CalculatePeriodPerformanceSummary(
            List<ActivityScheduleItem> activities,
            List<PeriodicAggregatePoint> monthlySeries,
            DateTime? dataDate)
        {
            var summary = new PeriodPerformanceSummary();
            var targetActivities = activities.Where(a => !a.IsSummary).ToList();
            if (!targetActivities.Any()) targetActivities = activities;

            if (monthlySeries == null || !monthlySeries.Any())
            {
                return summary;
            }

            // Find last period (the period leading up to or matching the latest earned value)
            int lastPeriodIndex = monthlySeries.FindLastIndex(m => m.PeriodicEarnedUnits > 0 || m.PeriodicEarnedCost > 0);
            if (lastPeriodIndex < 0) lastPeriodIndex = 0;

            int currentPeriodIndex = Math.Min(lastPeriodIndex + 1, monthlySeries.Count - 1);

            var lastPeriodPoint = monthlySeries[lastPeriodIndex];
            var currentPeriodPoint = monthlySeries[currentPeriodIndex];

            summary.LastPeriodLabel = lastPeriodPoint.PeriodLabel;
            summary.CurrentPeriodLabel = currentPeriodPoint.PeriodLabel;

            summary.LastPeriodPlannedUnits = lastPeriodPoint.PeriodicPlannedUnits;
            summary.LastPeriodEarnedUnits = lastPeriodPoint.PeriodicEarnedUnits;
            summary.LastPeriodPlannedCost = lastPeriodPoint.PeriodicPlannedCost;
            summary.LastPeriodEarnedCost = lastPeriodPoint.PeriodicEarnedCost;

            summary.CurrentPeriodPlannedUnits = currentPeriodPoint.PeriodicPlannedUnits;
            summary.CurrentPeriodPlannedCost = currentPeriodPoint.PeriodicPlannedCost;

            // Populate Last Period Tasks
            foreach (var act in targetActivities)
            {
                var periodMatch = act.PeriodValues.FirstOrDefault(p =>
                    string.Equals(p.PeriodLabel, lastPeriodPoint.PeriodLabel, StringComparison.OrdinalIgnoreCase) ||
                    (p.PeriodDate.HasValue && lastPeriodPoint.PeriodDate.HasValue && p.PeriodDate.Value.Month == lastPeriodPoint.PeriodDate.Value.Month && p.PeriodDate.Value.Year == lastPeriodPoint.PeriodDate.Value.Year));

                if (periodMatch != null && (periodMatch.PlannedUnits > 0 || periodMatch.EarnedUnits > 0 || periodMatch.PlannedCost > 0 || periodMatch.EarnedCost > 0 || act.Status == ActivityStatus.InProgress || act.Status == ActivityStatus.Delayed))
                {
                    bool isDelayed = (act.FinishVarianceDays.HasValue && act.FinishVarianceDays.Value < 0) || act.Status == ActivityStatus.Delayed;
                    string recommendation = isDelayed
                        ? "Expedite resources; review slippage causes"
                        : (act.Status == ActivityStatus.Completed ? "Completed on schedule" : "Progressing as planned");

                    summary.Tasks.Add(new PeriodTaskItem
                    {
                        ActivityId = act.ActivityId,
                        ActivityName = act.ActivityName,
                        PeriodType = "LastPeriod",
                        PeriodLabel = lastPeriodPoint.PeriodLabel,
                        PlannedStartDate = act.PlannedStartDate,
                        PlannedFinishDate = act.PlannedFinishDate,
                        ForecastStartDate = act.ForecastStartDate,
                        ForecastFinishDate = act.ForecastFinishDate,
                        VarianceDays = act.FinishVarianceDays,
                        IsCritical = act.IsCritical,
                        PlannedUnits = periodMatch.PlannedUnits,
                        EarnedUnits = periodMatch.EarnedUnits,
                        PlannedCost = periodMatch.PlannedCost,
                        EarnedCost = periodMatch.EarnedCost,
                        CumulativePercent = act.PercentComplete,
                        SchedulePercent = act.SchedulePercent ?? 0,
                        Status = act.Status,
                        ActionRecommendation = recommendation
                    });
                }
            }

            // Populate Current Period Tasks (What is scheduled in the coming period)
            foreach (var act in targetActivities)
            {
                var periodMatch = act.PeriodValues.FirstOrDefault(p =>
                    string.Equals(p.PeriodLabel, currentPeriodPoint.PeriodLabel, StringComparison.OrdinalIgnoreCase) ||
                    (p.PeriodDate.HasValue && currentPeriodPoint.PeriodDate.HasValue && p.PeriodDate.Value.Month == currentPeriodPoint.PeriodDate.Value.Month && p.PeriodDate.Value.Year == currentPeriodPoint.PeriodDate.Value.Year));

                if (periodMatch != null && (periodMatch.PlannedUnits > 0 || periodMatch.PlannedCost > 0 || act.Status == ActivityStatus.InProgress || act.Status == ActivityStatus.Delayed))
                {
                    bool isDelayed = (act.FinishVarianceDays.HasValue && act.FinishVarianceDays.Value < 0) || act.Status == ActivityStatus.Delayed;
                    string recommendation = isDelayed
                        ? "Critical attention: Delayed activity continuing into current period"
                        : (act.IsCritical ? "Critical path task: Monitor daily start & progress" : "Upcoming milestone task");

                    summary.Tasks.Add(new PeriodTaskItem
                    {
                        ActivityId = act.ActivityId,
                        ActivityName = act.ActivityName,
                        PeriodType = "CurrentPeriod",
                        PeriodLabel = currentPeriodPoint.PeriodLabel,
                        PlannedStartDate = act.PlannedStartDate,
                        PlannedFinishDate = act.PlannedFinishDate,
                        ForecastStartDate = act.ForecastStartDate,
                        ForecastFinishDate = act.ForecastFinishDate,
                        VarianceDays = act.FinishVarianceDays,
                        IsCritical = act.IsCritical,
                        PlannedUnits = periodMatch.PlannedUnits,
                        EarnedUnits = periodMatch.EarnedUnits,
                        PlannedCost = periodMatch.PlannedCost,
                        EarnedCost = periodMatch.EarnedCost,
                        CumulativePercent = act.PercentComplete,
                        SchedulePercent = act.SchedulePercent ?? 0,
                        Status = act.Status,
                        ActionRecommendation = recommendation
                    });
                }
            }

            summary.LastPeriodActiveCount = summary.Tasks.Count(t => t.PeriodType == "LastPeriod");
            summary.CurrentPeriodComingCount = summary.Tasks.Count(t => t.PeriodType == "CurrentPeriod");
            summary.DelayedTasksCount = summary.Tasks.Count(t => t.IsDelayed);
            summary.CriticalTasksCount = summary.Tasks.Count(t => t.IsCritical);

            return summary;
        }

        public List<LookaheadScheduleItem> CalculateLookahead(List<ActivityScheduleItem> activities, DateTime baseDate, int lookaheadDays = 30)
        {
            var lookaheadList = new List<LookaheadScheduleItem>();
            var windowEnd = baseDate.AddDays(lookaheadDays);

            var candidates = activities.Where(a => !a.IsSummary && a.Status != ActivityStatus.Completed).ToList();
            if (!candidates.Any())
            {
                candidates = activities.Where(a => a.Status != ActivityStatus.Completed).ToList();
            }

            foreach (var act in candidates)
            {
                var fStart = act.ForecastStartDate ?? act.PlannedStartDate ?? baseDate;
                var fFinish = act.ForecastFinishDate ?? act.PlannedFinishDate ?? baseDate.AddDays(7);

                // Activity is within lookahead if its forecast active window intersects [baseDate, windowEnd]
                // or if it was supposed to start/finish in the past and is still InProgress/Delayed
                bool inLookahead = (fStart <= windowEnd && fFinish >= baseDate) ||
                                   (act.Status == ActivityStatus.InProgress && fFinish >= baseDate) ||
                                   (act.Status == ActivityStatus.Delayed);

                if (inLookahead)
                {
                    string phase = "Ongoing";
                    if (fStart >= baseDate && fStart <= windowEnd && fFinish > windowEnd)
                        phase = "Starting in Window";
                    else if (fStart < baseDate && fFinish <= windowEnd)
                        phase = "Finishing in Window";
                    else if (fStart >= baseDate && fFinish <= windowEnd)
                        phase = "Entirely in Window";
                    else if (fStart < baseDate && fFinish > windowEnd)
                        phase = "Spanning Window";

                    // Compute lookahead planned units/cost proportional to days in window
                    double totalDays = Math.Max(1, (fFinish - fStart).TotalDays);
                    var windowActiveStart = fStart > baseDate ? fStart : baseDate;
                    var windowActiveEnd = fFinish < windowEnd ? fFinish : windowEnd;
                    double activeDaysInWindow = Math.Max(0, (windowActiveEnd - windowActiveStart).TotalDays);
                    double ratio = Math.Min(1.0, activeDaysInWindow / totalDays);

                    double actBacUnits = act.BudgetAtCompletionUnits > 0 ? act.BudgetAtCompletionUnits : act.BudgetAtCompletion;
                    double actBacCost = act.BudgetAtCompletionCost > 0 ? act.BudgetAtCompletionCost : act.BudgetAtCompletion;

                    lookaheadList.Add(new LookaheadScheduleItem
                    {
                        ActivityId = act.ActivityId,
                        ActivityName = act.ActivityName,
                        PlannedStartDate = act.PlannedStartDate,
                        PlannedFinishDate = act.PlannedFinishDate,
                        ForecastStartDate = act.ForecastStartDate,
                        ForecastFinishDate = act.ForecastFinishDate,
                        VarianceDays = act.FinishVarianceDays,
                        PercentComplete = act.PercentComplete,
                        SchedulePercent = act.SchedulePercent ?? 0,
                        TotalPlannedUnits = actBacUnits,
                        EarnedUnits = act.EarnedUnitsToDate > 0 ? act.EarnedUnitsToDate : act.EarnedValueToDate,
                        TotalPlannedCost = actBacCost,
                        EarnedCost = act.EarnedCostToDate > 0 ? act.EarnedCostToDate : act.EarnedValueToDate,
                        LookaheadPlannedUnits = Math.Round(actBacUnits * ratio, 1),
                        LookaheadPlannedCost = Math.Round(actBacCost * ratio, 2),
                        Status = act.Status,
                        LookaheadPhase = phase,
                        DaysUntilForecastStart = (int)(fStart - baseDate).TotalDays,
                        DaysUntilForecastFinish = (int)(fFinish - baseDate).TotalDays,
                        IsCritical = act.IsCritical
                    });
                }
            }

            return lookaheadList.OrderBy(x => x.ForecastStartDate ?? DateTime.MaxValue).ToList();
        }
    }
}
