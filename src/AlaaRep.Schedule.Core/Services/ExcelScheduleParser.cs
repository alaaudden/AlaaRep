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

        public ScheduleProjectSummary ParseWorksheet(IXLWorksheet worksheet)
        {
            var headerRowIndex = FindHeaderRow(worksheet);
            if (headerRowIndex == -1)
            {
                throw new InvalidOperationException("Could not detect header row with required schedule columns (Activity ID / Name).");
            }

            var headerRow = worksheet.Row(headerRowIndex);
            var columnMap = MapColumns(headerRow);

            if (!columnMap.ContainsKey("activity_id") && !columnMap.ContainsKey("activity_name"))
            {
                throw new InvalidOperationException("Missing mandatory columns: Activity ID or Activity Name column could not be identified.");
            }

            var activities = new List<ActivityScheduleItem>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRowIndex;

            for (int r = headerRowIndex + 1; r <= lastRow; r++)
            {
                var row = worksheet.Row(r);
                if (row.IsEmpty()) continue;

                var id = columnMap.ContainsKey("activity_id") ? row.Cell(columnMap["activity_id"]).GetString().Trim() : $"ACT-{r - headerRowIndex}";
                var name = columnMap.ContainsKey("activity_name") ? row.Cell(columnMap["activity_name"]).GetString().Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name))
                    continue;

                var activity = new ActivityScheduleItem
                {
                    ActivityId = string.IsNullOrWhiteSpace(id) ? $"ACT-{r - headerRowIndex}" : id,
                    ActivityName = string.IsNullOrWhiteSpace(name) ? id : name
                };

                if (columnMap.TryGetValue("planned_start", out int psCol))
                    activity.PlannedStartDate = TryGetDate(row.Cell(psCol));

                if (columnMap.TryGetValue("planned_finish", out int pfCol))
                    activity.PlannedFinishDate = TryGetDate(row.Cell(pfCol));

                if (columnMap.TryGetValue("forecast_start", out int fsCol))
                    activity.ForecastStartDate = TryGetDate(row.Cell(fsCol));

                if (columnMap.TryGetValue("forecast_finish", out int ffCol))
                    activity.ForecastFinishDate = TryGetDate(row.Cell(ffCol));

                if (columnMap.TryGetValue("bac", out int bacCol))
                    activity.BudgetAtCompletion = TryGetDouble(row.Cell(bacCol));

                if (columnMap.TryGetValue("earned_value", out int evCol))
                    activity.EarnedValueToDate = TryGetDouble(row.Cell(evCol));

                if (columnMap.TryGetValue("planned_value", out int pvCol))
                    activity.PlannedValueToDate = TryGetDouble(row.Cell(pvCol));

                // Process weekly columns
                foreach (var weeklyCol in _weeklyColumns)
                {
                    double pv = 0;
                    double ev = 0;

                    if (weeklyCol.PlannedColIndex.HasValue)
                        pv = TryGetDouble(row.Cell(weeklyCol.PlannedColIndex.Value));

                    if (weeklyCol.EarnedColIndex.HasValue)
                        ev = TryGetDouble(row.Cell(weeklyCol.EarnedColIndex.Value));

                    activity.WeeklyValues.Add(new WeeklyValueItem
                    {
                        WeekNumber = weeklyCol.WeekNumber,
                        WeekLabel = weeklyCol.WeekLabel,
                        WeekEndingDate = weeklyCol.WeekDate,
                        PlannedValue = pv,
                        EarnedValue = ev
                    });
                }

                if (activity.WeeklyValues.Any())
                {
                    var sumPv = activity.WeeklyValues.Sum(w => w.PlannedValue);
                    var sumEv = activity.WeeklyValues.Sum(w => w.EarnedValue);

                    if (activity.BudgetAtCompletion <= 0 && sumPv > 0)
                        activity.BudgetAtCompletion = sumPv;

                    if (activity.PlannedValueToDate <= 0 && sumPv > 0)
                        activity.PlannedValueToDate = sumPv;

                    if (activity.EarnedValueToDate <= 0 && sumEv > 0)
                        activity.EarnedValueToDate = sumEv;
                }

                activities.Add(activity);
            }

            var projectName = worksheet.Name;
            return _calculationEngine.CalculateSummary(activities, projectName);
        }

        private class WeeklyColumnDef
        {
            public int WeekNumber { get; set; }
            public string WeekLabel { get; set; } = string.Empty;
            public DateTime? WeekDate { get; set; }
            public int? PlannedColIndex { get; set; }
            public int? EarnedColIndex { get; set; }
        }

        private List<WeeklyColumnDef> _weeklyColumns = new();

        private int FindHeaderRow(IXLWorksheet worksheet)
        {
            var maxScan = Math.Min(15, worksheet.LastRowUsed()?.RowNumber() ?? 1);
            for (int r = 1; r <= maxScan; r++)
            {
                var row = worksheet.Row(r);
                var text = string.Join(" ", row.CellsUsed().Select(c => c.GetString().Trim().ToLowerInvariant()));
                if ((text.Contains("activity") || text.Contains("act") || text.Contains("task") || text.Contains("wbs")) &&
                    (text.Contains("plan") || text.Contains("start") || text.Contains("date") || text.Contains("forecast") || text.Contains("week") || text.Contains("pv") || text.Contains("ev")))
                {
                    return r;
                }
            }
            return 1;
        }

        private Dictionary<string, int> MapColumns(IXLRow headerRow)
        {
            var map = new Dictionary<string, int>();
            _weeklyColumns = new List<WeeklyColumnDef>();

            var weeklyPvDict = new Dictionary<int, (int colIndex, string label, DateTime? date)>();
            var weeklyEvDict = new Dictionary<int, (int colIndex, string label, DateTime? date)>();

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
                else if (Matches(norm, "budget", "bac", "budget at completion", "total planned value", "total pv"))
                {
                    map["bac"] = col;
                }
                else if (Matches(norm, "cumulative earned value", "total earned value", "total ev", "earned value to date"))
                {
                    map["earned_value"] = col;
                }
                else if (Matches(norm, "cumulative planned value", "total planned value", "planned value to date"))
                {
                    map["planned_value"] = col;
                }
                else
                {
                    // Check if it's weekly PV or EV column
                    // Formats: "W1 PV", "Week 1 Planned", "W01 PV", "2026-04-05 PV", "Week 1 EV", "W1 Earned"
                    var weekNumMatch = Regex.Match(norm, @"(w|week|wk)\s*(\d+)", RegexOptions.IgnoreCase);
                    int weekNum = 0;
                    if (weekNumMatch.Success)
                    {
                        int.TryParse(weekNumMatch.Groups[2].Value, out weekNum);
                    }

                    DateTime? colDate = TryParseDateHeader(text);
                    if (weekNum == 0 && colDate.HasValue)
                    {
                        weekNum = colDate.Value.DayOfYear / 7;
                    }

                    bool isPv = norm.Contains("pv") || norm.Contains("plan") || norm.Contains("baseline");
                    bool isEv = norm.Contains("ev") || norm.Contains("earn") || norm.Contains("actual");

                    if (weekNum > 0 || isPv || isEv)
                    {
                        if (weekNum == 0)
                        {
                            // Try extracting single number
                            var numOnly = Regex.Match(norm, @"\d+");
                            if (numOnly.Success) int.TryParse(numOnly.Value, out weekNum);
                        }

                        if (weekNum > 0)
                        {
                            string label = $"W{weekNum}";
                            if (isEv && !isPv)
                            {
                                weeklyEvDict[weekNum] = (col, label, colDate);
                            }
                            else
                            {
                                weeklyPvDict[weekNum] = (col, label, colDate);
                            }
                        }
                    }
                }
            }

            // Consolidate weekly columns
            var allWeekNums = weeklyPvDict.Keys.Union(weeklyEvDict.Keys).OrderBy(k => k).ToList();
            foreach (var wn in allWeekNums)
            {
                var def = new WeeklyColumnDef
                {
                    WeekNumber = wn,
                    WeekLabel = weeklyPvDict.ContainsKey(wn) ? weeklyPvDict[wn].label : weeklyEvDict[wn].label,
                    WeekDate = weeklyPvDict.ContainsKey(wn) ? weeklyPvDict[wn].date : weeklyEvDict[wn].date,
                    PlannedColIndex = weeklyPvDict.ContainsKey(wn) ? weeklyPvDict[wn].colIndex : null,
                    EarnedColIndex = weeklyEvDict.ContainsKey(wn) ? weeklyEvDict[wn].colIndex : null
                };
                _weeklyColumns.Add(def);
            }

            return map;
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

        private static DateTime? TryParseDateHeader(string text)
        {
            var match = Regex.Match(text, @"(\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/]\d{1,2}[-/]\d{2,4})");
            if (match.Success && DateTime.TryParse(match.Value, out var dt))
                return dt;
            return null;
        }
    }
}
