using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using AlaaRep.Schedule.Core.Models;

namespace AlaaRep.Schedule.Core.Services
{
    public class ExcelScheduleParser
    {
        private readonly EvmCalculationEngine _calculationEngine;

        public ExcelScheduleParser(EvmCalculationEngine? calculationEngine = null)
        {
            _calculationEngine = calculationEngine ?? new EvmCalculationEngine();
        }

        public ScheduleProjectSummary Parse(Stream excelStream, string? sheetName = null)
        {
            using var workbook = new XLWorkbook(excelStream);
            IXLWorksheet worksheet;

            if (!string.IsNullOrWhiteSpace(sheetName) && workbook.Worksheets.Contains(sheetName))
            {
                worksheet = workbook.Worksheet(sheetName);
            }
            else
            {
                worksheet = workbook.Worksheets.FirstOrDefault()
                    ?? throw new InvalidOperationException("Excel workbook does not contain any worksheets.");
            }

            return ParseWorksheet(worksheet);
        }

        public class TimelineColumnDef
        {
            public int ColumnIndex { get; set; }
            public string HeaderText { get; set; } = string.Empty;
            public DateTime? PeriodDate { get; set; }
            public bool IsCost { get; set; }
            public bool IsEarned { get; set; }
            public int WeekNumber { get; set; }
        }

        public ScheduleProjectSummary ParseWorksheet(IXLWorksheet worksheet)
        {
            var headerRowIndex = FindHeaderRow(worksheet);
            if (headerRowIndex == -1)
            {
                throw new InvalidOperationException("Could not detect header row with required schedule columns (Activity ID / Name).");
            }

            var headerRow = worksheet.Row(headerRowIndex);
            var (columnMap, timelineCols) = MapColumns(headerRow);

            if (!columnMap.ContainsKey("activity_id") && !columnMap.ContainsKey("activity_name"))
            {
                throw new InvalidOperationException("Missing mandatory columns: Activity ID or Activity Name column could not be identified.");
            }

            var activities = new List<ActivityScheduleItem>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRowIndex;

            DateTime? detectedDataDate = null;

            for (int r = headerRowIndex + 1; r <= lastRow; r++)
            {
                var row = worksheet.Row(r);
                if (row.IsEmpty()) continue;

                var id = columnMap.ContainsKey("activity_id") ? row.Cell(columnMap["activity_id"]).GetString().Trim() : string.Empty;
                var name = columnMap.ContainsKey("activity_name") ? row.Cell(columnMap["activity_name"]).GetString().Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name))
                    continue;

                // Detect indentation level or summary rows
                int indentLevel = 0;
                if (columnMap.ContainsKey("activity_id"))
                {
                    indentLevel = Math.Max(0, row.Cell(columnMap["activity_id"]).Style.Alignment.Indent);
                }
                if (indentLevel == 0 && columnMap.ContainsKey("activity_name"))
                {
                    indentLevel = Math.Max(0, row.Cell(columnMap["activity_name"]).Style.Alignment.Indent);
                }

                bool isBold = false;
                if (columnMap.ContainsKey("activity_id") && row.Cell(columnMap["activity_id"]).Style.Font.Bold)
                    isBold = true;
                if (columnMap.ContainsKey("activity_name") && row.Cell(columnMap["activity_name"]).Style.Font.Bold)
                    isBold = true;

                var activity = new ActivityScheduleItem
                {
                    ActivityId = string.IsNullOrWhiteSpace(id) ? (string.IsNullOrWhiteSpace(name) ? $"ACT-{r - headerRowIndex}" : name) : id,
                    ActivityName = string.IsNullOrWhiteSpace(name) ? id : name,
                    Level = indentLevel,
                    IsSummary = isBold && indentLevel <= 1
                };

                // Schedule variance days
                if (columnMap.TryGetValue("var_days", out int varCol))
                {
                    var val = TryGetInt(row.Cell(varCol));
                    if (val.HasValue) activity.VarianceDays = val.Value;
                }

                // Schedule %
                if (columnMap.TryGetValue("schedule_pct", out int schedCol))
                {
                    activity.SchedulePercent = TryGetPercent(row.Cell(schedCol));
                }

                // Performance %
                if (columnMap.TryGetValue("perform_pct", out int perfCol))
                {
                    activity.PerformancePercent = TryGetPercent(row.Cell(perfCol));
                }

                // Planned & Forecast dates
                if (columnMap.TryGetValue("planned_start", out int psCol))
                    activity.PlannedStartDate = TryGetDate(row.Cell(psCol));

                if (columnMap.TryGetValue("planned_finish", out int pfCol))
                    activity.PlannedFinishDate = TryGetDate(row.Cell(pfCol));

                if (columnMap.TryGetValue("forecast_start", out int fsCol))
                    activity.ForecastStartDate = TryGetDate(row.Cell(fsCol));

                if (columnMap.TryGetValue("forecast_finish", out int ffCol))
                    activity.ForecastFinishDate = TryGetDate(row.Cell(ffCol));

                // BAC, EV, PV Cost
                if (columnMap.TryGetValue("bac_cost", out int bCostCol))
                    activity.BudgetAtCompletionCost = TryGetDouble(row.Cell(bCostCol));
                else if (columnMap.TryGetValue("bac", out int bacCol))
                    activity.BudgetAtCompletionCost = TryGetDouble(row.Cell(bacCol));

                if (columnMap.TryGetValue("earned_cost", out int eCostCol))
                    activity.EarnedCostToDate = TryGetDouble(row.Cell(eCostCol));
                else if (columnMap.TryGetValue("earned_value", out int evCol))
                    activity.EarnedCostToDate = TryGetDouble(row.Cell(evCol));

                if (columnMap.TryGetValue("planned_cost", out int pCostCol))
                    activity.PlannedCostToDate = TryGetDouble(row.Cell(pCostCol));
                else if (columnMap.TryGetValue("planned_value", out int pvCol))
                    activity.PlannedCostToDate = TryGetDouble(row.Cell(pvCol));

                // BAC, EV, PV Units
                if (columnMap.TryGetValue("bac_units", out int bUnitsCol))
                    activity.BudgetAtCompletionUnits = TryGetDouble(row.Cell(bUnitsCol));

                if (columnMap.TryGetValue("earned_units", out int eUnitsCol))
                    activity.EarnedUnitsToDate = TryGetDouble(row.Cell(eUnitsCol));

                if (columnMap.TryGetValue("planned_units", out int pUnitsCol))
                    activity.PlannedUnitsToDate = TryGetDouble(row.Cell(pUnitsCol));

                // Parse Timeline Columns (e.g. 1-Sep-26, 1-Oct-26, etc.)
                int periodIndex = 1;
                foreach (var tCol in timelineCols)
                {
                    var cell = row.Cell(tCol.ColumnIndex);
                    double cellVal = TryGetDouble(cell);

                    // Look for existing period item or add new
                    var existing = activity.PeriodValues.FirstOrDefault(p => p.PeriodLabel == tCol.HeaderText);
                    if (existing == null)
                    {
                        existing = new PeriodicValueItem
                        {
                            PeriodIndex = periodIndex++,
                            PeriodLabel = tCol.HeaderText,
                            PeriodDate = tCol.PeriodDate
                        };
                        activity.PeriodValues.Add(existing);
                    }

                    if (tCol.IsCost)
                    {
                        if (tCol.IsEarned) existing.EarnedCost = cellVal;
                        else existing.PlannedCost = cellVal;
                    }
                    else
                    {
                        // Default to Units (or standard value)
                        if (tCol.IsEarned) existing.EarnedUnits = cellVal;
                        else existing.PlannedUnits = cellVal;
                    }

                    // Backwards compatible weekly item
                    if (tCol.WeekNumber > 0)
                    {
                        var wItem = activity.WeeklyValues.FirstOrDefault(w => w.WeekNumber == tCol.WeekNumber);
                        if (wItem == null)
                        {
                            wItem = new WeeklyValueItem
                            {
                                WeekNumber = tCol.WeekNumber,
                                WeekLabel = tCol.HeaderText,
                                WeekEndingDate = tCol.PeriodDate
                            };
                            activity.WeeklyValues.Add(wItem);
                        }
                        if (tCol.IsEarned) wItem.EarnedValue = cellVal;
                        else wItem.PlannedValue = cellVal;
                    }
                }

                // If monthly units or cost values exist, infer dates & BAC if missing
                if (activity.PeriodValues.Any())
                {
                    var activePeriods = activity.PeriodValues.Where(p => p.PlannedUnits > 0 || p.EarnedUnits > 0 || p.PlannedCost > 0 || p.EarnedCost > 0).ToList();
                    if (activePeriods.Any())
                    {
                        var firstDate = activePeriods.First().PeriodDate;
                        var lastDate = activePeriods.Last().PeriodDate;
                        if (!activity.PlannedStartDate.HasValue && firstDate.HasValue)
                            activity.PlannedStartDate = firstDate.Value;
                        if (!activity.PlannedFinishDate.HasValue && lastDate.HasValue)
                            activity.PlannedFinishDate = lastDate.Value.AddDays(28); // end of that month

                        if (!activity.ForecastStartDate.HasValue) activity.ForecastStartDate = activity.PlannedStartDate;
                        if (!activity.ForecastFinishDate.HasValue)
                        {
                            int shift = activity.VarianceDays.HasValue ? -activity.VarianceDays.Value : 0;
                            activity.ForecastFinishDate = activity.PlannedFinishDate?.AddDays(shift);
                        }
                    }

                    var totalPUnits = activity.PeriodValues.Sum(p => p.PlannedUnits);
                    var totalEUnits = activity.PeriodValues.Sum(p => p.EarnedUnits);
                    var totalPCost = activity.PeriodValues.Sum(p => p.PlannedCost);
                    var totalECost = activity.PeriodValues.Sum(p => p.EarnedCost);

                    if (activity.BudgetAtCompletionUnits <= 0 && totalPUnits > 0)
                        activity.BudgetAtCompletionUnits = totalPUnits;

                    if (activity.EarnedUnitsToDate <= 0 && totalEUnits > 0)
                        activity.EarnedUnitsToDate = totalEUnits;

                    if (activity.PlannedUnitsToDate <= 0 && totalPUnits > 0)
                        activity.PlannedUnitsToDate = totalPUnits;

                    if (activity.BudgetAtCompletionCost <= 0 && totalPCost > 0)
                        activity.BudgetAtCompletionCost = totalPCost;

                    if (activity.EarnedCostToDate <= 0 && totalECost > 0)
                        activity.EarnedCostToDate = totalECost;

                    if (activity.PlannedCostToDate <= 0 && totalPCost > 0)
                        activity.PlannedCostToDate = totalPCost;

                    // If cost values were not specified separately, mirror units 1:1 unless cost was explicitly supplied
                    if (activity.BudgetAtCompletionCost <= 0 && activity.BudgetAtCompletionUnits > 0)
                    {
                        activity.BudgetAtCompletionCost = activity.BudgetAtCompletionUnits;
                        activity.PlannedCostToDate = activity.PlannedUnitsToDate;
                        activity.EarnedCostToDate = activity.EarnedUnitsToDate;

                        foreach (var p in activity.PeriodValues)
                        {
                            if (p.PlannedCost == 0) p.PlannedCost = p.PlannedUnits;
                            if (p.EarnedCost == 0) p.EarnedCost = p.EarnedUnits;
                        }
                    }
                    else if (activity.BudgetAtCompletionUnits <= 0 && activity.BudgetAtCompletionCost > 0)
                    {
                        activity.BudgetAtCompletionUnits = activity.BudgetAtCompletionCost;
                        activity.PlannedUnitsToDate = activity.PlannedCostToDate;
                        activity.EarnedUnitsToDate = activity.EarnedCostToDate;

                        foreach (var p in activity.PeriodValues)
                        {
                            if (p.PlannedUnits == 0) p.PlannedUnits = p.PlannedCost;
                            if (p.EarnedUnits == 0) p.EarnedUnits = p.EarnedCost;
                        }
                    }
                }

                activities.Add(activity);
            }

            // Estimate Data Date from periods that have earned values
            var earnedDates = activities.SelectMany(a => a.PeriodValues)
                .Where(p => (p.EarnedUnits > 0 || p.EarnedCost > 0) && p.PeriodDate.HasValue)
                .Select(p => p.PeriodDate!.Value)
                .ToList();

            if (earnedDates.Any())
            {
                detectedDataDate = earnedDates.Max().AddDays(15);
            }
            else
            {
                detectedDataDate = DateTime.Today;
            }

            var projectName = worksheet.Name;
            var summary = _calculationEngine.CalculateSummary(activities, projectName, detectedDataDate);
            return summary;
        }

        private int FindHeaderRow(IXLWorksheet worksheet)
        {
            var maxScan = Math.Min(15, worksheet.LastRowUsed()?.RowNumber() ?? 1);
            for (int r = 1; r <= maxScan; r++)
            {
                var row = worksheet.Row(r);
                var text = string.Join(" ", row.CellsUsed().Select(c => c.GetString().Trim().ToLowerInvariant()));
                if ((text.Contains("activity") || text.Contains("act") || text.Contains("task") || text.Contains("wbs") || text.Contains("var") || text.Contains("schedule")) &&
                    (text.Contains("id") || text.Contains("plan") || text.Contains("start") || text.Contains("date") || text.Contains("sep") || text.Contains("oct") || text.Contains("var") || text.Contains("%")))
                {
                    return r;
                }
            }
            return 1;
        }

        private (Dictionary<string, int> columnMap, List<TimelineColumnDef> timelineCols) MapColumns(IXLRow headerRow)
        {
            var map = new Dictionary<string, int>();
            var timelineCols = new List<TimelineColumnDef>();

            foreach (var cell in headerRow.CellsUsed())
            {
                var text = cell.GetString().Trim();
                var norm = text.ToLowerInvariant().Replace("_", " ").Replace("-", " ");
                var col = cell.Address.ColumnNumber;

                if (Matches(norm, "activity id", "act id", "task id", "code", "wbs id", "activity code", "id"))
                {
                    if (!map.ContainsKey("activity_id")) map["activity_id"] = col;
                }
                else if (Matches(norm, "activity name", "activity description", "task name", "description", "name", "activity"))
                {
                    if (!map.ContainsKey("activity_name")) map["activity_name"] = col;
                }
                else if (norm.Contains("var") || norm.Contains("variance"))
                {
                    map["var_days"] = col;
                }
                else if (Matches(norm, "schedule %", "schedule pct", "sched %", "plan %", "planned %"))
                {
                    map["schedule_pct"] = col;
                }
                else if (Matches(norm, "perform. %", "perform %", "performance %", "act %", "actual %", "earned %"))
                {
                    map["perform_pct"] = col;
                }
                else if (Matches(norm, "planned start", "baseline start", "plan start", "bl start"))
                {
                    map["planned_start"] = col;
                }
                else if (Matches(norm, "planned finish", "baseline finish", "plan finish", "bl finish", "planned end"))
                {
                    map["planned_finish"] = col;
                }
                else if (Matches(norm, "forecast start", "actual start", "fc start", "forecasted start"))
                {
                    map["forecast_start"] = col;
                }
                else if (Matches(norm, "forecast finish", "actual finish", "fc finish", "forecasted finish", "forecast end"))
                {
                    map["forecast_finish"] = col;
                }
                else if (norm.Contains("cost") && (norm.Contains("bac") || norm.Contains("budget")))
                {
                    map["bac_cost"] = col;
                }
                else if (norm.Contains("unit") && (norm.Contains("bac") || norm.Contains("budget")))
                {
                    map["bac_units"] = col;
                }
                else if (Matches(norm, "budget", "bac", "budget at completion", "total planned value", "total pv"))
                {
                    map["bac"] = col;
                }
                else if (norm.Contains("cost") && (norm.Contains("earned") || norm.Contains("ev")))
                {
                    map["earned_cost"] = col;
                }
                else if (norm.Contains("unit") && (norm.Contains("earned") || norm.Contains("ev")))
                {
                    map["earned_units"] = col;
                }
                else if (Matches(norm, "cumulative earned value", "total earned value", "total ev", "earned value to date"))
                {
                    map["earned_value"] = col;
                }
                else if (norm.Contains("cost") && (norm.Contains("planned") || norm.Contains("pv")))
                {
                    map["planned_cost"] = col;
                }
                else if (norm.Contains("unit") && (norm.Contains("planned") || norm.Contains("pv")))
                {
                    map["planned_units"] = col;
                }
                else if (Matches(norm, "cumulative planned value", "total planned value", "planned value to date"))
                {
                    map["planned_value"] = col;
                }
                else
                {
                    // Check if it's a timeline periodic column: e.g. "1-Sep-26", "Sep-26", "Oct-26", "2026-09-01", "W1", "Week 1"
                    var colDate = TryParseDateHeader(text);
                    var weekNumMatch = Regex.Match(norm, @"(w|week|wk)\s*(\d+)", RegexOptions.IgnoreCase);
                    int weekNum = 0;
                    if (weekNumMatch.Success) int.TryParse(weekNumMatch.Groups[2].Value, out weekNum);

                    bool isCost = norm.Contains("cost") || norm.Contains("$");
                    bool isEarned = norm.Contains("ev") || norm.Contains("earned") || norm.Contains("actual");

                    if (colDate.HasValue || weekNum > 0)
                    {
                        timelineCols.Add(new TimelineColumnDef
                        {
                            ColumnIndex = col,
                            HeaderText = text,
                            PeriodDate = colDate,
                            IsCost = isCost,
                            IsEarned = isEarned,
                            WeekNumber = weekNum > 0 ? weekNum : (colDate.HasValue ? colDate.Value.DayOfYear / 7 : 0)
                        });
                    }
                }
            }

            return (map, timelineCols);
        }

        private static bool Matches(string text, params string[] patterns)
        {
            foreach (var p in patterns)
            {
                if (text == p || text.StartsWith(p + " ") || text.EndsWith(" " + p)) return true;
            }
            return false;
        }

        private static DateTime? TryGetDate(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            if (cell.DataType == XLDataType.DateTime)
                return cell.GetDateTime();

            var str = cell.GetString().Trim();
            if (DateTime.TryParse(str, out var dt)) return dt;
            if (double.TryParse(str, out var oaDate) && oaDate > 20000 && oaDate < 80000)
            {
                try { return DateTime.FromOADate(oaDate); } catch { }
            }
            return null;
        }

        private static double TryGetDouble(IXLCell cell)
        {
            if (cell.IsEmpty()) return 0;
            if (cell.DataType == XLDataType.Number)
                return cell.GetDouble();

            var str = cell.GetString().Trim().Replace("$", "").Replace(",", "").Replace("%", "");
            if (double.TryParse(str, out var val)) return val;
            return 0;
        }

        private static int? TryGetInt(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            if (cell.DataType == XLDataType.Number)
                return (int)cell.GetDouble();

            var str = cell.GetString().Trim();
            if (int.TryParse(str, out var val)) return val;
            if (double.TryParse(str, out var dVal)) return (int)dVal;
            return null;
        }

        private static double? TryGetPercent(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            if (cell.DataType == XLDataType.Number)
            {
                var val = cell.GetDouble();
                // Check if it's formatted as fraction 0.1579 (15.79%) or direct 15.79
                return val <= 1.0 ? Math.Round(val * 100.0, 2) : Math.Round(val, 2);
            }

            var str = cell.GetString().Trim().Replace("%", "");
            if (double.TryParse(str, out var parsed))
            {
                return parsed <= 1.0 && cell.GetString().Contains("%") ? Math.Round(parsed * 100.0, 2) : Math.Round(parsed, 2);
            }
            return null;
        }

        private static DateTime? TryParseDateHeader(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Formats like "1-Sep-26", "01-Sep-2026", "Sep-26", "2026-09-01", "1/9/2026"
            if (DateTime.TryParse(text, out var directDate))
                return directDate;

            var match = Regex.Match(text, @"(\d{1,2}[-/][A-Za-z]{3}[-/]\d{2,4})");
            if (match.Success && DateTime.TryParse(match.Value, out var dtMonth))
                return dtMonth;

            var isoMatch = Regex.Match(text, @"(\d{4}[-/]\d{1,2}[-/]\d{1,2})");
            if (isoMatch.Success && DateTime.TryParse(isoMatch.Value, out var dtIso))
                return dtIso;

            return null;
        }
    }
}
