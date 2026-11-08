"""
Primavera P6 XER File Parser
Parses Oracle Primavera P6 XER export files into structured Python objects.

Primavera XER format:
- Lines starting with %T denote Table definition (%T TABLE_NAME)
- Lines starting with %F denote Field definitions (%F col1 \t col2 ...)
- Lines starting with %R denote Data records (%R val1 \t val2 ...)
- Lines starting with %E denote End of export
"""

import os
from typing import Dict, List, Optional, Any
from datetime import datetime


def parse_date(val: Optional[str]) -> Optional[datetime]:
    """Parse standard P6 date formats: 'YYYY-MM-DD HH:MM' or 'YYYY-MM-DD'."""
    if not val:
        return None
    val = val.strip()
    if not val:
        return None
    for fmt in ("%Y-%m-%d %H:%M", "%Y-%m-%d %H:%M:%S", "%Y-%m-%d", "%d-%b-%y %H:%M", "%d-%b-%y"):
        try:
            return datetime.strptime(val, fmt)
        except ValueError:
            continue
    return None


def format_date(dt: Optional[datetime], fmt: str = "%Y-%m-%d") -> str:
    if not dt:
        return "-"
    return dt.strftime(fmt)


def to_float(val: Any, default: float = 0.0) -> float:
    if val is None:
        return default
    try:
        return float(val)
    except (ValueError, TypeError):
        return default


def to_int(val: Any, default: int = 0) -> int:
    if val is None:
        return default
    try:
        return int(float(val))
    except (ValueError, TypeError):
        return default


class XERProjectData:
    def __init__(self, filename: str = ""):
        self.filename: str = filename
        self.tables: Dict[str, List[Dict[str, str]]] = {}
        self.project_info: Dict[str, Any] = {}
        self.tasks: Dict[str, Dict[str, Any]] = {}  # task_code -> task dict
        self.tasks_by_id: Dict[str, Dict[str, Any]] = {}  # task_id -> task dict
        self.wbs: Dict[str, Dict[str, Any]] = {}  # wbs_id -> wbs dict
        self.wbs_hierarchy: List[Dict[str, Any]] = []  # nested or ordered wbs tree
        self.relationships: List[Dict[str, Any]] = []
        self.resources: Dict[str, Dict[str, Any]] = {}  # rsrc_id -> resource dict
        self.task_resources: List[Dict[str, Any]] = []  # task resource assignments
        self.activity_code_types: Dict[str, Dict[str, Any]] = {}  # actv_code_type_id -> type dict
        self.activity_codes: Dict[str, Dict[str, Any]] = {}  # actv_code_id -> code dict
        self.task_activity_codes: List[Dict[str, Any]] = []  # task_id -> list of assigned codes
        self.calendars: Dict[str, Dict[str, Any]] = {}

    def get_table(self, table_name: str) -> List[Dict[str, str]]:
        return self.tables.get(table_name.upper(), [])


def parse_xer(filepath: str) -> XERProjectData:
    """
    Parses a Primavera P6 .xer file from disk.
    Tries multiple encodings to handle international and legacy exports.
    """
    if not os.path.exists(filepath):
        raise FileNotFoundError(f"XER file not found: {filepath}")

    encodings = ["utf-8-sig", "utf-8", "windows-1252", "latin-1", "iso-8859-1"]
    content: Optional[str] = None

    for enc in encodings:
        try:
            with open(filepath, "r", encoding=enc) as f:
                content = f.read()
                break
        except (UnicodeDecodeError, UnicodeError):
            continue

    if content is None:
        with open(filepath, "rb") as f:
            raw = f.read()
            content = raw.decode("latin-1", errors="replace")

    return parse_xer_content(content, os.path.basename(filepath))


def parse_xer_content(content: str, filename: str = "schedule.xer") -> XERProjectData:
    """
    Parses XER file text content into a structured XERProjectData object.
    """
    project_data = XERProjectData(filename=filename)
    current_table: Optional[str] = None
    current_fields: List[str] = []

    # Lines in XER are delimited by newlines, fields within %F and %R are tab-delimited
    lines = content.splitlines()

    for line in lines:
        if not line:
            continue
        line_strip = line.strip()
        if not line_strip:
            continue

        if line.startswith("%T\t") or line.startswith("%T "):
            parts = line.split(None, 1)
            current_table = parts[1].strip().upper() if len(parts) > 1 else ""
            if current_table not in project_data.tables:
                project_data.tables[current_table] = []
            current_fields = []

        elif line.startswith("%F\t") or line.startswith("%F "):
            fields_str = line[3:] if line.startswith("%F\t") else line[2:].lstrip()
            current_fields = [f.strip() for f in fields_str.split("\t")]

        elif line.startswith("%R\t") or line.startswith("%R "):
            if not current_table or not current_fields:
                continue
            record_str = line[3:] if line.startswith("%R\t") else line[2:].lstrip()
            # Split tabs
            vals = record_str.split("\t")
            # Map fields to values
            row: Dict[str, str] = {}
            for i, field_name in enumerate(current_fields):
                if i < len(vals):
                    row[field_name] = vals[i].strip()
                else:
                    row[field_name] = ""
            project_data.tables[current_table].append(row)

        elif line.startswith("%E"):
            # End of export
            break

    # Process High-Level Entities
    _process_entities(project_data)
    return project_data


def _process_entities(data: XERProjectData) -> None:
    # 1. Project Info (PROJECT table)
    projects = data.get_table("PROJECT")
    if projects:
        proj = projects[0]
        data.project_info = {
            "proj_id": proj.get("proj_id", ""),
            "proj_short_name": proj.get("proj_short_name", "Project Schedule"),
            "data_date": parse_date(proj.get("last_recalc_date") or proj.get("restart_date")),
            "plan_start_date": parse_date(proj.get("plan_start_date")),
            "plan_end_date": parse_date(proj.get("plan_end_date")),
            "scd_end_date": parse_date(proj.get("scd_end_date")),
            "raw": proj,
        }
    else:
        data.project_info = {
            "proj_id": "",
            "proj_short_name": "Project Schedule",
            "data_date": None,
            "plan_start_date": None,
            "plan_end_date": None,
            "scd_end_date": None,
            "raw": {},
        }

    # 2. WBS (PROJWBS table)
    wbs_rows = data.get_table("PROJWBS")
    for row in wbs_rows:
        wbs_id = row.get("wbs_id", "")
        if not wbs_id:
            continue
        data.wbs[wbs_id] = {
            "wbs_id": wbs_id,
            "parent_wbs_id": row.get("parent_wbs_id", ""),
            "wbs_short_name": row.get("wbs_short_name", ""),
            "wbs_name": row.get("wbs_name", ""),
            "seq_num": to_int(row.get("seq_num", 0)),
            "status_code": row.get("status_code", ""),
            "level": 1,
            "path": row.get("wbs_short_name", ""),
            "raw": row,
        }

    # Compute WBS levels and hierarchy paths
    def get_wbs_level_and_path(wid: str, visited=None) -> tuple[int, str]:
        if visited is None:
            visited = set()
        if wid in visited or wid not in data.wbs:
            return 1, ""
        visited.add(wid)
        parent_id = data.wbs[wid].get("parent_wbs_id", "")
        if parent_id and parent_id in data.wbs:
            p_level, p_path = get_wbs_level_and_path(parent_id, visited)
            curr_code = data.wbs[wid].get("wbs_short_name") or data.wbs[wid].get("wbs_name")
            return p_level + 1, f"{p_path} > {curr_code}" if p_path else curr_code
        curr_code = data.wbs[wid].get("wbs_short_name") or data.wbs[wid].get("wbs_name")
        return 1, curr_code

    for wid in list(data.wbs.keys()):
        lvl, pth = get_wbs_level_and_path(wid)
        data.wbs[wid]["level"] = lvl
        data.wbs[wid]["path"] = pth

    # 3. Activity Code Types (ACTVTYPE table)
    # Fields: actv_code_type_id, actv_code_type, actv_code_type_scope, proj_id, seq_num
    actv_type_rows = data.get_table("ACTVTYPE")
    for row in actv_type_rows:
        type_id = row.get("actv_code_type_id", "")
        if not type_id:
            continue
        data.activity_code_types[type_id] = {
            "actv_code_type_id": type_id,
            "type_name": row.get("actv_code_type", ""),
            "scope": row.get("actv_code_type_scope", ""),
            "seq_num": to_int(row.get("seq_num", 0)),
            "raw": row,
        }

    # 4. Activity Codes (ACTVCODE table)
    # Fields: actv_code_id, actv_code_type_id, parent_actv_code_id, actv_code_val, actv_code_name, seq_num
    actv_code_rows = data.get_table("ACTVCODE")
    for row in actv_code_rows:
        code_id = row.get("actv_code_id", "")
        if not code_id:
            continue
        type_id = row.get("actv_code_type_id", "")
        type_name = data.activity_code_types.get(type_id, {}).get("type_name", "Code")
        data.activity_codes[code_id] = {
            "actv_code_id": code_id,
            "actv_code_type_id": type_id,
            "type_name": type_name,
            "code_value": row.get("actv_code_val", ""),
            "code_name": row.get("actv_code_name", "") or row.get("actv_code_val", ""),
            "seq_num": to_int(row.get("seq_num", 0)),
            "raw": row,
        }

    # 5. Task Activity Code Assignments (TASKACTV table)
    # Fields: task_id, actv_code_type_id, actv_code_id, proj_id
    task_code_map: Dict[str, Dict[str, Dict[str, str]]] = {}  # task_id -> {type_name -> {val, name}}
    task_actv_rows = data.get_table("TASKACTV")
    for row in task_actv_rows:
        task_id = row.get("task_id", "")
        code_id = row.get("actv_code_id", "")
        type_id = row.get("actv_code_type_id", "")
        c_obj = data.activity_codes.get(code_id)
        type_obj = data.activity_code_types.get(type_id)
        type_name = type_obj.get("type_name") if type_obj else (c_obj.get("type_name") if c_obj else "General")
        val = c_obj.get("code_value", "") if c_obj else ""
        name = c_obj.get("code_name", "") if c_obj else ""

        if task_id not in task_code_map:
            task_code_map[task_id] = {}
        if type_name:
            task_code_map[task_id][type_name] = {
                "code_id": code_id,
                "code_val": val,
                "code_name": name,
            }
        data.task_activity_codes.append({
            "task_id": task_id,
            "actv_code_type_id": type_id,
            "actv_code_id": code_id,
            "type_name": type_name,
            "code_val": val,
            "code_name": name,
        })

    # 6. Resources (RSRC table) & Task Resource Assignments (TASKRSRC table)
    rsrc_rows = data.get_table("RSRC")
    for row in rsrc_rows:
        r_id = row.get("rsrc_id", "")
        if not r_id:
            continue
        data.resources[r_id] = {
            "rsrc_id": r_id,
            "rsrc_short_name": row.get("rsrc_short_name", ""),
            "rsrc_name": row.get("rsrc_name", ""),
            "rsrc_type": row.get("rsrc_type", "RT_Labor"),  # RT_Labor, RT_NonLabor, RT_Mat
            "unit_of_measure": row.get("unit_id", "hr"),
            "cost_rate": to_float(row.get("cost_rate", 0.0)),
            "raw": row,
        }

    task_rsrc_rows = data.get_table("TASKRSRC")
    task_resource_map: Dict[str, List[Dict[str, Any]]] = {}
    for row in task_rsrc_rows:
        t_id = row.get("task_id", "")
        r_id = row.get("rsrc_id", "")
        r_obj = data.resources.get(r_id, {})
        tgt_qty = to_float(row.get("target_qty", 0.0) or row.get("target_work_qty", 0.0))
        act_qty = to_float(row.get("act_qty", 0.0) or row.get("act_work_qty", 0.0))
        remain_qty = to_float(row.get("remain_qty", 0.0) or row.get("remain_work_qty", 0.0))
        tgt_cost = to_float(row.get("target_cost", 0.0))
        act_cost = to_float(row.get("act_cost", 0.0))

        assignment = {
            "task_id": t_id,
            "rsrc_id": r_id,
            "rsrc_name": r_obj.get("rsrc_name") or r_obj.get("rsrc_short_name") or "Labor Craft",
            "rsrc_type": r_obj.get("rsrc_type", "RT_Labor"),
            "target_qty": tgt_qty,
            "act_qty": act_qty,
            "remain_qty": remain_qty,
            "target_cost": tgt_cost,
            "act_cost": act_cost,
            "raw": row,
        }
        data.task_resources.append(assignment)
        if t_id not in task_resource_map:
            task_resource_map[t_id] = []
        task_resource_map[t_id].append(assignment)

    # 7. Tasks / Activities (TASK table)
    task_rows = data.get_table("TASK")
    for row in task_rows:
        task_id = row.get("task_id", "")
        task_code = row.get("task_code", "").strip()
        if not task_code:
            task_code = f"TASK-{task_id}"

        # Status: 'TK_NotStart' (Not Started), 'TK_Active' (In Progress), 'TK_Complete' (Completed)
        status_raw = row.get("status_code", "TK_NotStart")
        status_map = {
            "TK_NotStart": "Not Started",
            "TK_Active": "In Progress",
            "TK_Complete": "Completed",
        }
        status_clean = status_map.get(status_raw, status_raw)

        # Dates
        target_start = parse_date(row.get("target_start_date"))
        target_end = parse_date(row.get("target_end_date"))
        act_start = parse_date(row.get("act_start_date"))
        act_end = parse_date(row.get("act_end_date"))
        early_start = parse_date(row.get("early_start_date")) or parse_date(row.get("restart_date"))
        early_end = parse_date(row.get("early_end_date"))
        late_start = parse_date(row.get("late_start_date"))
        late_end = parse_date(row.get("late_end_date"))

        # Effective Start & Finish based on progress
        curr_start = act_start or early_start or target_start
        curr_end = act_end or early_end or target_end

        # Float and Durations
        # P6 durations in XER are stored in hours (usually based on 8 hr/day, e.g. 80.0 = 10 days)
        target_durn_hr = to_float(row.get("target_durn_hr_cnt", 0.0))
        act_durn_hr = to_float(row.get("act_work_qty", 0.0) or row.get("act_durn_hr_cnt", 0.0))
        remain_durn_hr = to_float(row.get("remain_durn_hr_cnt", 0.0))
        total_float_hr = to_float(row.get("total_float_hr_cnt", 0.0))
        free_float_hr = to_float(row.get("free_float_hr_cnt", 0.0))

        # Convert hours to days (default 8 hours per day)
        target_durn_days = round(target_durn_hr / 8.0, 1)
        act_durn_days = round(act_durn_hr / 8.0, 1)
        remain_durn_days = round(remain_durn_hr / 8.0, 1)
        total_float_days = round(total_float_hr / 8.0, 1)

        # % Complete
        # P6 has phys_complete_pct, act_work_qty, etc.
        phys_pct = to_float(row.get("phys_complete_pct", 0.0))
        dur_pct = to_float(row.get("duration_pct_comp", 0.0))
        pct_comp = phys_pct if phys_pct > 0 else dur_pct
        if status_clean == "Completed":
            pct_comp = 100.0

        task_type = row.get("task_type", "TT_Task")  # TT_Mile (Milestone), TT_FinMile, etc.
        is_milestone = "Mile" in task_type or target_durn_hr == 0

        # WBS path and level
        wbs_id = row.get("wbs_id", "")
        wbs_item = data.wbs.get(wbs_id, {})
        wbs_name = wbs_item.get("wbs_name", "General")
        wbs_code = wbs_item.get("wbs_short_name", "")
        wbs_level = wbs_item.get("level", 1)
        wbs_path = wbs_item.get("path", wbs_name)

        # Assigned Activity Codes
        assigned_codes = task_code_map.get(task_id, {})

        # Area / Zone extraction: Check activity codes first (e.g. 'Area' or 'Location' or 'Zone')
        # If not present in TASKACTV, infer from WBS or Task Name
        area_code = ""
        area_name = ""
        for k, c_info in assigned_codes.items():
            k_low = k.lower()
            if "area" in k_low or "zone" in k_low or "loc" in k_low:
                area_code = c_info.get("code_val", "")
                area_name = c_info.get("code_name", "")
                break

        if not area_code:
            # Fallback area inference from WBS name / task code prefix
            w_up = wbs_name.upper()
            t_up = task_code.upper()
            if "SUB" in w_up or "PIL" in w_up or "BASEMENT" in w_up or "SUB" in t_up:
                area_code = "ZONE-SUB"
                area_name = "Substructure & Foundation"
            elif "SUP" in w_up or "TOWER" in w_up or "CONCRETE" in w_up or "SUP" in t_up:
                area_code = "ZONE-TOW"
                area_name = "Superstructure Tower"
            elif "MEP" in w_up or "MEP" in t_up:
                area_code = "ZONE-MEP"
                area_name = "MEP Plant & Core"
            elif "FIN" in w_up or "ARCH" in w_up or "FIN" in t_up:
                area_code = "ZONE-FIN"
                area_name = "Interior Finishes & Facade"
            elif "ENG" in w_up or "PROC" in w_up or "CLO" in w_up or "ENG" in t_up or "PRC" in t_up:
                area_code = "ZONE-SITE"
                area_name = "Site Works & Infrastructure"
            else:
                area_code = "ZONE-GEN"
                area_name = "General Site"

        # Costs and Resources for this task
        assigned_res = task_resource_map.get(task_id, [])
        est_cost = to_float(row.get("target_cost", 0.0))
        act_cost = to_float(row.get("act_cost", 0.0))

        if not est_cost and assigned_res:
            est_cost = sum(r["target_cost"] for r in assigned_res)
            act_cost = sum(r["act_cost"] for r in assigned_res)

        # If still no cost specified in XER, default cost proportional to duration (e.g. $1,000 / day)
        if est_cost <= 0.0 and target_durn_days > 0:
            est_cost = target_durn_days * 5000.0
            act_cost = est_cost * (pct_comp / 100.0)

        task_dict = {
            "task_id": task_id,
            "task_code": task_code,
            "task_name": row.get("task_name", ""),
            "task_type": task_type,
            "is_milestone": is_milestone,
            "status_code": status_raw,
            "status": status_clean,
            "pct_comp": pct_comp,
            "wbs_id": wbs_id,
            "wbs_code": wbs_code,
            "wbs_name": wbs_name,
            "wbs_level": wbs_level,
            "wbs_path": wbs_path,
            "area_code": area_code,
            "area_name": area_name,
            "activity_codes": assigned_codes,
            "target_start": target_start,
            "target_end": target_end,
            "act_start": act_start,
            "act_end": act_end,
            "early_start": early_start,
            "early_end": early_end,
            "late_start": late_start,
            "late_end": late_end,
            "curr_start": curr_start,
            "curr_end": curr_end,
            "target_durn_days": target_durn_days,
            "act_durn_days": act_durn_days,
            "remain_durn_days": remain_durn_days,
            "total_float_days": total_float_days,
            "is_critical": total_float_days <= 0.0 and status_clean != "Completed",
            "target_cost": est_cost,
            "act_cost": act_cost,
            "resources": assigned_res,
            "raw": row,
        }

        data.tasks[task_code] = task_dict
        data.tasks_by_id[task_id] = task_dict

    # 8. Relationships (TASKPRED table)
    rel_rows = data.get_table("TASKPRED")
    for row in rel_rows:
        pred_task_id = row.get("pred_task_id", "")
        task_id = row.get("task_id", "")
        pred_type = row.get("pred_type", "PR_FS")  # PR_FS, PR_SS, PR_FF, PR_SF
        lag_hr = to_float(row.get("lag_hr_cnt", 0.0))
        data.relationships.append({
            "pred_task_id": pred_task_id,
            "task_id": task_id,
            "pred_type": pred_type,
            "lag_days": round(lag_hr / 8.0, 1),
            "raw": row,
        })
