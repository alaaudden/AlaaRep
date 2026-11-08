"""
Generator script for authentic Primavera P6 Baseline & Weekly Update XER files
Represents a realistic commercial building construction project ("Marina Commercial Tower").
Includes PROJWBS, TASK, TASKPRED, ACTVTYPE, ACTVCODE, TASKACTV, RSRC, and TASKRSRC tables.
"""

from datetime import datetime, timedelta

def generate_sample_xers():
    # Base start date: Jan 8, 2024
    # Cutoff date (Update week 12): Mar 29, 2024

    # Baseline project
    baseline_project_fields = [
        "proj_id", "proj_short_name", "last_recalc_date", "plan_start_date", "plan_end_date", "scd_end_date"
    ]
    baseline_project_record = [
        "1001", "Marina Commercial Tower - Baseline", "2024-01-08 08:00", "2024-01-08 08:00", "2024-10-31 17:00", "2024-10-31 17:00"
    ]

    update_project_record = [
        "1001", "Marina Commercial Tower - Update W12", "2024-03-29 17:00", "2024-01-08 08:00", "2024-10-31 17:00", "2024-11-15 17:00"
    ]

    # WBS definitions
    wbs_fields = ["wbs_id", "proj_id", "parent_wbs_id", "wbs_short_name", "wbs_name", "seq_num"]
    wbs_records = [
        ["1", "1001", "", "MCT", "Marina Commercial Tower", "1"],
        ["2", "1001", "1", "MCT.ENG", "Engineering & Approvals", "2"],
        ["3", "1001", "1", "MCT.PROC", "Procurement & Subcontracts", "3"],
        ["4", "1001", "1", "MCT.SUB", "Substructure & Piling", "4"],
        ["5", "1001", "1", "MCT.SUP", "Superstructure & Concrete", "5"],
        ["6", "1001", "1", "MCT.MEP", "MEP First Fix & Rough-in", "6"],
        ["7", "1001", "1", "MCT.FIN", "Architectural Finishes", "7"],
        ["8", "1001", "1", "MCT.CLO", "Testing & Handover", "8"],
    ]

    # Activity Code Types (ACTVTYPE)
    actv_type_fields = ["actv_code_type_id", "actv_code_type", "actv_code_type_scope", "proj_id", "seq_num"]
    actv_type_records = [
        ["1", "Project Area", "AS_Project", "1001", "1"],
        ["2", "Discipline", "AS_Project", "1001", "2"],
        ["3", "Phase", "AS_Project", "1001", "3"],
    ]

    # Activity Code Values (ACTVCODE)
    actv_code_fields = ["actv_code_id", "actv_code_type_id", "parent_actv_code_id", "actv_code_val", "actv_code_name", "seq_num"]
    actv_code_records = [
        # Area codes (matching layout zones!)
        ["101", "1", "", "ZONE-SUB", "Substructure & Foundation", "1"],
        ["102", "1", "", "ZONE-TOW", "Superstructure Tower", "2"],
        ["103", "1", "", "ZONE-MEP", "MEP Plant & Core", "3"],
        ["104", "1", "", "ZONE-FIN", "Interior Finishes & Facade", "4"],
        ["105", "1", "", "ZONE-SITE", "Site Works & Infrastructure", "5"],

        # Discipline codes
        ["201", "2", "", "CIVIL", "Civil & Structural", "1"],
        ["202", "2", "", "MEP", "Mechanical & Electrical", "2"],
        ["203", "2", "", "ARCH", "Architectural", "3"],
        ["204", "2", "", "MGMT", "Project Management", "4"],

        # Phase codes
        ["301", "3", "", "PHASE-1", "Design & Pre-construction", "1"],
        ["302", "3", "", "PHASE-2", "Foundations & Shell", "2"],
        ["303", "3", "", "PHASE-3", "Fit-Out & Commissioning", "3"],
    ]

    # Resources (RSRC)
    rsrc_fields = ["rsrc_id", "rsrc_short_name", "rsrc_name", "rsrc_type", "cost_rate"]
    rsrc_records = [
        ["1", "PM-TEAM", "Project Management Staff", "RT_Labor", "85.0"],
        ["2", "CIVIL-CREW", "Civil Construction Labor Crew", "RT_Labor", "50.0"],
        ["3", "MEP-CREW", "MEP Electricians & Plumbers Crew", "RT_Labor", "60.0"],
        ["4", "FIN-CREW", "Finishes Craftsmen & Glaziers", "RT_Labor", "55.0"],
    ]

    # Activity master list:
    # (task_id, task_code, task_name, wbs_id, task_type, base_start, base_end, base_dur_days, base_tf, area_code_id, disc_code_id, phase_code_id, rsrc_id)
    activities = [
        # Milestones
        ("101", "M1000", "Notice to Proceed (NTP)", "1", "TT_Mile", "2024-01-08 08:00", "2024-01-08 08:00", 0, 0, "105", "204", "301", "1"),
        ("102", "M1010", "Foundation Approval Milestone", "4", "TT_Mile", "2024-02-16 17:00", "2024-02-16 17:00", 0, 0, "101", "201", "302", "1"),
        ("103", "M1020", "Superstructure Topping Out", "5", "TT_Mile", "2024-07-26 17:00", "2024-07-26 17:00", 0, 0, "102", "201", "302", "1"),
        ("104", "M1030", "Project Substantial Completion", "8", "TT_Mile", "2024-10-31 17:00", "2024-10-31 17:00", 0, 0, "105", "204", "303", "1"),

        # Engineering
        ("201", "ENG1010", "Mobilization & Site Survey", "2", "TT_Task", "2024-01-08 08:00", "2024-01-19 17:00", 10, 5, "105", "204", "301", "1"),
        ("202", "ENG1020", "Structural Foundation Shop Drawings", "2", "TT_Task", "2024-01-15 08:00", "2024-02-02 17:00", 15, 0, "101", "201", "301", "1"),
        ("203", "ENG1030", "Municipality Building Permit Approval", "2", "TT_Task", "2024-01-22 08:00", "2024-02-16 17:00", 20, 0, "105", "204", "301", "1"),
        ("204", "ENG1040", "MEP Coordinated BIM Models", "2", "TT_Task", "2024-02-05 08:00", "2024-03-08 17:00", 25, 12, "103", "202", "301", "1"),

        # Procurement
        ("301", "PRC1010", "Rebar & Structural Steel Supply Contract", "3", "TT_Task", "2024-01-15 08:00", "2024-02-09 17:00", 20, 0, "101", "201", "302", "2"),
        ("302", "PRC1020", "Curtain Wall & Facade Long-Lead Procurement", "3", "TT_Task", "2024-02-12 08:00", "2024-04-05 17:00", 40, 8, "104", "203", "302", "4"),
        ("303", "PRC1030", "HVAC Chillers & Main Switchgear Order", "3", "TT_Task", "2024-02-19 08:00", "2024-04-12 17:00", 40, 15, "103", "202", "302", "3"),

        # Substructure
        ("401", "SUB1010", "Site Clearing & Shoring Installation", "4", "TT_Task", "2024-01-22 08:00", "2024-02-09 17:00", 15, 2, "101", "201", "302", "2"),
        ("402", "SUB1020", "Mass Excavation & Dewatering", "4", "TT_Task", "2024-02-12 08:00", "2024-03-01 17:00", 15, 0, "101", "201", "302", "2"),
        ("403", "SUB1030", "Cast Concrete Piles & Pile Caps", "4", "TT_Task", "2024-03-04 08:00", "2024-03-22 17:00", 15, 0, "101", "201", "302", "2"),
        ("404", "SUB1040", "Raft Foundation Waterproofing & Casting", "4", "TT_Task", "2024-03-25 08:00", "2024-04-12 17:00", 15, 0, "101", "201", "302", "2"),
        ("405", "SUB1050", "Basement Retaining Walls & SOG", "4", "TT_Task", "2024-04-15 08:00", "2024-05-03 17:00", 15, 0, "101", "201", "302", "2"),

        # Superstructure
        ("501", "SUP1010", "Ground Floor Columns & Elevated Slab", "5", "TT_Task", "2024-05-06 08:00", "2024-05-24 17:00", 15, 0, "102", "201", "302", "2"),
        ("502", "SUP1020", "Floors 1 to 5 Concrete Framing", "5", "TT_Task", "2024-05-27 08:00", "2024-07-05 17:00", 30, 0, "102", "201", "302", "2"),
        ("503", "SUP1030", "Roof Slab & Parapet Casting", "5", "TT_Task", "2024-07-08 08:00", "2024-07-26 17:00", 15, 0, "102", "201", "302", "2"),

        # MEP
        ("601", "MEP1010", "Basement Underground Drainage & Conduits", "6", "TT_Task", "2024-03-18 08:00", "2024-04-05 17:00", 15, 10, "103", "202", "302", "3"),
        ("602", "MEP1020", "Electrical Risers & Plumbing Rough-in", "6", "TT_Task", "2024-06-03 08:00", "2024-07-19 17:00", 35, 6, "103", "202", "302", "3"),
        ("603", "MEP1030", "HVAC Ductwork & Fire Protection Piping", "6", "TT_Task", "2024-07-01 08:00", "2024-08-16 17:00", 35, 4, "103", "202", "302", "3"),

        # Finishes
        ("701", "FIN1010", "Exterior Glazing & Curtain Wall Panels", "7", "TT_Task", "2024-07-29 08:00", "2024-09-20 17:00", 40, 2, "104", "203", "303", "4"),
        ("702", "FIN1020", "Internal CMU Partitions & Drywall Framing", "7", "TT_Task", "2024-08-05 08:00", "2024-09-27 17:00", 40, 5, "104", "203", "303", "4"),
        ("703", "FIN1030", "Flooring, Ceiling & Painting Works", "7", "TT_Task", "2024-09-02 08:00", "2024-10-18 17:00", 35, 0, "104", "203", "303", "4"),

        # Handover
        ("801", "CLO1010", "MEP Testing, Adjusting & Commissioning", "8", "TT_Task", "2024-09-23 08:00", "2024-10-18 17:00", 20, 0, "103", "202", "303", "3"),
        ("802", "CLO1020", "Civil Defense Inspection & Authority Sign-off", "8", "TT_Task", "2024-10-21 08:00", "2024-10-31 17:00", 9, 0, "105", "204", "303", "1"),
    ]

    task_fields = [
        "task_id", "proj_id", "wbs_id", "task_code", "task_name", "task_type",
        "status_code", "target_durn_hr_cnt", "remain_durn_hr_cnt", "act_durn_hr_cnt",
        "phys_complete_pct", "total_float_hr_cnt", "target_start_date", "target_end_date",
        "act_start_date", "act_end_date", "early_start_date", "early_end_date",
        "late_start_date", "late_end_date"
    ]

    task_actv_fields = ["task_id", "actv_code_type_id", "actv_code_id", "proj_id"]
    task_actv_records = []
    task_rsrc_fields = ["task_id", "rsrc_id", "target_qty", "remain_qty", "act_qty", "target_cost", "act_cost"]
    task_rsrc_records = []

    # 1. Build Baseline Records
    baseline_task_records = []
    for item in activities:
        tid, code, name, wid, ttype, bstart, bend, dur_days, tf_days, a_id, d_id, p_id, r_id = item
        dur_hr = dur_days * 8.0
        tf_hr = tf_days * 8.0
        rec = [
            tid, "1001", wid, code, name, ttype,
            "TK_NotStart", f"{dur_hr:.1f}", f"{dur_hr:.1f}", "0.0",
            "0.0", f"{tf_hr:.1f}", bstart, bend,
            "", "", bstart, bend,
            bstart, bend
        ]
        baseline_task_records.append(rec)

        # Code assignments
        task_actv_records.append([tid, "1", a_id, "1001"])
        task_actv_records.append([tid, "2", d_id, "1001"])
        task_actv_records.append([tid, "3", p_id, "1001"])

        # Resource assignments
        work_hours = dur_hr * 4.0 if dur_hr > 0 else 0.0
        cost = work_hours * 55.0
        task_rsrc_records.append([tid, r_id, f"{work_hours:.1f}", f"{work_hours:.1f}", "0.0", f"{cost:.2f}", "0.00"])

    # 2. Build Update Week 12 Records (Cutoff: 2024-03-29)
    update_updates = {
        "M1000": ("TK_Complete", 0, 0, 0, 100.0, 0, "2024-01-08 08:00", "2024-01-08 08:00", "", ""),
        "ENG1010": ("TK_Complete", 10, 0, 10, 100.0, 5, "2024-01-08 08:00", "2024-01-19 17:00", "", ""),
        "ENG1020": ("TK_Complete", 15, 0, 15, 100.0, 0, "2024-01-15 08:00", "2024-02-05 17:00", "", ""),
        "PRC1010": ("TK_Complete", 20, 0, 20, 100.0, 0, "2024-01-18 08:00", "2024-02-16 17:00", "", ""),
        "SUB1010": ("TK_Complete", 15, 0, 15, 100.0, 2, "2024-01-24 08:00", "2024-02-14 17:00", "", ""),
        "ENG1030": ("TK_Complete", 20, 0, 20, 100.0, 0, "2024-01-25 08:00", "2024-02-23 17:00", "", ""),
        "M1010": ("TK_Complete", 0, 0, 0, 100.0, 0, "2024-02-23 17:00", "2024-02-23 17:00", "", ""),
        "SUB1020": ("TK_Complete", 15, 0, 15, 100.0, 0, "2024-02-19 08:00", "2024-03-08 17:00", "", ""),
        "ENG1040": ("TK_Complete", 25, 0, 25, 100.0, 12, "2024-02-05 08:00", "2024-03-15 17:00", "", ""),

        "PRC1020": ("TK_Active", 40, 14, 26, 65.0, 4, "2024-02-12 08:00", "", "2024-02-12 08:00", "2024-04-12 17:00"),
        "PRC1030": ("TK_Active", 40, 22, 18, 45.0, 10, "2024-02-19 08:00", "", "2024-02-19 08:00", "2024-04-16 17:00"),
        "MEP1010": ("TK_Active", 15, 10.5, 4.5, 30.0, 6, "2024-03-20 08:00", "", "2024-03-20 08:00", "2024-04-12 17:00"),
        "SUB1030": ("TK_Active", 15, 3, 12, 80.0, -11, "2024-03-08 08:00", "", "2024-03-08 08:00", "2024-04-05 17:00"),
        "SUB1040": ("TK_NotStart", 15, 15, 0, 0.0, -11, "", "", "2024-04-08 08:00", "2024-04-26 17:00"),
        "SUB1050": ("TK_NotStart", 15, 15, 0, 0.0, -11, "", "", "2024-04-29 08:00", "2024-05-17 17:00"),
        "SUP1010": ("TK_NotStart", 15, 15, 0, 0.0, -11, "", "", "2024-05-20 08:00", "2024-06-07 17:00"),
        "SUP1020": ("TK_NotStart", 30, 30, 0, 0.0, -11, "", "", "2024-06-10 08:00", "2024-07-19 17:00"),
        "SUP1030": ("TK_NotStart", 15, 15, 0, 0.0, -11, "", "", "2024-07-22 08:00", "2024-08-09 17:00"),
        "M1020": ("TK_NotStart", 0, 0, 0, 0.0, -11, "", "", "2024-08-09 17:00", "2024-08-09 17:00"),
        "MEP1020": ("TK_NotStart", 35, 35, 0, 0.0, -5, "", "", "2024-06-17 08:00", "2024-08-02 17:00"),
        "MEP1030": ("TK_NotStart", 35, 35, 0, 0.0, -7, "", "", "2024-07-15 08:00", "2024-08-30 17:00"),
        "FIN1010": ("TK_NotStart", 40, 40, 0, 0.0, -9, "", "", "2024-08-12 08:00", "2024-10-04 17:00"),
        "FIN1020": ("TK_NotStart", 40, 40, 0, 0.0, -6, "", "", "2024-08-19 08:00", "2024-10-11 17:00"),
        "FIN1030": ("TK_NotStart", 35, 35, 0, 0.0, -11, "", "", "2024-09-16 08:00", "2024-11-01 17:00"),
        "CLO1010": ("TK_NotStart", 20, 20, 0, 0.0, -11, "", "", "2024-10-07 08:00", "2024-11-01 17:00"),
        "CLO1020": ("TK_NotStart", 9, 9, 0, 0.0, -11, "", "", "2024-11-04 08:00", "2024-11-15 17:00"),
        "M1030": ("TK_NotStart", 0, 0, 0, 0.0, -11, "", "", "2024-11-15 17:00", "2024-11-15 17:00"),
    }

    update_task_records = []
    update_task_rsrc_records = []
    for item in activities:
        tid, code, name, wid, ttype, bstart, bend, orig_dur_days, orig_tf_days, a_id, d_id, p_id, r_id = item
        u_info = update_updates.get(code)
        if u_info:
            status, tgt_d, rem_d, act_d, pct, tf_d, a_start, a_end, e_start, e_end = u_info
            curr_s = a_start if a_start else (e_start if e_start else bstart)
            curr_e = a_end if a_end else (e_end if e_end else bend)
            rec = [
                tid, "1001", wid, code, name, ttype,
                status, f"{tgt_d*8:.1f}", f"{rem_d*8:.1f}", f"{act_d*8:.1f}",
                f"{pct:.1f}", f"{tf_d*8:.1f}", bstart, bend,
                a_start, a_end, e_start or curr_s, e_end or curr_e,
                e_start or curr_s, e_end or curr_e
            ]
            # Resource actuals
            tot_w = orig_dur_days * 8.0 * 4.0
            act_w = tot_w * (pct / 100.0)
            rem_w = tot_w - act_w
            cost = tot_w * 55.0
            act_cost = act_w * 55.0
            update_task_rsrc_records.append([tid, r_id, f"{tot_w:.1f}", f"{rem_w:.1f}", f"{act_w:.1f}", f"{cost:.2f}", f"{act_cost:.2f}"])
        else:
            rec = [
                tid, "1001", wid, code, name, ttype,
                "TK_NotStart", f"{orig_dur_days*8:.1f}", f"{orig_dur_days*8:.1f}", "0.0",
                "0.0", f"{orig_tf_days*8:.1f}", bstart, bend,
                "", "", bstart, bend, bstart, bend
            ]
            tot_w = orig_dur_days * 8.0 * 4.0
            cost = tot_w * 55.0
            update_task_rsrc_records.append([tid, r_id, f"{tot_w:.1f}", f"{tot_w:.1f}", "0.0", f"{cost:.2f}", "0.00"])
        update_task_records.append(rec)

    # Relationships
    rel_fields = ["pred_task_id", "task_id", "pred_type", "lag_hr_cnt"]
    rel_records = [
        ["101", "201", "PR_FS", "0.0"],
        ["201", "202", "PR_SS", "40.0"],
        ["202", "203", "PR_FS", "0.0"],
        ["203", "102", "PR_FS", "0.0"],
        ["201", "301", "PR_FS", "0.0"],
        ["301", "401", "PR_FS", "0.0"],
        ["401", "402", "PR_FS", "0.0"],
        ["402", "403", "PR_FS", "0.0"],
        ["403", "404", "PR_FS", "0.0"],
        ["404", "405", "PR_FS", "0.0"],
        ["405", "501", "PR_FS", "0.0"],
        ["501", "502", "PR_FS", "0.0"],
        ["502", "503", "PR_FS", "0.0"],
        ["503", "103", "PR_FS", "0.0"],
        ["403", "601", "PR_FS", "0.0"],
        ["502", "602", "PR_SS", "80.0"],
        ["602", "603", "PR_SS", "160.0"],
        ["503", "701", "PR_FS", "0.0"],
        ["502", "702", "PR_SS", "160.0"],
        ["702", "703", "PR_FS", "0.0"],
        ["703", "801", "PR_FS", "0.0"],
        ["603", "801", "PR_FS", "0.0"],
        ["801", "802", "PR_FS", "0.0"],
        ["802", "104", "PR_FS", "0.0"],
    ]

    def render_xer(proj_rec, task_recs, rsrc_recs):
        out = []
        out.append("ERMHDR\t8.3\t2024-03-29\tPROJECT")

        # PROJECT table
        out.append("%T\tPROJECT")
        out.append("%F\t" + "\t".join(baseline_project_fields))
        out.append("%R\t" + "\t".join(proj_rec))

        # PROJWBS table
        out.append("%T\tPROJWBS")
        out.append("%F\t" + "\t".join(wbs_fields))
        for r in wbs_records:
            out.append("%R\t" + "\t".join(r))

        # ACTVTYPE table
        out.append("%T\tACTVTYPE")
        out.append("%F\t" + "\t".join(actv_type_fields))
        for r in actv_type_records:
            out.append("%R\t" + "\t".join(r))

        # ACTVCODE table
        out.append("%T\tACTVCODE")
        out.append("%F\t" + "\t".join(actv_code_fields))
        for r in actv_code_records:
            out.append("%R\t" + "\t".join(r))

        # RSRC table
        out.append("%T\tRSRC")
        out.append("%F\t" + "\t".join(rsrc_fields))
        for r in rsrc_records:
            out.append("%R\t" + "\t".join(r))

        # TASK table
        out.append("%T\tTASK")
        out.append("%F\t" + "\t".join(task_fields))
        for r in task_recs:
            out.append("%R\t" + "\t".join(r))

        # TASKACTV table
        out.append("%T\tTASKACTV")
        out.append("%F\t" + "\t".join(task_actv_fields))
        for r in task_actv_records:
            out.append("%R\t" + "\t".join(r))

        # TASKRSRC table
        out.append("%T\tTASKRSRC")
        out.append("%F\t" + "\t".join(task_rsrc_fields))
        for r in rsrc_recs:
            out.append("%R\t" + "\t".join(r))

        # TASKPRED table
        out.append("%T\tTASKPRED")
        out.append("%F\t" + "\t".join(rel_fields))
        for r in rel_records:
            out.append("%R\t" + "\t".join(r))

        out.append("%E")
        return "\n".join(out) + "\n"

    baseline_xer_text = render_xer(baseline_project_record, baseline_task_records, task_rsrc_records)
    update_xer_text = render_xer(update_project_record, update_task_records, update_task_rsrc_records)

    with open("samples/baseline_project.xer", "w", encoding="utf-8") as f:
        f.write(baseline_xer_text)

    with open("samples/update_week12.xer", "w", encoding="utf-8") as f:
        f.write(update_xer_text)
    print("Sample XER files created successfully in samples/")

if __name__ == "__main__":
    generate_sample_xers()
