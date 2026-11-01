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
        self.relationships: List[Dict[str, Any]] = []
        self.resources: Dict[str, Dict[str, Any]] = {}
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
            "raw": row,
        }

    # 3. Tasks / Activities (TASK table)
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

        # WBS path
        wbs_id = row.get("wbs_id", "")
        wbs_item = data.wbs.get(wbs_id, {})
        wbs_name = wbs_item.get("wbs_name", "General")

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
            "wbs_name": wbs_name,
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
            "raw": row,
        }

        data.tasks[task_code] = task_dict
        data.tasks_by_id[task_id] = task_dict

    # 4. Relationships (TASKPRED table)
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
