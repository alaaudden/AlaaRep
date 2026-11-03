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

            // W1 PV = 40 + 10 = 50; W1 EV = 30 + 10 = 40;
            // W2 PV = 60 + 50 = 110; W2 EV = 50 + 40 = 90;
            // Cumulative PV = 50 + 110 = 160; Cumulative EV = 40 + 90 = 130
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
