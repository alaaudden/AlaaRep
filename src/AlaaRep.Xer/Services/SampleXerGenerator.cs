using System;
using System.Collections.Generic;
using AlaaRep.Xer.Models;

namespace AlaaRep.Xer.Services;

/// <summary>
/// Generates realistic Primavera P6 sample XER documents for demonstrations and testing.
/// </summary>
public static class SampleXerGenerator
{
    public static XerDocument CreateSampleProject(string projectName = "Commercial Building Project")
    {
        var doc = new XerDocument
        {
            HeaderLine = $"ERMHDR\t8.3\t{DateTime.Now:yyyy-MM-dd}\tPROJECT"
        };

        // 1. PROJECT table
        var projTable = doc.GetOrCreateTable("PROJECT");
        projTable.Fields.AddRange(new[]
        {
            "proj_id", "proj_short_name", "last_recalc_date", "plan_start_date", "plan_end_date", "scd_end_date"
        });
        projTable.AddRow(new Dictionary<string, string>
        {
            ["proj_id"] = "1001",
            ["proj_short_name"] = projectName,
            ["last_recalc_date"] = "2025-01-06 08:00",
            ["plan_start_date"] = "2025-01-06 08:00",
            ["plan_end_date"] = "2025-10-31 17:00",
            ["scd_end_date"] = "2025-10-31 17:00"
        });

        // 2. PROJWBS table
        var wbsTable = doc.GetOrCreateTable("PROJWBS");
        wbsTable.Fields.AddRange(new[]
        {
            "wbs_id", "proj_id", "parent_wbs_id", "wbs_short_name", "wbs_name", "seq_num"
        });
        wbsTable.AddRow(new Dictionary<string, string>
        {
            ["wbs_id"] = "1", ["proj_id"] = "1001", ["parent_wbs_id"] = "", ["wbs_short_name"] = "CBP", ["wbs_name"] = projectName, ["seq_num"] = "1"
        });
        wbsTable.AddRow(new Dictionary<string, string>
        {
            ["wbs_id"] = "2", ["proj_id"] = "1001", ["parent_wbs_id"] = "1", ["wbs_short_name"] = "ENG", ["wbs_name"] = "Engineering & Permitting", ["seq_num"] = "2"
        });
        wbsTable.AddRow(new Dictionary<string, string>
        {
            ["wbs_id"] = "3", ["proj_id"] = "1001", ["parent_wbs_id"] = "1", ["wbs_short_name"] = "SUB", ["wbs_name"] = "Substructure & Foundation", ["seq_num"] = "3"
        });
        wbsTable.AddRow(new Dictionary<string, string>
        {
            ["wbs_id"] = "4", ["proj_id"] = "1001", ["parent_wbs_id"] = "1", ["wbs_short_name"] = "SUP", ["wbs_name"] = "Superstructure Framing", ["seq_num"] = "4"
        });
        wbsTable.AddRow(new Dictionary<string, string>
        {
            ["wbs_id"] = "5", ["proj_id"] = "1001", ["parent_wbs_id"] = "1", ["wbs_short_name"] = "MEP", ["wbs_name"] = "MEP & Finishes", ["seq_num"] = "5"
        });

        // 3. TASK table
        var taskTable = doc.GetOrCreateTable("TASK");
        taskTable.Fields.AddRange(new[]
        {
            "task_id", "proj_id", "wbs_id", "task_code", "task_name", "task_type", "status_code",
            "target_durn_hr_cnt", "remain_durn_hr_cnt", "act_durn_hr_cnt", "phys_complete_pct", "total_float_hr_cnt",
            "target_start_date", "target_end_date", "act_start_date", "act_end_date", "early_start_date", "early_end_date"
        });

        var sampleTasks = new[]
        {
            ("101", "1", "M1000", "Notice to Proceed", "TT_Mile", "TK_Complete", "0.0", "0.0", "0.0", "100.0", "0.0", "2025-01-06 08:00", "2025-01-06 08:00", "2025-01-06 08:00", "2025-01-06 08:00"),
            ("102", "2", "ENG1010", "Site Survey & Mobilization", "TT_Task", "TK_Complete", "80.0", "0.0", "80.0", "100.0", "0.0", "2025-01-06 08:00", "2025-01-17 17:00", "2025-01-06 08:00", "2025-01-17 17:00"),
            ("103", "2", "ENG1020", "Structural Design Package", "TT_Task", "TK_Active", "160.0", "40.0", "120.0", "75.0", "0.0", "2025-01-20 08:00", "2025-02-14 17:00", "2025-01-20 08:00", ""),
            ("104", "3", "SUB1010", "Excavation & Shoring", "TT_Task", "TK_Active", "120.0", "60.0", "60.0", "50.0", "0.0", "2025-02-17 08:00", "2025-03-07 17:00", "2025-02-17 08:00", ""),
            ("105", "3", "SUB1020", "Piling & Reinforced Raft", "TT_Task", "TK_NotStart", "160.0", "160.0", "0.0", "0.0", "0.0", "2025-03-10 08:00", "2025-04-04 17:00", "", ""),
            ("106", "4", "SUP1010", "Columns & Level 1 Concrete", "TT_Task", "TK_NotStart", "200.0", "200.0", "0.0", "0.0", "0.0", "2025-04-07 08:00", "2025-05-09 17:00", "", ""),
            ("107", "4", "SUP1020", "Elevated Slabs Level 2-5", "TT_Task", "TK_NotStart", "320.0", "320.0", "0.0", "0.0", "0.0", "2025-05-12 08:00", "2025-07-04 17:00", "", ""),
            ("108", "5", "MEP1010", "MEP First Fix & Rough-ins", "TT_Task", "TK_NotStart", "240.0", "240.0", "0.0", "0.0", "16.0", "2025-07-07 08:00", "2025-08-15 17:00", "", ""),
            ("109", "5", "FIN1010", "Drywall Partitions & Ceilings", "TT_Task", "TK_NotStart", "200.0", "200.0", "0.0", "0.0", "24.0", "2025-08-18 08:00", "2025-09-19 17:00", "", ""),
            ("110", "1", "M1030", "Project Handover & Completion", "TT_Mile", "TK_NotStart", "0.0", "0.0", "0.0", "0.0", "0.0", "2025-10-31 17:00", "2025-10-31 17:00", "", "")
        };

        foreach (var t in sampleTasks)
        {
            taskTable.AddRow(new Dictionary<string, string>
            {
                ["task_id"] = t.Item1,
                ["proj_id"] = "1001",
                ["wbs_id"] = t.Item2,
                ["task_code"] = t.Item3,
                ["task_name"] = t.Item4,
                ["task_type"] = t.Item5,
                ["status_code"] = t.Item6,
                ["target_durn_hr_cnt"] = t.Item7,
                ["remain_durn_hr_cnt"] = t.Item8,
                ["act_durn_hr_cnt"] = t.Item9,
                ["phys_complete_pct"] = t.Item10,
                ["total_float_hr_cnt"] = t.Item11,
                ["target_start_date"] = t.Item12,
                ["target_end_date"] = t.Item13,
                ["act_start_date"] = t.Item14,
                ["act_end_date"] = t.Item15,
                ["early_start_date"] = t.Item12,
                ["early_end_date"] = t.Item13,
            });
        }

        // 4. TASKPRED relationships
        var relTable = doc.GetOrCreateTable("TASKPRED");
        relTable.Fields.AddRange(new[] { "task_pred_id", "task_id", "pred_task_id", "pred_type", "lag_hr_cnt" });
        relTable.AddRow(new Dictionary<string, string> { ["task_pred_id"] = "1", ["task_id"] = "102", ["pred_task_id"] = "101", ["pred_type"] = "PR_FS", ["lag_hr_cnt"] = "0.0" });
        relTable.AddRow(new Dictionary<string, string> { ["task_pred_id"] = "2", ["task_id"] = "103", ["pred_task_id"] = "102", ["pred_type"] = "PR_FS", ["lag_hr_cnt"] = "0.0" });
        relTable.AddRow(new Dictionary<string, string> { ["task_pred_id"] = "3", ["task_id"] = "104", ["pred_task_id"] = "103", ["pred_type"] = "PR_FS", ["lag_hr_cnt"] = "0.0" });
        relTable.AddRow(new Dictionary<string, string> { ["task_pred_id"] = "4", ["task_id"] = "105", ["pred_task_id"] = "104", ["pred_type"] = "PR_FS", ["lag_hr_cnt"] = "0.0" });
        relTable.AddRow(new Dictionary<string, string> { ["task_pred_id"] = "5", ["task_id"] = "106", ["pred_task_id"] = "105", ["pred_type"] = "PR_FS", ["lag_hr_cnt"] = "0.0" });
        relTable.AddRow(new Dictionary<string, string> { ["task_pred_id"] = "6", ["task_id"] = "107", ["pred_task_id"] = "106", ["pred_type"] = "PR_FS", ["lag_hr_cnt"] = "0.0" });
        relTable.AddRow(new Dictionary<string, string> { ["task_pred_id"] = "7", ["task_id"] = "108", ["pred_task_id"] = "107", ["pred_type"] = "PR_FS", ["lag_hr_cnt"] = "0.0" });
        relTable.AddRow(new Dictionary<string, string> { ["task_pred_id"] = "8", ["task_id"] = "109", ["pred_task_id"] = "108", ["pred_type"] = "PR_FS", ["lag_hr_cnt"] = "0.0" });
        relTable.AddRow(new Dictionary<string, string> { ["task_pred_id"] = "9", ["task_id"] = "110", ["pred_task_id"] = "109", ["pred_type"] = "PR_FS", ["lag_hr_cnt"] = "0.0" });

        return doc;
    }
}
