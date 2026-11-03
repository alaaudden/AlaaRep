using System;
using System.IO;
using System.Linq;
using AlaaRep.Schedule.Core.Models;
using AlaaRep.Schedule.Core.Services;
using ClosedXML.Excel;
using Xunit;

namespace AlaaRep.Schedule.Tests
{
    public class ExcelScheduleParserTests
    {
        [Fact]
        public void Parse_GeneratedSampleWorkbook_SucceedsWithExpectedMetrics()
        {
            var generator = new SampleScheduleGenerator();
            var parser = new ExcelScheduleParser();

            var bytes = generator.GenerateSampleWorkbookBytes();
            using var stream = new MemoryStream(bytes);

            var summary = parser.Parse(stream);

            Assert.NotNull(summary);
            Assert.Equal(7, summary.TotalActivities);
            Assert.True(summary.TotalBudgetAtCompletion > 0);
            Assert.True(summary.TotalPlannedValue > 0);
            Assert.True(summary.TotalEarnedValue > 0);
            Assert.NotEmpty(summary.WeeklySeries);
            Assert.Equal(6, summary.WeeklySeries.Count);

            // Verify first activity
            var act1 = summary.Activities.FirstOrDefault(a => a.ActivityId == "ACT-100");
            Assert.NotNull(act1);
            Assert.Equal("Mobilization & Site Prep", act1.ActivityName);
            Assert.Equal(50000.0, act1.BudgetAtCompletion);
            Assert.Equal(new DateTime(2026, 1, 5), act1.PlannedStartDate);
            Assert.Equal(new DateTime(2026, 1, 23), act1.PlannedFinishDate);
            Assert.Equal(new DateTime(2026, 1, 5), act1.ForecastStartDate);
            Assert.Equal(new DateTime(2026, 1, 22), act1.ForecastFinishDate);
            Assert.Equal(1, act1.FinishVarianceDays); // Forecast is 1 day earlier than planned
        }

        [Fact]
        public void Parse_CustomWorksheetWithVariedHeaders_MapsCorrectly()
        {
            using var stream = new MemoryStream();
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Engineering");
                ws.Cell(1, 1).Value = "Task ID";
                ws.Cell(1, 2).Value = "Description";
                ws.Cell(1, 3).Value = "Baseline Start";
                ws.Cell(1, 4).Value = "Baseline Finish";
                ws.Cell(1, 5).Value = "Actual Start";
                ws.Cell(1, 6).Value = "Actual Finish";
                ws.Cell(1, 7).Value = "Week 1 Planned";
                ws.Cell(1, 8).Value = "Week 1 Earned";
                ws.Cell(1, 9).Value = "Week 2 Planned";
                ws.Cell(1, 10).Value = "Week 2 Earned";

                ws.Cell(2, 1).Value = "ENG-01";
                ws.Cell(2, 2).Value = "Architectural Concept";
                ws.Cell(2, 3).Value = new DateTime(2026, 3, 1);
                ws.Cell(2, 4).Value = new DateTime(2026, 3, 15);
                ws.Cell(2, 5).Value = new DateTime(2026, 3, 1);
                ws.Cell(2, 6).Value = new DateTime(2026, 3, 20); // 5 days delayed
                ws.Cell(2, 7).Value = 1000;
                ws.Cell(2, 8).Value = 800;
                ws.Cell(2, 9).Value = 2000;
                ws.Cell(2, 10).Value = 1500;

                wb.SaveAs(stream);
            }

            stream.Position = 0;
            var parser = new ExcelScheduleParser();
            var summary = parser.Parse(stream);

            Assert.Single(summary.Activities);
            var act = summary.Activities[0];
            Assert.Equal("ENG-01", act.ActivityId);
            Assert.Equal("Architectural Concept", act.ActivityName);
            Assert.Equal(-5, act.FinishVarianceDays);
            Assert.Equal(3000, act.BudgetAtCompletion);
            Assert.Equal(2300, act.EarnedValueToDate);
            Assert.Equal(2, summary.WeeklySeries.Count);
            Assert.Equal(3000, summary.TotalPlannedValue);
            Assert.Equal(2300, summary.TotalEarnedValue);
            Assert.Equal(-700, summary.ScheduleVariance);
            Assert.Equal(0.767, summary.SchedulePerformanceIndex);
        }

        [Fact]
        public void Parse_InvalidWorksheet_ThrowsInformativeException()
        {
            using var stream = new MemoryStream();
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("RandomData");
                ws.Cell(1, 1).Value = "Country";
                ws.Cell(1, 2).Value = "Population";
                ws.Cell(2, 1).Value = "Canada";
                ws.Cell(2, 2).Value = 38000000;
                wb.SaveAs(stream);
            }

            stream.Position = 0;
            var parser = new ExcelScheduleParser();
            Assert.Throws<InvalidOperationException>(() => parser.Parse(stream));
        }
    }
}
