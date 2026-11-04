using System;
using System.Collections.Generic;
using AlaaRep.Schedule.Core.Models;
using AlaaRep.Schedule.Core.Services;
using Xunit;

namespace AlaaRep.Schedule.Tests
{
    public class EvmCalculationEngineTests
    {
        [Fact]
        public void CalculateSummary_ComputesCorrectCumulativeValuesAndSpi()
        {
            var engine = new EvmCalculationEngine();

            var act1 = new ActivityScheduleItem
            {
                ActivityId = "A1",
                ActivityName = "Design",
                BudgetAtCompletion = 100,
                PlannedStartDate = new DateTime(2026, 1, 1),
                PlannedFinishDate = new DateTime(2026, 1, 10),
                ForecastStartDate = new DateTime(2026, 1, 1),
                ForecastFinishDate = new DateTime(2026, 1, 12),
                WeeklyValues = new List<WeeklyValueItem>
                {
                    new() { WeekNumber = 1, WeekLabel = "W1", PlannedValue = 40, EarnedValue = 30 },
                    new() { WeekNumber = 2, WeekLabel = "W2", PlannedValue = 60, EarnedValue = 50 }
                }
            };

            var act2 = new ActivityScheduleItem
            {
                ActivityId = "A2",
                ActivityName = "Build",
                BudgetAtCompletion = 200,
                PlannedStartDate = new DateTime(2026, 1, 11),
                PlannedFinishDate = new DateTime(2026, 1, 25),
                ForecastStartDate = new DateTime(2026, 1, 11),
                ForecastFinishDate = new DateTime(2026, 1, 25),
                WeeklyValues = new List<WeeklyValueItem>
                {
                    new() { WeekNumber = 1, WeekLabel = "W1", PlannedValue = 10, EarnedValue = 10 },
                    new() { WeekNumber = 2, WeekLabel = "W2", PlannedValue = 50, EarnedValue = 40 }
                }
            };

            var summary = engine.CalculateSummary(new List<ActivityScheduleItem> { act1, act2 }, "Test Project");

            Assert.Equal("Test Project", summary.ProjectTitle);
            Assert.Equal(2, summary.TotalActivities);
            Assert.Equal(300, summary.TotalBudgetAtCompletion);

            Assert.Equal(2, summary.WeeklySeries.Count);
            Assert.Equal(50, summary.WeeklySeries[0].WeeklyPlannedValue);
            Assert.Equal(40, summary.WeeklySeries[0].WeeklyEarnedValue);
            Assert.Equal(50, summary.WeeklySeries[0].CumulativePlannedValue);
            Assert.Equal(40, summary.WeeklySeries[0].CumulativeEarnedValue);
            Assert.Equal(0.8, summary.WeeklySeries[0].SchedulePerformanceIndex);

            Assert.Equal(110, summary.WeeklySeries[1].WeeklyPlannedValue);
            Assert.Equal(90, summary.WeeklySeries[1].WeeklyEarnedValue);
            Assert.Equal(160, summary.WeeklySeries[1].CumulativePlannedValue);
            Assert.Equal(130, summary.WeeklySeries[1].CumulativeEarnedValue);

            Assert.Equal(160, summary.TotalPlannedValue);
            Assert.Equal(130, summary.TotalEarnedValue);
            Assert.Equal(-30, summary.ScheduleVariance);
            Assert.Equal(Math.Round(130.0 / 160.0, 3), summary.SchedulePerformanceIndex);
            Assert.Equal(new DateTime(2026, 1, 1), summary.ProjectPlannedStart);
            Assert.Equal(new DateTime(2026, 1, 25), summary.ProjectPlannedFinish);
        }

        [Fact]
        public void CalculateSummary_WithMonthlyCostAndUnits_CalculatesDualSeries()
        {
            var engine = new EvmCalculationEngine();

            var act1 = new ActivityScheduleItem
            {
                ActivityId = "ACT-1",
                ActivityName = "Earth Works",
                BudgetAtCompletionUnits = 1000,
                BudgetAtCompletionCost = 100000,
                PeriodValues = new List<PeriodicValueItem>
                {
                    new() { PeriodLabel = "1-Sep-26", PeriodDate = new DateTime(2026, 9, 1), PlannedUnits = 400, EarnedUnits = 400, PlannedCost = 40000, EarnedCost = 40000 },
                    new() { PeriodLabel = "1-Oct-26", PeriodDate = new DateTime(2026, 10, 1), PlannedUnits = 600, EarnedUnits = 500, PlannedCost = 60000, EarnedCost = 50000 }
                }
            };

            var summary = engine.CalculateSummary(new List<ActivityScheduleItem> { act1 }, "Dual Metric Test");

            Assert.Equal(2, summary.MonthlySeries.Count);

            // Unit wise checks
            Assert.Equal(1000, summary.MonthlySeries[1].CumulativePlannedUnits);
            Assert.Equal(900, summary.MonthlySeries[1].CumulativeEarnedUnits);
            Assert.Equal(-100, summary.UnitsScheduleVariance);
            Assert.Equal(0.9, summary.UnitsSchedulePerformanceIndex);

            // Cost wise checks
            Assert.Equal(100000, summary.MonthlySeries[1].CumulativePlannedCost);
            Assert.Equal(90000, summary.MonthlySeries[1].CumulativeEarnedCost);
            Assert.Equal(-10000, summary.CostScheduleVariance);
            Assert.Equal(0.9, summary.CostSchedulePerformanceIndex);
        }

        [Fact]
        public void CalculateLookahead_FiltersUpcomingActivitiesWithinHorizon()
        {
            var engine = new EvmCalculationEngine();
            var baseDate = new DateTime(2026, 10, 1);

            var actPast = new ActivityScheduleItem
            {
                ActivityId = "PAST-1",
                ActivityName = "Completed Mobilization",
                ForecastStartDate = new DateTime(2026, 9, 1),
                ForecastFinishDate = new DateTime(2026, 9, 25),
                EarnedUnitsToDate = 500,
                BudgetAtCompletionUnits = 500 // 100% complete
            };

            var actCurrent = new ActivityScheduleItem
            {
                ActivityId = "CUR-1",
                ActivityName = "Excavation",
                ForecastStartDate = new DateTime(2026, 10, 5),
                ForecastFinishDate = new DateTime(2026, 10, 20),
                EarnedUnitsToDate = 50,
                BudgetAtCompletionUnits = 200
            };

            var actFuture = new ActivityScheduleItem
            {
                ActivityId = "FUT-1",
                ActivityName = "Finishes",
                ForecastStartDate = new DateTime(2027, 5, 1),
                ForecastFinishDate = new DateTime(2027, 8, 1),
                BudgetAtCompletionUnits = 1000
            };

            var lookahead = engine.CalculateLookahead(new List<ActivityScheduleItem> { actPast, actCurrent, actFuture }, baseDate, lookaheadDays: 30);

            // Only actCurrent should be included in 30-day window from Oct 1
            Assert.Single(lookahead);
            Assert.Equal("CUR-1", lookahead[0].ActivityId);
            Assert.Equal("Entirely in Window", lookahead[0].LookaheadPhase);
            Assert.Equal(4, lookahead[0].DaysUntilForecastStart);
        }

        [Fact]
        public void CalculatePeriodPerformanceSummary_IdentifiesLastPeriodAndCurrentPeriodTasks()
        {
            var engine = new EvmCalculationEngine();

            var act1 = new ActivityScheduleItem
            {
                ActivityId = "A-MOB",
                ActivityName = "Mobilization",
                VarianceDays = -15, // Delayed
                BudgetAtCompletionUnits = 1000,
                EarnedUnitsToDate = 800,
                PeriodValues = new List<PeriodicValueItem>
                {
                    new() { PeriodLabel = "1-Sep-26", PeriodDate = new DateTime(2026, 9, 1), PlannedUnits = 500, EarnedUnits = 400 },
                    new() { PeriodLabel = "1-Oct-26", PeriodDate = new DateTime(2026, 10, 1), PlannedUnits = 500, EarnedUnits = 0 }
                }
            };

            var act2 = new ActivityScheduleItem
            {
                ActivityId = "A-FOUND",
                ActivityName = "Foundation Concrete",
                VarianceDays = 0,
                BudgetAtCompletionUnits = 2000,
                EarnedUnitsToDate = 0,
                PeriodValues = new List<PeriodicValueItem>
                {
                    new() { PeriodLabel = "1-Sep-26", PeriodDate = new DateTime(2026, 9, 1), PlannedUnits = 0, EarnedUnits = 0 },
                    new() { PeriodLabel = "1-Oct-26", PeriodDate = new DateTime(2026, 10, 1), PlannedUnits = 800, EarnedUnits = 0 }
                }
            };

            var summary = engine.CalculateSummary(new List<ActivityScheduleItem> { act1, act2 }, "Period Test");

            Assert.NotNull(summary.PeriodSummary);
            Assert.Equal("1-Sep-26", summary.PeriodSummary.LastPeriodLabel);
            Assert.Equal("1-Oct-26", summary.PeriodSummary.CurrentPeriodLabel);

            // Last period aggregates
            Assert.Equal(500, summary.PeriodSummary.LastPeriodPlannedUnits);
            Assert.Equal(400, summary.PeriodSummary.LastPeriodEarnedUnits);
            Assert.Equal(-100, summary.PeriodSummary.LastPeriodUnitsVariance);
            Assert.Equal(0.8, summary.PeriodSummary.LastPeriodUnitsSpi);

            // Current period aggregates
            Assert.Equal(1300, summary.PeriodSummary.CurrentPeriodPlannedUnits);

            // Tasks validation
            Assert.NotEmpty(summary.PeriodSummary.Tasks);
            var delayedTasks = summary.PeriodSummary.Tasks.FindAll(t => t.IsDelayed);
            Assert.NotEmpty(delayedTasks);
            Assert.Contains(delayedTasks, t => t.ActivityId == "A-MOB");

            var criticalTasks = summary.PeriodSummary.Tasks.FindAll(t => t.IsCritical);
            Assert.NotEmpty(criticalTasks);
        }

        [Fact]
        public void CalculateSummary_EmptyActivities_ReturnsSafeDefaults()
        {
            var engine = new EvmCalculationEngine();
            var summary = engine.CalculateSummary(new List<ActivityScheduleItem>());

            Assert.Equal(0, summary.TotalActivities);
            Assert.Equal(0, summary.TotalBudgetAtCompletion);
            Assert.Equal(0, summary.TotalPlannedValue);
            Assert.Equal(0, summary.TotalEarnedValue);
            Assert.Equal(1.0, summary.SchedulePerformanceIndex);
            Assert.Empty(summary.WeeklySeries);
        }
    }
}
