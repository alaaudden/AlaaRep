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
                var ws = workbook.Worksheets.Add("Schedule Update");

                // Headers
                ws.Cell(1, 1).Value = "Activity ID";
                ws.Cell(1, 2).Value = "Activity Name";
                ws.Cell(1, 3).Value = "Planned Start";
                ws.Cell(1, 4).Value = "Planned Finish";
                ws.Cell(1, 5).Value = "Forecast Start";
                ws.Cell(1, 6).Value = "Forecast Finish";
                ws.Cell(1, 7).Value = "BAC ($)";

                // Weekly PV & EV columns for 6 weeks
                int col = 8;
                for (int w = 1; w <= 6; w++)
                {
                    ws.Cell(1, col++).Value = $"W{w} PV";
                    ws.Cell(1, col++).Value = $"W{w} EV";
                }

                // Sample Activities
                var activities = new[]
                {
                    new {
                        Id = "ACT-100",
                        Name = "Mobilization & Site Prep",
                        PS = new DateTime(2026, 1, 5), PF = new DateTime(2026, 1, 23),
                        FS = new DateTime(2026, 1, 5), FF = new DateTime(2026, 1, 22),
                        Bac = 50000.0,
                        Weekly = new (double pv, double ev)[] { (20000, 20000), (20000, 22000), (10000, 8000), (0, 0), (0, 0), (0, 0) }
                    },
                    new {
                        Id = "ACT-110",
                        Name = "Substructure & Excavation",
                        PS = new DateTime(2026, 1, 19), PF = new DateTime(2026, 2, 13),
                        FS = new DateTime(2026, 1, 20), FF = new DateTime(2026, 2, 18),
                        Bac = 120000.0,
                        Weekly = new (double pv, double ev)[] { (0, 0), (25000, 20000), (35000, 30000), (35000, 30000), (25000, 22000), (0, 0) }
                    },
                    new {
                        Id = "ACT-120",
                        Name = "Reinforced Concrete Foundation",
                        PS = new DateTime(2026, 1, 26), PF = new DateTime(2026, 2, 20),
                        FS = new DateTime(2026, 1, 28), FF = new DateTime(2026, 2, 27),
                        Bac = 180000.0,
                        Weekly = new (double pv, double ev)[] { (0, 0), (0, 0), (45000, 40000), (55000, 48000), (50000, 42000), (30000, 25000) }
                    },
                    new {
                        Id = "ACT-130",
                        Name = "Structural Steel Erection",
                        PS = new DateTime(2026, 2, 9), PF = new DateTime(2026, 3, 6),
                        FS = new DateTime(2026, 2, 12), FF = new DateTime(2026, 3, 13),
                        Bac = 220000.0,
                        Weekly = new (double pv, double ev)[] { (0, 0), (0, 0), (0, 0), (40000, 35000), (70000, 65000), (60000, 50000) }
                    },
                    new {
                        Id = "ACT-140",
                        Name = "Exterior Envelope & Glazing",
                        PS = new DateTime(2026, 2, 23), PF = new DateTime(2026, 3, 20),
                        FS = new DateTime(2026, 2, 28), FF = new DateTime(2026, 3, 27),
                        Bac = 160000.0,
                        Weekly = new (double pv, double ev)[] { (0, 0), (0, 0), (0, 0), (0, 0), (30000, 20000), (50000, 40000) }
                    },
                    new {
                        Id = "ACT-150",
                        Name = "MEP Rough-in & Ductwork",
                        PS = new DateTime(2026, 2, 16), PF = new DateTime(2026, 3, 13),
                        FS = new DateTime(2026, 2, 16), FF = new DateTime(2026, 3, 13),
                        Bac = 140000.0,
                        Weekly = new (double pv, double ev)[] { (0, 0), (0, 0), (0, 0), (30000, 32000), (45000, 45000), (45000, 44000) }
                    },
                    new {
                        Id = "ACT-160",
                        Name = "Interior Partitions & Drywall",
                        PS = new DateTime(2026, 3, 2), PF = new DateTime(2026, 3, 27),
                        FS = new DateTime(2026, 3, 5), FF = new DateTime(2026, 4, 3),
                        Bac = 90000.0,
                        Weekly = new (double pv, double ev)[] { (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (20000, 15000) }
                    }
                };

                int row = 2;
                foreach (var a in activities)
                {
                    ws.Cell(row, 1).Value = a.Id;
                    ws.Cell(row, 2).Value = a.Name;
                    ws.Cell(row, 3).Value = a.PS;
                    ws.Cell(row, 4).Value = a.PF;
                    ws.Cell(row, 5).Value = a.FS;
                    ws.Cell(row, 6).Value = a.FF;
                    ws.Cell(row, 7).Value = a.Bac;

                    int c = 8;
                    foreach (var w in a.Weekly)
                    {
                        ws.Cell(row, c++).Value = w.pv;
                        ws.Cell(row, c++).Value = w.ev;
                    }
                    row++;
                }

                // Styling
                var headerRange = ws.Range(1, 1, 1, 7 + 12);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                headerRange.Style.Font.FontColor = XLColor.White;

                ws.Columns().AdjustToContents();
                workbook.SaveAs(stream);
            }
            return stream.ToArray();
        }
    }
}
