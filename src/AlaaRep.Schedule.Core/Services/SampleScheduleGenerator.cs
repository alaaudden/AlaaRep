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
                var ws = workbook.Worksheets.Add("Royat Al Ebtikar School");

                // Monthly periods from 1-Sep-26 to 1-Jan-28 matching user image
                var months = new[]
                {
                    "1-Sep-26", "1-Oct-26", "1-Nov-26", "1-Dec-26", "1-Jan-27", "1-Feb-27",
                    "1-Mar-27", "1-Apr-27", "1-May-27", "1-Jun-27", "1-Jul-27", "1-Aug-27",
                    "1-Sep-27", "1-Oct-27", "1-Nov-27", "1-Dec-27", "1-Jan-28"
                };

                // Column Headers matching the updated template image
                ws.Cell(1, 1).Value = "Activity ID";
                ws.Cell(1, 2).Value = "Activity Name";
                ws.Cell(1, 3).Value = "Var (days)";
                ws.Cell(1, 4).Value = "Schedule %";
                ws.Cell(1, 5).Value = "Perform. %";
                ws.Cell(1, 6).Value = "BAC Units";
                ws.Cell(1, 7).Value = "BAC Cost ($)";
                ws.Cell(1, 8).Value = "Planned Start";
                ws.Cell(1, 9).Value = "Planned Finish";
                ws.Cell(1, 10).Value = "Forecast Start";
                ws.Cell(1, 11).Value = "Forecast Finish";
                ws.Cell(1, 12).Value = ""; // Value Type indicator column ("Planned Value" / "Earned Value")

                int startCol = 13;
                for (int m = 0; m < months.Length; m++)
                {
                    ws.Cell(1, startCol + m).Value = months[m];
                }

                // Activities from the image with Planned Value and Earned Value rows
                var items = new[]
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

                int row = 2;
                foreach (var r in items)
                {
                    // Row 1: Activity info + Planned Value
                    ws.Cell(row, 1).Value = r.Id;
                    ws.Cell(row, 2).Value = r.Name;
                    ws.Cell(row, 2).Style.Alignment.Indent = r.Indent;
                    if (r.Bold) ws.Cell(row, 2).Style.Font.Bold = true;

                    ws.Cell(row, 3).Value = r.Var;
                    ws.Cell(row, 4).Value = r.Sched;
                    ws.Cell(row, 5).Value = r.Perf;
                    ws.Cell(row, 6).Value = r.BacUnits;
                    ws.Cell(row, 7).Value = r.Cost;
                    ws.Cell(row, 8).Value = DateTime.Parse(r.Ps);
                    ws.Cell(row, 9).Value = DateTime.Parse(r.Pf);
                    ws.Cell(row, 10).Value = DateTime.Parse(r.Fs);
                    ws.Cell(row, 11).Value = DateTime.Parse(r.Ff);

                    // Row 1 metric label: Planned Value
                    var pCell = ws.Cell(row, 12);
                    pCell.Value = "Planned Value";
                    pCell.Style.Font.FontColor = XLColor.FromHtml("#1D4ED8"); // Blue

                    foreach (var v in r.PlannedValues)
                    {
                        var c = ws.Cell(row, startCol + v.idx);
                        c.Value = v.val;
                        c.Style.Font.FontColor = XLColor.FromHtml("#1E40AF"); // Blue font matching screenshot
                    }

                    row++;

                    // Row 2: Earned Value row
                    var eCell = ws.Cell(row, 12);
                    eCell.Value = "Earned Value";
                    eCell.Style.Font.FontColor = XLColor.FromHtml("#DC2626"); // Red

                    foreach (var v in r.EarnedValues)
                    {
                        var c = ws.Cell(row, startCol + v.idx);
                        c.Value = v.val;
                        c.Style.Font.FontColor = XLColor.FromHtml("#DC2626"); // Red font matching screenshot
                    }

                    row++;
                }

                // Styling
                var headerRange = ws.Range(1, 1, 1, startCol + months.Length - 1);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
                headerRange.Style.Font.FontColor = XLColor.Black;

                // Color header columns matching the image (yellow for id/var/sched, light green for timeline)
                ws.Range(1, 1, 1, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF9C3"); // Light yellow
                ws.Range(1, startCol, 1, startCol + months.Length - 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7"); // Light green

                ws.Columns().AdjustToContents();
                workbook.SaveAs(stream);
            }
            return stream.ToArray();
        }
    }
}
