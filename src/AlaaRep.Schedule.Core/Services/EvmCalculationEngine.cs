using System;
using System.Collections.Generic;
using System.Linq;
using AlaaRep.Schedule.Core.Models;

namespace AlaaRep.Schedule.Core.Services
{
    public class EvmCalculationEngine
    {
        public ScheduleProjectSummary CalculateSummary(List<ActivityScheduleItem> activities, string? projectTitle = null, DateTime? dataDate = null)
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

            // Calculate Activity status counts
            summary.NotStartedCount = activities.Count(a => a.Status == ActivityStatus.NotStarted);
            summary.InProgressCount = activities.Count(a => a.Status == ActivityStatus.InProgress);
            summary.CompletedCount = activities.Count(a => a.Status == ActivityStatus.Completed);
            summary.DelayedCount = activities.Count(a => a.Status == ActivityStatus.Delayed);

            // Calculate Project-level dates
            var plannedStarts = activities.Where(a => a.PlannedStartDate.HasValue).Select(a => a.PlannedStartDate!.Value).ToList();
            var plannedFinishes = activities.Where(a => a.PlannedFinishDate.HasValue).Select(a => a.PlannedFinishDate!.Value).ToList();
            var forecastStarts = activities.Where(a => a.ForecastStartDate.HasValue).Select(a => a.ForecastStartDate!.Value).ToList();
            var forecastFinishes = activities.Where(a => a.ForecastFinishDate.HasValue).Select(a => a.ForecastFinishDate!.Value).ToList();

            if (plannedStarts.Any()) summary.ProjectPlannedStart = plannedStarts.Min();
            if (plannedFinishes.Any()) summary.ProjectPlannedFinish = plannedFinishes.Max();
            if (forecastStarts.Any()) summary.ProjectForecastStart = forecastStarts.Min();
            if (forecastFinishes.Any()) summary.ProjectForecastFinish = forecastFinishes.Max();

            // Total BAC
            summary.TotalBudgetAtCompletion = activities.Sum(a => a.BudgetAtCompletion);

            // Compute Weekly aggregates across all activities
            var allWeekLabels = activities
                .SelectMany(a => a.WeeklyValues)
                .Select(w => (w.WeekNumber, w.WeekLabel, w.WeekEndingDate))
                .Distinct()
                .OrderBy(w => w.WeekNumber)
                .ToList();

            double cumulativePv = 0;
            double cumulativeEv = 0;

            foreach (var week in allWeekLabels)
            {
                double weekPv = 0;
                double weekEv = 0;

                foreach (var act in activities)
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

            // Totals from weekly series if available, otherwise from activity level
            if (summary.WeeklySeries.Any())
            {
                var last = summary.WeeklySeries.Last();
                summary.TotalPlannedValue = last.CumulativePlannedValue;
                summary.TotalEarnedValue = last.CumulativeEarnedValue;
            }
            else
            {
                summary.TotalPlannedValue = Math.Round(activities.Sum(a => a.PlannedValueToDate), 2);
                summary.TotalEarnedValue = Math.Round(activities.Sum(a => a.EarnedValueToDate), 2);
            }

            // If BAC was 0 or not explicitly set per activity, BAC defaults to max of TotalPlannedValue or sum of activity BAC
            if (summary.TotalBudgetAtCompletion <= 0 && summary.TotalPlannedValue > 0)
            {
                summary.TotalBudgetAtCompletion = summary.TotalPlannedValue;
                foreach (var act in activities.Where(a => a.BudgetAtCompletion <= 0))
                {
                    var actPvSum = act.WeeklyValues.Sum(w => w.PlannedValue);
                    act.BudgetAtCompletion = actPvSum > 0 ? actPvSum : act.PlannedValueToDate;
                }
            }

            return summary;
        }
    }
}
