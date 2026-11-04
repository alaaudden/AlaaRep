using System;
using System.IO;
using ClosedXML.Excel;

namespace AlaaRep.Schedule.Core.Services
{
    public class SampleScheduleGenerator
    {
        public byte[] GenerateSampleWorkbookBytes()
        {
            using var stream = new MemoryStream();
            using (var workbook = new XLWorkbook())
            {
                // =========================================================================
                // PAGE 1: Summary Layout (Used to produce histograms: monthly planned/earned values & S-Curves)
                // =========================================================================
                var wsSummary = workbook.Worksheets.Add("Summary Layout");

                var months = new[]
                {
                    "1-Sep-26", "1-Oct-26", "1-Nov-26", "1-Dec-26", "1-Jan-27", "1-Feb-27",
                    "1-Mar-27", "1-Apr-27", "1-May-27", "1-Jun-27", "1-Jul-27", "1-Aug-27",
                    "1-Sep-27", "1-Oct-27", "1-Nov-27", "1-Dec-27", "1-Jan-28"
                };

                // Column Headers for Summary sheet
                wsSummary.Cell(1, 1).Value = "Activity ID";
                wsSummary.Cell(1, 2).Value = "Activity Name";
                wsSummary.Cell(1, 3).Value = "Var (days)";
                wsSummary.Cell(1, 4).Value = "Schedule %";
                wsSummary.Cell(1, 5).Value = "Perform. %";
                wsSummary.Cell(1, 6).Value = "BAC Units";
                wsSummary.Cell(1, 7).Value = "BAC Cost ($)";
                wsSummary.Cell(1, 8).Value = "Planned Start";
                wsSummary.Cell(1, 9).Value = "Planned Finish";
                wsSummary.Cell(1, 10).Value = "Forecast Start";
                wsSummary.Cell(1, 11).Value = "Forecast Finish";
                wsSummary.Cell(1, 12).Value = "Value Type";

                int startCol = 13;
                for (int m = 0; m < months.Length; m++)
                {
                    wsSummary.Cell(1, startCol + m).Value = months[m];
                }

                // Summary items matching the exact image layout (with 2 rows per activity: Planned in blue, Earned in red)
                var summaryItems = new[]
                {
                    new {
                        Id = "SCH-001", Name = "Royat Al Ebtikar International School", Var = 0, Sched = 0.62, Perf = 0.61, BacUnits = 580000.0, Cost = 58000000.0,
                        Ps = "2026-09-01", Pf = "2028-01-30", Fs = "2026-09-01", Ff = "2028-01-30", Bold = true, Indent = 0,
                        PlannedValues = new (int idx, double val)[] { (0, 1790.5), (1, 1840.0), (2, 8586), (3, 22824), (4, 23593), (5, 25891), (6, 32833), (7, 41366), (8, 58671), (9, 60339), (10, 65500), (11, 66233), (12, 62206), (13, 46973), (14, 35643), (15, 24245), (16, 4294) },
                        EarnedValues = new (int idx, double val)[] { (0, 1244.0), (1, 1433.0) }
                    },
                    new {
                        Id = "MOB-100", Name = "Mobilization", Var = -25, Sched = 100.00, Perf = 90.00, BacUnits = 3581.0, Cost = 358100.0,
                        Ps = "2026-09-01", Pf = "2026-10-31", Fs = "2026-09-01", Ff = "2026-11-25", Bold = true, Indent = 1,
                        PlannedValues = new (int idx, double val)[] { (0, 1790.5), (1, 1640.0) },
                        EarnedValues = new (int idx, double val)[] { (0, 1222.0), (1, 1456.0) }
                    },
                    new {
                        Id = "CST-200", Name = "Construction", Var = 0, Sched = 0.01, Perf = 0.01, BacUnits = 576419.0, Cost = 57641900.0,
                        Ps = "2026-10-01", Pf = "2028-01-28", Fs = "2026-10-01", Ff = "2028-01-28", Bold = true, Indent = 1,
                        PlannedValues = new (int idx, double val)[] { (1, 49), (2, 7344), (3, 22276), (4, 23593), (5, 25891), (6, 32833), (7, 41366), (8, 58671), (9, 60339), (10, 65500), (11, 66233), (12, 62206), (13, 46973), (14, 34008), (15, 20302), (16, 2672) },
                        EarnedValues = new (int idx, double val)[] { (1, 49) }
                    },
                    new {
                        Id = "SCH-300", Name = "School", Var = 0, Sched = 0.01, Perf = 0.01, BacUnits = 576419.0, Cost = 57641900.0,
                        Ps = "2026-10-01", Pf = "2028-01-28", Fs = "2026-10-01", Ff = "2028-01-28", Bold = true, Indent = 2,
                        PlannedValues = new (int idx, double val)[] { (1, 49), (2, 6935), (3, 21149), (4, 20863), (5, 18538), (6, 24432), (7, 36668), (8, 52815), (9, 51143), (10, 55100), (11, 57983), (12, 51214), (13, 40056), (14, 31650), (15, 20301), (16, 2672) },
                        EarnedValues = new (int idx, double val)[] { (1, 49) }
                    },
                    new {
                        Id = "EW-400", Name = "Earth works", Var = 0, Sched = 0.65, Perf = 0.70, BacUnits = 4200.0, Cost = 420000.0,
                        Ps = "2026-10-01", Pf = "2026-12-31", Fs = "2026-10-01", Ff = "2026-12-31", Bold = true, Indent = 3,
                        PlannedValues = new (int idx, double val)[] { (1, 39), (2, 2121), (3, 2040) },
                        EarnedValues = new (int idx, double val)[] { (1, 39) }
                    },
                    new {
                        Id = "EW-401", Name = "Excavation for foundations", Var = 0, Sched = 15.79, Perf = 17.11, BacUnits = 228.0, Cost = 22800.0,
                        Ps = "2026-10-01", Pf = "2026-12-15", Fs = "2026-10-01", Ff = "2026-12-15", Bold = false, Indent = 4,
                        PlannedValues = new (int idx, double val)[] { (1, 41), (2, 111), (3, 76) },
                        EarnedValues = new (int idx, double val)[] { (1, 41) }
                    },
                    new {
                        Id = "EW-402", Name = "Soil Replacement", Var = 0, Sched = 0.0, Perf = 0.0, BacUnits = 220.0, Cost = 22000.0,
                        Ps = "2026-11-01", Pf = "2026-12-30", Fs = "2026-11-01", Ff = "2026-12-30", Bold = false, Indent = 4,
                        PlannedValues = new (int idx, double val)[] { (2, 70), (3, 150) },
                        EarnedValues = Array.Empty<(int idx, double val)>()
                    },
                    new {
                        Id = "EW-403", Name = "Backfill the excavated area", Var = 0, Sched = 0.0, Perf = 0.0, BacUnits = 5085.0, Cost = 508500.0,
                        Ps = "2026-11-15", Pf = "2027-02-28", Fs = "2026-11-15", Ff = "2027-02-28", Bold = false, Indent = 4,
                        PlannedValues = new (int idx, double val)[] { (2, 142), (3, 1581), (4, 2220), (5, 1142) },
                        EarnedValues = Array.Empty<(int idx, double val)>()
                    },
                    new {
                        Id = "RC-500", Name = "RC Concrete", Var = 0, Sched = 0.0, Perf = 0.0, BacUnits = 89907.0, Cost = 8990700.0,
                        Ps = "2026-11-15", Pf = "2027-06-30", Fs = "2026-11-15", Ff = "2027-06-30", Bold = false, Indent = 3,
                        PlannedValues = new (int idx, double val)[] { (2, 6136), (3, 17226), (4, 16119), (5, 16944), (6, 24082), (7, 25017), (8, 15812), (9, 1049) },
                        EarnedValues = Array.Empty<(int idx, double val)>()
                    },
                    new {
                        Id = "STL-510", Name = "Structural Steel Elements", Var = 0, Sched = 0.0, Perf = 0.0, BacUnits = 8617.0, Cost = 1292550.0,
                        Ps = "2027-04-01", Pf = "2027-07-31", Fs = "2027-04-01", Ff = "2027-07-31", Bold = false, Indent = 3,
                        PlannedValues = new (int idx, double val)[] { (7, 115), (8, 1799), (9, 5108), (10, 595) },
                        EarnedValues = Array.Empty<(int idx, double val)>()
                    },
                    new {
                        Id = "FIN-600", Name = "Rough finish", Var = 0, Sched = 0.0, Perf = 0.0, BacUnits = 100918.0, Cost = 10091800.0,
                        Ps = "2027-03-01", Pf = "2027-12-31", Fs = "2027-03-01", Ff = "2027-12-31", Bold = false, Indent = 3,
                        PlannedValues = new (int idx, double val)[] { (6, 351), (7, 9507), (8, 25115), (9, 25087), (10, 20657), (11, 16309), (12, 9748), (13, 5276), (14, 2646), (15, 89) },
                        EarnedValues = Array.Empty<(int idx, double val)>()
                    },
                    new {
                        Id = "FIN-610", Name = "Final finish", Var = 0, Sched = 0.0, Perf = 0.0, BacUnits = 70566.0, Cost = 8467920.0,
                        Ps = "2027-06-01", Pf = "2028-01-30", Fs = "2027-06-01", Ff = "2028-01-30", Bold = false, Indent = 3,
                        PlannedValues = new (int idx, double val)[] { (9, 1989), (10, 4829), (11, 9622), (12, 11853), (13, 10873), (14, 12713), (15, 9423), (16, 456) },
                        EarnedValues = Array.Empty<(int idx, double val)>()
                    },
                    new {
                        Id = "MEP-700", Name = "HVAC works", Var = 0, Sched = 0.0, Perf = 0.0, BacUnits = 18987.0, Cost = 2848050.0,
                        Ps = "2027-06-01", Pf = "2027-12-31", Fs = "2027-06-01", Ff = "2027-12-31", Bold = false, Indent = 3,
                        PlannedValues = new (int idx, double val)[] { (9, 1515), (10, 2667), (11, 3131), (12, 4903), (13, 5945), (14, 1718), (15, 367) },
                        EarnedValues = Array.Empty<(int idx, double val)>()
                    },
                    new {
                        Id = "MEP-710", Name = "Fire fighting", Var = 0, Sched = 0.0, Perf = 0.0, BacUnits = 8689.0, Cost = 1303350.0,
                        Ps = "2027-05-01", Pf = "2028-01-30", Fs = "2027-05-01", Ff = "2028-01-30", Bold = false, Indent = 3,
                        PlannedValues = new (int idx, double val)[] { (8, 729), (9, 2080), (10, 2335), (11, 1844), (12, 2448), (13, 745), (14, 802), (15, 223), (16, 9) },
                        EarnedValues = Array.Empty<(int idx, double val)>()
                    },
                    new {
                        Id = "MEP-720", Name = "Plumbing works", Var = 1, Sched = 0.0, Perf = 0.0, BacUnits = 11352.0, Cost = 1702800.0,
                        Ps = "2027-04-15", Pf = "2027-11-30", Fs = "2027-04-14", Ff = "2027-11-29", Bold = false, Indent = 3,
                        PlannedValues = new (int idx, double val)[] { (7, 400), (8, 1354), (9, 1697), (10, 2592), (11, 3029), (12, 1826), (13, 1048), (14, 199) },
                        EarnedValues = Array.Empty<(int idx, double val)>()
                    },
                    new {
                        Id = "MEP-730", Name = "Electrical works", Var = 0, Sched = 0.0, Perf = 0.0, BacUnits = 64455.0, Cost = 9668250.0,
                        Ps = "2026-11-01", Pf = "2027-12-31", Fs = "2026-11-01", Ff = "2027-12-31", Bold = false, Indent = 3,
                        PlannedValues = new (int idx, double val)[] { (2, 477), (3, 2116), (4, 2524), (5, 452), (7, 1629), (8, 4735), (9, 6900), (10, 12206), (11, 13231), (12, 8949), (13, 3982), (14, 7616), (15, 5359) },
                        EarnedValues = Array.Empty<(int idx, double val)>()
                    },
                    new {
                        Id = "TST-900", Name = "Testing & commissioning", Var = 0, Sched = 0.0, Perf = 0.0, BacUnits = 7200.0, Cost = 1080000.0,
                        Ps = "2027-11-01", Pf = "2028-01-30", Fs = "2027-11-01", Ff = "2028-01-30", Bold = true, Indent = 0,
                        PlannedValues = new (int idx, double val)[] { (14, 1635), (15, 3943), (16, 1622) },
                        EarnedValues = Array.Empty<(int idx, double val)>()
                    }
                };

                int sRow = 2;
                foreach (var r in summaryItems)
                {
                    // Row 1: Planned Value row
                    wsSummary.Cell(sRow, 1).Value = r.Id;
                    wsSummary.Cell(sRow, 2).Value = r.Name;
                    wsSummary.Cell(sRow, 2).Style.Alignment.Indent = r.Indent;
                    if (r.Bold) wsSummary.Cell(sRow, 2).Style.Font.Bold = true;

                    wsSummary.Cell(sRow, 3).Value = r.Var;
                    wsSummary.Cell(sRow, 4).Value = r.Sched;
                    wsSummary.Cell(sRow, 5).Value = r.Perf;
                    wsSummary.Cell(sRow, 6).Value = r.BacUnits;
                    wsSummary.Cell(sRow, 7).Value = r.Cost;
                    wsSummary.Cell(sRow, 8).Value = DateTime.Parse(r.Ps);
                    wsSummary.Cell(sRow, 9).Value = DateTime.Parse(r.Pf);
                    wsSummary.Cell(sRow, 10).Value = DateTime.Parse(r.Fs);
                    wsSummary.Cell(sRow, 11).Value = DateTime.Parse(r.Ff);

                    var pCell = wsSummary.Cell(sRow, 12);
                    pCell.Value = "Planned Value";
                    pCell.Style.Font.FontColor = XLColor.FromHtml("#1D4ED8");

                    foreach (var v in r.PlannedValues)
                    {
                        var c = wsSummary.Cell(sRow, startCol + v.idx);
                        c.Value = v.val;
                        c.Style.Font.FontColor = XLColor.FromHtml("#1E40AF");
                    }
                    sRow++;

                    // Row 2: Earned Value row
                    var eCell = wsSummary.Cell(sRow, 12);
                    eCell.Value = "Earned Value";
                    eCell.Style.Font.FontColor = XLColor.FromHtml("#DC2626");

                    foreach (var v in r.EarnedValues)
                    {
                        var c = wsSummary.Cell(sRow, startCol + v.idx);
                        c.Value = v.val;
                        c.Style.Font.FontColor = XLColor.FromHtml("#DC2626");
                    }
                    sRow++;
                }

                // Summary Sheet Header Styling
                wsSummary.Range(1, 1, 1, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF9C3"); // Light yellow
                wsSummary.Range(1, 6, 1, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
                wsSummary.Range(1, startCol, 1, startCol + months.Length - 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7"); // Light green
                wsSummary.Range(1, 1, 1, startCol + months.Length - 1).Style.Font.Bold = true;
                wsSummary.Columns().AdjustToContents();

                // =========================================================================
                // PAGE 2: Detailed Schedule - Primavera (Direct export format from Primavera P6)
                // Used to produce:
                // 1) Period Performance & Upcoming Tasks Summary
                // 2) Individual Lookahead Schedule Table
                // =========================================================================
                var wsDetailed = workbook.Worksheets.Add("Detailed Schedule - Primavera");

                wsDetailed.Cell(1, 1).Value = "Activity ID";
                wsDetailed.Cell(1, 2).Value = "Activity Name";
                wsDetailed.Cell(1, 3).Value = "WBS Code";
                wsDetailed.Cell(1, 4).Value = "Activity Status";
                wsDetailed.Cell(1, 5).Value = "Planned Start";
                wsDetailed.Cell(1, 6).Value = "Planned Finish";
                wsDetailed.Cell(1, 7).Value = "Forecast Start";
                wsDetailed.Cell(1, 8).Value = "Forecast Finish";
                wsDetailed.Cell(1, 9).Value = "Variance (days)";
                wsDetailed.Cell(1, 10).Value = "Critical";
                wsDetailed.Cell(1, 11).Value = "BAC Units";
                wsDetailed.Cell(1, 12).Value = "BAC Cost ($)";
                wsDetailed.Cell(1, 13).Value = "Planned Units";
                wsDetailed.Cell(1, 14).Value = "Earned Units";
                wsDetailed.Cell(1, 15).Value = "% Complete";
                wsDetailed.Cell(1, 16).Value = "Action / Notes";

                int dTimelineCol = 17;
                for (int m = 0; m < months.Length; m++)
                {
                    wsDetailed.Cell(1, dTimelineCol + m).Value = months[m];
                }

                var detailedItems = new[]
                {
                    // WBS Level 1
                    new { Id = "SCH.01", Name = "Royat Al Ebtikar International School", Wbs = "SCH", Status = "In Progress",
                          Ps = "2026-09-01", Pf = "2028-01-30", Fs = "2026-09-01", Ff = "2028-01-30", Var = 0, Crit = "Yes",
                          BacU = 580000.0, BacC = 58000000.0, Pu = 3630.5, Eu = 2677.0, Pct = 0.61, Note = "Overall Project Master", Indent = 0, Bold = true,
                          Timeline = new (int idx, double pVal, double eVal)[] { (0, 1790.5, 1244.0), (1, 1840.0, 1433.0) } },

                    // WBS Level 2 - Mobilization & Site Setup
                    new { Id = "MOB.01", Name = "Mobilization & Site Setup", Wbs = "SCH.01", Status = "In Progress",
                          Ps = "2026-09-01", Pf = "2026-10-31", Fs = "2026-09-01", Ff = "2026-11-25", Var = -25, Crit = "Yes",
                          BacU = 3581.0, BacC = 358100.0, Pu = 3430.5, Eu = 2678.0, Pct = 90.0, Note = "Delay in authority crane permit", Indent = 1, Bold = true,
                          Timeline = new (int idx, double pVal, double eVal)[] { (0, 1790.5, 1222.0), (1, 1640.0, 1456.0) } },
                    new { Id = "A1010", Name = "Temporary Site Offices & Fencing", Wbs = "SCH.01.01", Status = "Completed",
                          Ps = "2026-09-01", Pf = "2026-09-25", Fs = "2026-09-01", Ff = "2026-09-22", Var = 3, Crit = "No",
                          BacU = 1200.0, BacC = 120000.0, Pu = 1200.0, Eu = 1200.0, Pct = 100.0, Note = "Completed ahead of schedule", Indent = 2, Bold = false,
                          Timeline = new (int idx, double pVal, double eVal)[] { (0, 1200.0, 1200.0) } },
                    new { Id = "A1020", Name = "Water & Power Substation Hookup", Wbs = "SCH.01.01", Status = "In Progress",
                          Ps = "2026-09-10", Pf = "2026-10-25", Fs = "2026-09-10", Ff = "2026-11-15", Var = -21, Crit = "Yes",
                          BacU = 1381.0, BacC = 138100.0, Pu = 1381.0, Eu = 800.0, Pct = 57.9, Note = "Expedite municipality utility connection", Indent = 2, Bold = false,
                          Timeline = new (int idx, double pVal, double eVal)[] { (0, 590.5, 22.0), (1, 790.5, 778.0) } },
                    new { Id = "A1030", Name = "Heavy Machinery Logistics", Wbs = "SCH.01.01", Status = "In Progress",
                          Ps = "2026-10-01", Pf = "2026-10-31", Fs = "2026-10-01", Ff = "2026-11-10", Var = -10, Crit = "No",
                          BacU = 1000.0, BacC = 100000.0, Pu = 849.5, Eu = 678.0, Pct = 67.8, Note = "Excavators mobilized", Indent = 2, Bold = false,
                          Timeline = new (int idx, double pVal, double eVal)[] { (1, 849.5, 678.0) } },

                    // WBS Level 2 - Earth Works
                    new { Id = "EW.01", Name = "Earth Works & Substructure Excavation", Wbs = "SCH.02", Status = "In Progress",
                          Ps = "2026-10-01", Pf = "2026-12-31", Fs = "2026-10-01", Ff = "2026-12-31", Var = 0, Crit = "Yes",
                          BacU = 4200.0, BacC = 420000.0, Pu = 39.0, Eu = 39.0, Pct = 0.70, Note = "Starting mass excavation", Indent = 1, Bold = true,
                          Timeline = new (int idx, double pVal, double eVal)[] { (1, 39.0, 39.0), (2, 2121.0, 0), (3, 2040.0, 0) } },
                    new { Id = "A2010", Name = "Excavation for Foundations & Footings", Wbs = "SCH.02.01", Status = "In Progress",
                          Ps = "2026-10-01", Pf = "2026-12-15", Fs = "2026-10-01", Ff = "2026-12-15", Var = 0, Crit = "Yes",
                          BacU = 228.0, BacC = 22800.0, Pu = 41.0, Eu = 41.0, Pct = 17.11, Note = "Zone A excavation on track", Indent = 2, Bold = false,
                          Timeline = new (int idx, double pVal, double eVal)[] { (1, 41.0, 41.0), (2, 111.0, 0), (3, 76.0, 0) } },
                    new { Id = "A2020", Name = "Engineered Soil Replacement & Compaction", Wbs = "SCH.02.01", Status = "Not Started",
                          Ps = "2026-11-01", Pf = "2026-12-30", Fs = "2026-11-01", Ff = "2026-12-30", Var = 0, Crit = "No",
                          BacU = 220.0, BacC = 22000.0, Pu = 70.0, Eu = 0.0, Pct = 0.0, Note = "Target start in current period lookahead", Indent = 2, Bold = false,
                          Timeline = new (int idx, double pVal, double eVal)[] { (2, 70.0, 0), (3, 150.0, 0) } },
                    new { Id = "A2030", Name = "Backfill Foundation Perimeter", Wbs = "SCH.02.01", Status = "Not Started",
                          Ps = "2026-11-15", Pf = "2027-02-28", Fs = "2026-11-15", Ff = "2027-02-28", Var = 0, Crit = "No",
                          BacU = 5085.0, BacC = 508500.0, Pu = 142.0, Eu = 0.0, Pct = 0.0, Note = "Upcoming next month", Indent = 2, Bold = false,
                          Timeline = new (int idx, double pVal, double eVal)[] { (2, 142.0, 0), (3, 1581.0, 0) } },

                    // WBS Level 2 - Structural Works
                    new { Id = "RC.01", Name = "Reinforced Concrete Works", Wbs = "SCH.03", Status = "Not Started",
                          Ps = "2026-11-15", Pf = "2027-06-30", Fs = "2026-11-15", Ff = "2027-06-30", Var = 0, Crit = "Yes",
                          BacU = 89907.0, BacC = 8990700.0, Pu = 6136.0, Eu = 0.0, Pct = 0.0, Note = "Rebar procurement ongoing", Indent = 1, Bold = true,
                          Timeline = new (int idx, double pVal, double eVal)[] { (2, 6136.0, 0), (3, 17226.0, 0) } },
                    new { Id = "A3010", Name = "Foundation Concrete Pouring - Block A", Wbs = "SCH.03.01", Status = "Not Started",
                          Ps = "2026-11-15", Pf = "2026-12-20", Fs = "2026-11-15", Ff = "2026-12-20", Var = 0, Crit = "Yes",
                          BacU = 4500.0, BacC = 450000.0, Pu = 4500.0, Eu = 0.0, Pct = 0.0, Note = "Ready mix concrete batching approved", Indent = 2, Bold = false,
                          Timeline = new (int idx, double pVal, double eVal)[] { (2, 4500.0, 0) } },
                    new { Id = "A3020", Name = "Columns & Retaining Walls Casting", Wbs = "SCH.03.01", Status = "Not Started",
                          Ps = "2026-12-01", Pf = "2027-01-30", Fs = "2026-12-01", Ff = "2027-01-30", Var = 0, Crit = "Yes",
                          BacU = 12726.0, BacC = 1272600.0, Pu = 0.0, Eu = 0.0, Pct = 0.0, Note = "Formwork delivery scheduled", Indent = 2, Bold = false,
                          Timeline = new (int idx, double pVal, double eVal)[] { (3, 12726.0, 0) } },

                    // WBS Level 2 - MEP First Fix
                    new { Id = "MEP.01", Name = "MEP Rough-in & Underground Piping", Wbs = "SCH.04", Status = "Not Started",
                          Ps = "2026-11-01", Pf = "2027-12-31", Fs = "2026-11-01", Ff = "2027-12-31", Var = 0, Crit = "No",
                          BacU = 64455.0, BacC = 9668250.0, Pu = 477.0, Eu = 0.0, Pct = 0.0, Note = "Underground conduit coordination", Indent = 1, Bold = true,
                          Timeline = new (int idx, double pVal, double eVal)[] { (2, 477.0, 0), (3, 2116.0, 0) } },
                    new { Id = "A4010", Name = "Underground Drainage Sleeves & Earthing", Wbs = "SCH.04.01", Status = "Not Started",
                          Ps = "2026-11-01", Pf = "2026-11-30", Fs = "2026-11-01", Ff = "2026-11-30", Var = 0, Crit = "No",
                          BacU = 477.0, BacC = 71550.0, Pu = 477.0, Eu = 0.0, Pct = 0.0, Note = "Commencing in lookahead window", Indent = 2, Bold = false,
                          Timeline = new (int idx, double pVal, double eVal)[] { (2, 477.0, 0) } },
                    new { Id = "A4020", Name = "Substation Civil Embedments", Wbs = "SCH.04.01", Status = "Not Started",
                          Ps = "2026-12-01", Pf = "2026-12-31", Fs = "2026-12-01", Ff = "2026-12-31", Var = 0, Crit = "No",
                          BacU = 1100.0, BacC = 165000.0, Pu = 1100.0, Eu = 0.0, Pct = 0.0, Note = "Transformer room pad layout", Indent = 2, Bold = false,
                          Timeline = new (int idx, double pVal, double eVal)[] { (3, 1100.0, 0) } }
                };

                int dRow = 2;
                foreach (var act in detailedItems)
                {
                    wsDetailed.Cell(dRow, 1).Value = act.Id;
                    wsDetailed.Cell(dRow, 2).Value = act.Name;
                    wsDetailed.Cell(dRow, 2).Style.Alignment.Indent = act.Indent;
                    if (act.Bold) wsDetailed.Cell(dRow, 2).Style.Font.Bold = true;

                    wsDetailed.Cell(dRow, 3).Value = act.Wbs;
                    wsDetailed.Cell(dRow, 4).Value = act.Status;
                    wsDetailed.Cell(dRow, 5).Value = DateTime.Parse(act.Ps);
                    wsDetailed.Cell(dRow, 6).Value = DateTime.Parse(act.Pf);
                    wsDetailed.Cell(dRow, 7).Value = DateTime.Parse(act.Fs);
                    wsDetailed.Cell(dRow, 8).Value = DateTime.Parse(act.Ff);
                    wsDetailed.Cell(dRow, 9).Value = act.Var;
                    wsDetailed.Cell(dRow, 10).Value = act.Crit;
                    wsDetailed.Cell(dRow, 11).Value = act.BacU;
                    wsDetailed.Cell(dRow, 12).Value = act.BacC;
                    wsDetailed.Cell(dRow, 13).Value = act.Pu;
                    wsDetailed.Cell(dRow, 14).Value = act.Eu;
                    wsDetailed.Cell(dRow, 15).Value = act.Pct;
                    wsDetailed.Cell(dRow, 16).Value = act.Note;

                    foreach (var pt in act.Timeline)
                    {
                        var col = dTimelineCol + pt.idx;
                        wsDetailed.Cell(dRow, col).Value = pt.pVal; // Planned in cell
                    }

                    dRow++;
                }

                // Detailed Sheet Header Styling (Primavera P6 style header)
                wsDetailed.Range(1, 1, 1, 16).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
                wsDetailed.Range(1, 1, 1, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF9C3");
                wsDetailed.Range(1, dTimelineCol, 1, dTimelineCol + months.Length - 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7");
                wsDetailed.Range(1, 1, 1, dTimelineCol + months.Length - 1).Style.Font.Bold = true;
                wsDetailed.Columns().AdjustToContents();

                workbook.SaveAs(stream);
            }
            return stream.ToArray();
        }
    }
}
