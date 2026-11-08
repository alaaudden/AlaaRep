"""
Construction Project Progress & Baseline Comparison Analyzer
Compares a Primavera P6 Baseline XER schedule with an Update XER schedule.
Calculates:
- Progress % (Planned vs Actual / Earned)
- Schedule Variance (SV) and Schedule Performance Index (SPI)
- Milestone Variance (Baseline Finish vs Forecast/Actual Finish, Days slippage)
- Critical Path & Total Float Distribution
- Delay Classification (Delayed, On Schedule, Ahead, Completed, Not Started)
- WBS Progress Summary & Breakdown (Multi-level WBS)
- Layout Areas / Zones Progress & KPIs (Interactive Layout Mapping)
- Lookahead Schedule (30-day, 60-day, upcoming critical & active items)
- Manpower & Craft Analysis (monthly hours, planned vs actual craft hours)
- Cashflow Curve (planned vs actual monthly spend & cumulative S-curve)
- Invoicing & Contractor Payment Applications (IPCs, certified vs claimed, retention)
- Activity Codes & WBS Level Filter Metadata
- Weekly Cumulative S-Curve data (Planned vs Actual / Forecast)
"""

from datetime import datetime, timedelta
from typing import Dict, List, Any, Optional
from src.xer_parser import XERProjectData, format_date


def compare_schedules(baseline: XERProjectData, update: XERProjectData) -> Dict[str, Any]:
    """
    Core engine comparing baseline vs update project.
    """
    # Project dates
    data_date = update.project_info.get("data_date") or baseline.project_info.get("data_date") or datetime.now()
    b_start = baseline.project_info.get("plan_start_date") or _find_min_start(baseline.tasks) or (data_date - timedelta(days=90))
    b_finish = baseline.project_info.get("plan_end_date") or _find_max_finish(baseline.tasks) or (data_date + timedelta(days=180))
    u_finish = update.project_info.get("scd_end_date") or update.project_info.get("plan_end_date") or _find_max_finish(update.tasks) or b_finish

    # Days variance at project level
    project_variance_days = 0
    if b_finish and u_finish:
        project_variance_days = (u_finish.date() - b_finish.date()).days

    # Map baseline tasks
    b_tasks = baseline.tasks
    u_tasks = update.tasks

    matched_activities: List[Dict[str, Any]] = []
    wbs_map: Dict[str, Dict[str, Any]] = {}
    areas_map: Dict[str, Dict[str, Any]] = {}

    # Pre-populate WBS structure
    for wid, wobj in update.wbs.items():
        wbs_map[wid] = {
            "wbs_id": wid,
            "wbs_code": wobj.get("wbs_short_name", ""),
            "wbs_name": wobj.get("wbs_name", "Unknown"),
            "parent_wbs_id": wobj.get("parent_wbs_id", ""),
            "level": wobj.get("level", 1),
            "path": wobj.get("path", wobj.get("wbs_name", "")),
            "weight": 0.0,
            "planned_earned_dur": 0.0,
            "actual_earned_dur": 0.0,
            "total_tasks": 0,
            "completed_tasks": 0,
            "in_progress_tasks": 0,
            "critical_tasks": 0,
            "delayed_tasks": 0,
        }

    total_weight = 0.0
    planned_earned_weight = 0.0
    actual_earned_weight = 0.0

    critical_activities: List[Dict[str, Any]] = []
    delayed_activities: List[Dict[str, Any]] = []
    milestones: List[Dict[str, Any]] = []

    # Activity count metrics
    status_counts = {"Completed": 0, "In Progress": 0, "Not Started": 0}
    delay_counts = {"Delayed": 0, "On Track": 0, "Ahead": 0}
    float_dist = {"Critical (<=0d)": 0, "Near Critical (1-10d)": 0, "Healthy (>10d)": 0}

    # Available filter metadata sets
    all_wbs_levels = set()
    activity_code_filters: Dict[str, set] = {}

    for task_code, u_task in u_tasks.items():
        b_task = b_tasks.get(task_code, {})
        status = u_task.get("status", "Not Started")
        status_counts[status] = status_counts.get(status, 0) + 1

        b_dur = b_task.get("target_durn_days", u_task.get("target_durn_days", 0.0))
        # Use baseline duration as weighting factor; minimum 1 day for non-milestone to avoid zero weight
        is_mile = u_task.get("is_milestone", False)
        weight = b_dur if (not is_mile and b_dur > 0) else (1.0 if not is_mile else 0.0)

        # Dates
        bl_start = b_task.get("target_start") or b_task.get("early_start")
        bl_end = b_task.get("target_end") or b_task.get("early_end")
        up_start = u_task.get("curr_start")
        up_end = u_task.get("curr_end")

        # Planned % at Data Date based on baseline schedule
        planned_pct = _calc_planned_pct(bl_start, bl_end, data_date)
        actual_pct = u_task.get("pct_comp", 0.0)

        # Variance calculation
        start_var_days = (up_start.date() - bl_start.date()).days if (up_start and bl_start) else 0
        finish_var_days = (up_end.date() - bl_end.date()).days if (up_end and bl_end) else 0

        # Delay status classification
        if status == "Completed":
            delay_status = "Delayed" if finish_var_days > 0 else "On Track"
        else:
            if finish_var_days > 2 or (actual_pct < planned_pct - 5.0):
                delay_status = "Delayed"
            elif finish_var_days < -2 or (actual_pct > planned_pct + 5.0):
                delay_status = "Ahead"
            else:
                delay_status = "On Track"

        if not is_mile:
            delay_counts[delay_status] = delay_counts.get(delay_status, 0) + 1

        # Float status
        tf = u_task.get("total_float_days", 0.0)
        if status != "Completed":
            if tf <= 0:
                float_dist["Critical (<=0d)"] += 1
            elif tf <= 10:
                float_dist["Near Critical (1-10d)"] += 1
            else:
                float_dist["Healthy (>10d)"] += 1

        # Progress Earned Weight calculation
        total_weight += weight
        planned_earned_weight += weight * (planned_pct / 100.0)
        actual_earned_weight += weight * (actual_pct / 100.0)

        # WBS rollup
        wid = u_task.get("wbs_id", "")
        w_level = u_task.get("wbs_level", 1)
        all_wbs_levels.add(w_level)

        if wid in wbs_map:
            wentry = wbs_map[wid]
            wentry["weight"] += weight
            wentry["planned_earned_dur"] += weight * (planned_pct / 100.0)
            wentry["actual_earned_dur"] += weight * (actual_pct / 100.0)
            wentry["total_tasks"] += 1
            if status == "Completed":
                wentry["completed_tasks"] += 1
            elif status == "In Progress":
                wentry["in_progress_tasks"] += 1
            if u_task.get("is_critical", False):
                wentry["critical_tasks"] += 1
            if delay_status == "Delayed":
                wentry["delayed_tasks"] += 1

        # Area / Zone grouping
        a_code = u_task.get("area_code", "ZONE-GEN")
        a_name = u_task.get("area_name", "General Site")
        if a_code not in areas_map:
            areas_map[a_code] = {
                "area_code": a_code,
                "area_name": a_name,
                "weight": 0.0,
                "planned_earned_dur": 0.0,
                "actual_earned_dur": 0.0,
                "total_tasks": 0,
                "completed_tasks": 0,
                "in_progress_tasks": 0,
                "critical_tasks": 0,
                "delayed_tasks": 0,
                "target_cost": 0.0,
                "act_cost": 0.0,
            }
        a_entry = areas_map[a_code]
        a_entry["weight"] += weight
        a_entry["planned_earned_dur"] += weight * (planned_pct / 100.0)
        a_entry["actual_earned_dur"] += weight * (actual_pct / 100.0)
        a_entry["total_tasks"] += 1
        a_entry["target_cost"] += u_task.get("target_cost", 0.0)
        a_entry["act_cost"] += u_task.get("act_cost", 0.0)
        if status == "Completed":
            a_entry["completed_tasks"] += 1
        elif status == "In Progress":
            a_entry["in_progress_tasks"] += 1
        if u_task.get("is_critical", False):
            a_entry["critical_tasks"] += 1
        if delay_status == "Delayed":
            a_entry["delayed_tasks"] += 1

        # Collect Activity Code values for filtering
        for code_type, code_obj in u_task.get("activity_codes", {}).items():
            if code_type not in activity_code_filters:
                activity_code_filters[code_type] = set()
            val = code_obj.get("code_val") or code_obj.get("code_name")
            if val:
                activity_code_filters[code_type].add(val)

        act_record = {
            "task_id": u_task.get("task_id", ""),
            "task_code": task_code,
            "task_name": u_task.get("task_name", ""),
            "wbs_id": wid,
            "wbs_name": u_task.get("wbs_name", ""),
            "wbs_code": u_task.get("wbs_code", ""),
            "wbs_level": w_level,
            "wbs_path": u_task.get("wbs_path", ""),
            "area_code": a_code,
            "area_name": a_name,
            "activity_codes": u_task.get("activity_codes", {}),
            "is_milestone": is_mile,
            "status": status,
            "weight": weight,
            "planned_pct": round(planned_pct, 1),
            "actual_pct": round(actual_pct, 1),
            "pct_variance": round(actual_pct - planned_pct, 1),
            "bl_start": format_date(bl_start),
            "bl_end": format_date(bl_end),
            "curr_start": format_date(up_start),
            "curr_end": format_date(up_end),
            "start_var_days": start_var_days,
            "finish_var_days": finish_var_days,
            "total_float_days": tf,
            "is_critical": u_task.get("is_critical", False),
            "delay_status": delay_status,
            "target_cost": u_task.get("target_cost", 0.0),
            "act_cost": u_task.get("act_cost", 0.0),
            "target_durn_days": u_task.get("target_durn_days", 0.0),
            "remain_durn_days": u_task.get("remain_durn_days", 0.0),
            "resources": u_task.get("resources", []),
        }
        matched_activities.append(act_record)

        if is_mile:
            milestones.append(act_record)
        else:
            if u_task.get("is_critical", False):
                critical_activities.append(act_record)
            if delay_status == "Delayed":
                delayed_activities.append(act_record)

    # Calculate overall project %
    overall_planned = round((planned_earned_weight / total_weight * 100.0), 2) if total_weight > 0 else 0.0
    overall_actual = round((actual_earned_weight / total_weight * 100.0), 2) if total_weight > 0 else 0.0
    schedule_variance_pct = round(overall_actual - overall_planned, 2)
    spi = round(overall_actual / overall_planned, 2) if overall_planned > 0 else 1.00

    # Calculate WBS rollups
    wbs_summary = []
    for wid, wentry in wbs_map.items():
        if wentry["total_tasks"] > 0:
            w_weight = wentry["weight"]
            w_plan = round((wentry["planned_earned_dur"] / w_weight * 100.0), 1) if w_weight > 0 else 0.0
            w_act = round((wentry["actual_earned_dur"] / w_weight * 100.0), 1) if w_weight > 0 else 0.0
            wentry["planned_pct"] = w_plan
            wentry["actual_pct"] = w_act
            wentry["var_pct"] = round(w_act - w_plan, 1)
            wbs_summary.append(wentry)

    # Sort WBS summary by wbs_id
    wbs_summary.sort(key=lambda x: str(x["wbs_id"]))

    # Calculate Area / Zone Rollups for the Interactive Layout
    areas_summary = []
    for acode, aentry in areas_map.items():
        a_weight = aentry["weight"]
        a_plan = round((aentry["planned_earned_dur"] / a_weight * 100.0), 1) if a_weight > 0 else 0.0
        a_act = round((aentry["actual_earned_dur"] / a_weight * 100.0), 1) if a_weight > 0 else 0.0
        a_spi = round(a_act / a_plan, 2) if a_plan > 0 else 1.0
        aentry["planned_pct"] = a_plan
        aentry["actual_pct"] = a_act
        aentry["var_pct"] = round(a_act - a_plan, 1)
        aentry["spi"] = a_spi
        aentry["status_indicator"] = (
            "Ahead" if a_act > a_plan + 2.0 else ("Behind" if a_act < a_plan - 2.0 else "On Track")
        )
        areas_summary.append(aentry)
    areas_summary.sort(key=lambda x: x["area_code"])

    # Sort activities
    matched_activities.sort(key=lambda x: x["curr_start"])
    critical_activities.sort(key=lambda x: (x["total_float_days"], x["curr_start"]))
    delayed_activities.sort(key=lambda x: -x["finish_var_days"])
    milestones.sort(key=lambda x: x["curr_end"])

    # Generate Weekly S-Curve Data Points
    scurve = _generate_scurve(matched_activities, b_start, b_finish, u_finish, data_date)

    # Generate Lookahead Schedule (Active or Starting within 30 days of data date)
    lookahead_30 = _generate_lookahead(matched_activities, data_date, days_window=30)
    lookahead_60 = _generate_lookahead(matched_activities, data_date, days_window=60)

    # Generate Manpower Profiles (Labor Trade breakdown & monthly hours histogram)
    manpower_data = _generate_manpower_profile(matched_activities, b_start, u_finish, data_date)

    # Generate Cashflow Profiles (Monthly spend histogram & cumulative planned vs actual cashflow)
    cashflow_data = _generate_cashflow_profile(matched_activities, b_start, b_finish, u_finish, data_date)

    # Generate Contractor Payment Applications / Invoices
    invoice_data = _generate_invoices(cashflow_data, data_date)

    # Format Filter Metadata
    filters_meta = {
        "wbs_levels": sorted(list(all_wbs_levels)),
        "activity_codes": {k: sorted(list(v)) for k, v in activity_code_filters.items()},
        "areas": [{"code": a["area_code"], "name": a["area_name"]} for a in areas_summary],
        "statuses": ["Completed", "In Progress", "Not Started"],
    }

    total_contract_value = sum(a["target_cost"] for a in matched_activities)
    earned_value_cost = sum(a["target_cost"] * (a["actual_pct"] / 100.0) for a in matched_activities)
    actual_cost_spent = sum(a["act_cost"] for a in matched_activities)
    cpi = round(earned_value_cost / actual_cost_spent, 2) if actual_cost_spent > 0 else 1.0

    return {
        "project_info": {
            "project_name": update.project_info.get("proj_short_name") or baseline.project_info.get("proj_short_name", "Construction Project"),
            "baseline_name": baseline.project_info.get("proj_short_name", "Baseline"),
            "update_name": update.project_info.get("proj_short_name", "Current Update"),
            "data_date": format_date(data_date),
            "baseline_start": format_date(b_start),
            "baseline_finish": format_date(b_finish),
            "forecast_finish": format_date(u_finish),
            "schedule_slippage_days": project_variance_days,
            "contract_value": round(total_contract_value, 2),
            "earned_value": round(earned_value_cost, 2),
            "actual_cost": round(actual_cost_spent, 2),
            "cpi": cpi,
        },
        "summary": {
            "overall_progress_planned": overall_planned,
            "overall_progress_actual": overall_actual,
            "schedule_variance_pct": schedule_variance_pct,
            "spi": spi,
            "cpi": cpi,
            "status_indicator": "Behind Schedule" if overall_actual < overall_planned - 2.0 else ("Ahead of Schedule" if overall_actual > overall_planned + 2.0 else "On Track"),
            "total_activities": len(matched_activities),
            "total_milestones": len(milestones),
            "status_counts": status_counts,
            "delay_counts": delay_counts,
            "float_dist": float_dist,
        },
        "wbs_summary": wbs_summary,
        "areas_summary": areas_summary,
        "filters_meta": filters_meta,
        "milestones": milestones,
        "critical_activities": critical_activities,
        "delayed_activities": delayed_activities,
        "lookahead": {
            "lookahead_30": lookahead_30,
            "lookahead_60": lookahead_60,
        },
        "manpower": manpower_data,
        "cashflow": cashflow_data,
        "invoice": invoice_data,
        "all_activities": matched_activities,
        "scurve": scurve,
    }


def _calc_planned_pct(start_dt: Optional[datetime], finish_dt: Optional[datetime], data_dt: datetime) -> float:
    """Calculates linear planned progress % of an activity at data date."""
    if not start_dt or not finish_dt:
        return 0.0
    if data_dt < start_dt:
        return 0.0
    if data_dt >= finish_dt:
        return 100.0
    total_duration = (finish_dt - start_dt).total_seconds()
    if total_duration <= 0:
        return 100.0
    elapsed = (data_dt - start_dt).total_seconds()
    pct = (elapsed / total_duration) * 100.0
    return min(max(pct, 0.0), 100.0)


def _find_min_start(tasks: Dict[str, Dict[str, Any]]) -> Optional[datetime]:
    dates = []
    for t in tasks.values():
        d = t.get("target_start") or t.get("early_start") or t.get("act_start")
        if d:
            dates.append(d)
    return min(dates) if dates else None


def _find_max_finish(tasks: Dict[str, Dict[str, Any]]) -> Optional[datetime]:
    dates = []
    for t in tasks.values():
        d = t.get("act_end") or t.get("early_end") or t.get("target_end")
        if d:
            dates.append(d)
    return max(dates) if dates else None


def _generate_scurve(
    activities: List[Dict[str, Any]],
    start_dt: Optional[datetime],
    bl_finish_dt: Optional[datetime],
    fc_finish_dt: Optional[datetime],
    data_date: datetime,
) -> Dict[str, Any]:
    """
    Generates weekly time intervals from start to finish.
    Calculates cumulative Planned % and Actual/Forecast % for each interval.
    """
    if not start_dt:
        start_dt = data_date - timedelta(days=60)
    end_dt = max(
        [d for d in [bl_finish_dt, fc_finish_dt, data_date] if d is not None] or [start_dt + timedelta(days=180)]
    )

    curr = start_dt - timedelta(days=start_dt.weekday())

    labels: List[str] = []
    planned_curve: List[float] = []
    actual_curve: List[Optional[float]] = []
    forecast_curve: List[Optional[float]] = []

    total_weight = sum(a["weight"] for a in activities if not a["is_milestone"])
    if total_weight <= 0:
        total_weight = 1.0

    while curr <= end_dt + timedelta(days=7):
        labels.append(curr.strftime("%d-%b-%Y"))
        is_past_or_current = curr <= (data_date + timedelta(days=4))

        p_earned = 0.0
        a_earned = 0.0
        f_earned = 0.0

        for a in activities:
            if a["is_milestone"]:
                continue
            w = a["weight"]

            bl_s = datetime.strptime(a["bl_start"], "%Y-%m-%d") if a["bl_start"] != "-" else None
            bl_e = datetime.strptime(a["bl_end"], "%Y-%m-%d") if a["bl_end"] != "-" else None
            p_pct = _calc_planned_pct(bl_s, bl_e, curr)
            p_earned += w * (p_pct / 100.0)

            cur_s = datetime.strptime(a["curr_start"], "%Y-%m-%d") if a["curr_start"] != "-" else None
            cur_e = datetime.strptime(a["curr_end"], "%Y-%m-%d") if a["curr_end"] != "-" else None

            if is_past_or_current:
                if a["status"] == "Completed":
                    act_end = cur_e
                    if act_end and curr >= act_end:
                        a_pct = 100.0
                    elif cur_s and curr >= cur_s and act_end:
                        a_pct = _calc_planned_pct(cur_s, act_end, curr)
                    else:
                        a_pct = 0.0
                elif a["status"] == "In Progress":
                    if cur_s and curr >= cur_s:
                        ratio = min(max((curr - cur_s).total_seconds() / max((data_date - cur_s).total_seconds(), 1.0), 0.0), 1.0)
                        a_pct = a["actual_pct"] * ratio
                    else:
                        a_pct = 0.0
                else:
                    a_pct = 0.0
                a_earned += w * (a_pct / 100.0)

            f_pct = _calc_planned_pct(cur_s, cur_e, curr)
            if curr <= data_date:
                f_pct = a["actual_pct"] if a["status"] == "Completed" or (a["status"] == "In Progress" and curr >= data_date - timedelta(days=3)) else f_pct
            f_earned += w * (f_pct / 100.0)

        cum_plan = round((p_earned / total_weight) * 100.0, 1)
        planned_curve.append(min(cum_plan, 100.0))

        if is_past_or_current:
            cum_act = round((a_earned / total_weight) * 100.0, 1)
            actual_curve.append(min(cum_act, 100.0))
            forecast_curve.append(None)
        else:
            actual_curve.append(None)
            cum_fc = round((f_earned / total_weight) * 100.0, 1)
            forecast_curve.append(min(cum_fc, 100.0))

        curr += timedelta(days=7)

    for i in range(len(actual_curve) - 1, -1, -1):
        if actual_curve[i] is not None:
            forecast_curve[i] = actual_curve[i]
            break

    return {
        "labels": labels,
        "planned": planned_curve,
        "actual": actual_curve,
        "forecast": forecast_curve,
    }


def _generate_lookahead(activities: List[Dict[str, Any]], data_date: datetime, days_window: int = 30) -> List[Dict[str, Any]]:
    """Filters activities active or scheduled to start within lookahead window."""
    window_end = data_date + timedelta(days=days_window)
    lookahead_list = []

    for a in activities:
        cur_s = datetime.strptime(a["curr_start"], "%Y-%m-%d") if a["curr_start"] != "-" else None
        cur_e = datetime.strptime(a["curr_end"], "%Y-%m-%d") if a["curr_end"] != "-" else None

        if not cur_s or not cur_e:
            continue

        # In progress or starts within the window
        is_active = (cur_s <= data_date and cur_e >= data_date) or a["status"] == "In Progress"
        starts_in_window = (cur_s >= data_date and cur_s <= window_end)

        if is_active or starts_in_window:
            # Days remaining or days until start
            if cur_s > data_date:
                window_status = f"Starts in {(cur_s - data_date).days} days"
            else:
                window_status = f"In Progress ({a['actual_pct']}%)"

            lookahead_list.append({
                **a,
                "window_status": window_status,
                "lookahead_days": days_window,
            })

    lookahead_list.sort(key=lambda x: (x["is_critical"] is False, x["curr_start"]))
    return lookahead_list


def _generate_manpower_profile(
    activities: List[Dict[str, Any]],
    start_dt: datetime,
    end_dt: datetime,
    data_date: datetime
) -> Dict[str, Any]:
    """
    Computes monthly manpower labor hour histogram and craft distribution.
    """
    # Group by calendar month
    months: List[str] = []
    month_starts: List[datetime] = []

    curr = datetime(start_dt.year, start_dt.month, 1)
    target_end = end_dt or (data_date + timedelta(days=180))

    while curr <= target_end + timedelta(days=31):
        m_str = curr.strftime("%b %Y")
        months.append(m_str)
        month_starts.append(curr)
        # Advance 1 month
        if curr.month == 12:
            curr = datetime(curr.year + 1, 1, 1)
        else:
            curr = datetime(curr.year, curr.month + 1, 1)

    # Monthly hours
    planned_hours = [0.0] * len(months)
    actual_hours = [0.0] * len(months)
    peak_manpower = [0] * len(months)

    # Craft breakdown
    craft_totals: Dict[str, Dict[str, float]] = {
        "Civil & Structural": {"planned": 0.0, "actual": 0.0},
        "MEP Trades": {"planned": 0.0, "actual": 0.0},
        "Finishes & Facade": {"planned": 0.0, "actual": 0.0},
        "Supervision & PM": {"planned": 0.0, "actual": 0.0},
    }

    for a in activities:
        if a["is_milestone"]:
            continue
        dur_days = max(a["target_durn_days"], 1.0)
        tot_hrs = dur_days * 8.0 * 5.0  # estimate 5 workers on average per task

        # Determine craft
        name_up = a["task_name"].upper()
        wbs_up = a["wbs_name"].upper()
        if "MEP" in name_up or "MEP" in wbs_up or "HVAC" in name_up or "ELECTRICAL" in name_up or "PLUMBING" in name_up:
            craft = "MEP Trades"
        elif "FINISH" in name_up or "GLAZING" in name_up or "PAINT" in name_up or "FLOOR" in name_up or "CURTAIN" in name_up:
            craft = "Finishes & Facade"
        elif "SURVEY" in name_up or "PERMIT" in name_up or "COMMISSION" in name_up or "INSPECT" in name_up or "BIM" in name_up:
            craft = "Supervision & PM"
        else:
            craft = "Civil & Structural"

        craft_totals[craft]["planned"] += tot_hrs
        craft_totals[craft]["actual"] += tot_hrs * (a["actual_pct"] / 100.0)

        # Distribute into months
        bl_s = datetime.strptime(a["bl_start"], "%Y-%m-%d") if a["bl_start"] != "-" else start_dt
        bl_e = datetime.strptime(a["bl_end"], "%Y-%m-%d") if a["bl_end"] != "-" else bl_s + timedelta(days=int(dur_days))

        for idx, m_start in enumerate(month_starts):
            m_end = (datetime(m_start.year + 1, 1, 1) if m_start.month == 12 else datetime(m_start.year, m_start.month + 1, 1))
            # Overlap with month
            overlap_s = max(bl_s, m_start)
            overlap_e = min(bl_e, m_end)
            if overlap_e > overlap_s:
                ratio = (overlap_e - overlap_s).total_seconds() / max((bl_e - bl_s).total_seconds(), 1.0)
                m_hrs = tot_hrs * ratio
                planned_hours[idx] += round(m_hrs, 1)

                # Actual hours if past month
                if m_start <= data_date:
                    cur_s = datetime.strptime(a["curr_start"], "%Y-%m-%d") if a["curr_start"] != "-" else bl_s
                    cur_e = datetime.strptime(a["curr_end"], "%Y-%m-%d") if a["curr_end"] != "-" else bl_e
                    o_act_s = max(cur_s, m_start)
                    o_act_e = min(cur_e, m_end)
                    if o_act_e > o_act_s:
                        act_ratio = (o_act_e - o_act_s).total_seconds() / max((cur_e - cur_s).total_seconds(), 1.0)
                        actual_hours[idx] += round((tot_hrs * (a["actual_pct"] / 100.0)) * act_ratio, 1)

    # Convert hours to average headcount (assume 200 working hours per worker per month)
    headcount_planned = [round(h / 200.0, 1) for h in planned_hours]
    headcount_actual = [round(h / 200.0, 1) if h > 0 else (headcount_planned[i] if month_starts[i] <= data_date else 0.0) for i, h in enumerate(actual_hours)]

    total_planned_hours = round(sum(planned_hours), 0)
    total_actual_hours = round(sum(actual_hours), 0)

    return {
        "months": months,
        "planned_hours": [round(h, 0) for h in planned_hours],
        "actual_hours": [round(h, 0) for h in actual_hours],
        "headcount_planned": headcount_planned,
        "headcount_actual": headcount_actual,
        "total_planned_hours": total_planned_hours,
        "total_actual_hours": total_actual_hours,
        "peak_labor": max(headcount_planned) if headcount_planned else 0,
        "craft_distribution": [
            {
                "craft": k,
                "planned_hours": round(v["planned"], 0),
                "actual_hours": round(v["actual"], 0),
                "percent_complete": round((v["actual"] / v["planned"] * 100.0), 1) if v["planned"] > 0 else 0.0,
            }
            for k, v in craft_totals.items()
        ],
    }


def _generate_cashflow_profile(
    activities: List[Dict[str, Any]],
    start_dt: datetime,
    bl_end_dt: datetime,
    end_dt: datetime,
    data_date: datetime
) -> Dict[str, Any]:
    """
    Computes monthly and cumulative cashflow spend curve and forecast.
    """
    months: List[str] = []
    month_starts: List[datetime] = []

    curr = datetime(start_dt.year, start_dt.month, 1)
    target_end = max([d for d in [bl_end_dt, end_dt, data_date] if d is not None] or [start_dt + timedelta(days=240)])

    while curr <= target_end + timedelta(days=31):
        months.append(curr.strftime("%b %Y"))
        month_starts.append(curr)
        if curr.month == 12:
            curr = datetime(curr.year + 1, 1, 1)
        else:
            curr = datetime(curr.year, curr.month + 1, 1)

    monthly_planned = [0.0] * len(months)
    monthly_actual = [0.0] * len(months)

    total_cost = sum(a["target_cost"] for a in activities if not a["is_milestone"])
    if total_cost <= 0:
        total_cost = 1000000.0

    for a in activities:
        if a["is_milestone"]:
            continue
        cost = a["target_cost"] if a["target_cost"] > 0 else 20000.0

        bl_s = datetime.strptime(a["bl_start"], "%Y-%m-%d") if a["bl_start"] != "-" else start_dt
        bl_e = datetime.strptime(a["bl_end"], "%Y-%m-%d") if a["bl_end"] != "-" else bl_s + timedelta(days=20)

        cur_s = datetime.strptime(a["curr_start"], "%Y-%m-%d") if a["curr_start"] != "-" else bl_s
        cur_e = datetime.strptime(a["curr_end"], "%Y-%m-%d") if a["curr_end"] != "-" else bl_e

        for idx, m_start in enumerate(month_starts):
            m_end = (datetime(m_start.year + 1, 1, 1) if m_start.month == 12 else datetime(m_start.year, m_start.month + 1, 1))

            # Planned monthly spend
            overlap_s = max(bl_s, m_start)
            overlap_e = min(bl_e, m_end)
            if overlap_e > overlap_s:
                ratio = (overlap_e - overlap_s).total_seconds() / max((bl_e - bl_s).total_seconds(), 1.0)
                monthly_planned[idx] += round(cost * ratio, 2)

            # Actual / Forecast spend
            if m_start <= data_date:
                o_act_s = max(cur_s, m_start)
                o_act_e = min(cur_e, m_end)
                if o_act_e > o_act_s:
                    act_ratio = (o_act_e - o_act_s).total_seconds() / max((cur_e - cur_s).total_seconds(), 1.0)
                    monthly_actual[idx] += round((cost * (a["actual_pct"] / 100.0)) * act_ratio, 2)

    # Compute cumulative cashflows
    cum_planned = []
    p_acc = 0.0
    for v in monthly_planned:
        p_acc += v
        cum_planned.append(round(p_acc, 2))

    cum_actual = []
    a_acc = 0.0
    for i, v in enumerate(monthly_actual):
        if month_starts[i] <= data_date:
            a_acc += v
            cum_actual.append(round(a_acc, 2))
        else:
            cum_actual.append(None)

    return {
        "months": months,
        "monthly_planned": [round(x, 2) for x in monthly_planned],
        "monthly_actual": [round(x, 2) for x in monthly_actual],
        "cum_planned": cum_planned,
        "cum_actual": cum_actual,
        "total_budget": round(p_acc, 2),
        "total_spent_to_date": round(a_acc, 2),
    }


def _generate_invoices(cashflow_data: Dict[str, Any], data_date: datetime) -> List[Dict[str, Any]]:
    """
    Generates realistic progress billing certificates (Interim Payment Certificates - IPC).
    """
    invoices = []
    cum_prev_certified = 0.0

    for idx, (month_name, act_val) in enumerate(zip(cashflow_data["months"], cashflow_data["monthly_actual"])):
        # Stop after data date months
        if act_val <= 0 and idx > 3:
            continue

        ipc_no = idx + 1
        gross_this_period = max(act_val, 15000.0) if idx <= 2 or act_val > 0 else 0.0
        if gross_this_period <= 0:
            continue

        retention_10pct = round(gross_this_period * 0.10, 2)
        advance_recovery_5pct = round(gross_this_period * 0.05, 2)
        net_certified = round(gross_this_period - retention_10pct - advance_recovery_5pct, 2)
        cum_prev_certified += net_certified

        status = "Paid" if idx < 2 else ("Certified - Pending Payment" if idx == 2 else "Under Review")

        invoices.append({
            "ipc_number": f"IPC-{ipc_no:02d}",
            "period": month_name,
            "gross_amount": gross_this_period,
            "retention": retention_10pct,
            "advance_recovery": advance_recovery_5pct,
            "net_payable": net_certified,
            "cum_certified": round(cum_prev_certified, 2),
            "status": status,
        })

    return invoices
