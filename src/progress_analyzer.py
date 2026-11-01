"""
Construction Project Progress & Baseline Comparison Analyzer
Compares a Primavera P6 Baseline XER schedule with an Update XER schedule.
Calculates:
- Progress % (Planned vs Actual / Earned)
- Schedule Variance (SV) and Schedule Performance Index (SPI)
- Milestone Variance (Baseline Finish vs Forecast/Actual Finish, Days slippage)
- Critical Path & Total Float Distribution
- Delay Classification (Delayed, On Schedule, Ahead, Completed, Not Started)
- WBS Progress Summary & Breakdown
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
    b_start = baseline.project_info.get("plan_start_date") or _find_min_start(baseline.tasks)
    b_finish = baseline.project_info.get("plan_end_date") or _find_max_finish(baseline.tasks)
    u_finish = update.project_info.get("scd_end_date") or update.project_info.get("plan_end_date") or _find_max_finish(update.tasks)

    # Days variance at project level
    project_variance_days = 0
    if b_finish and u_finish:
        project_variance_days = (u_finish.date() - b_finish.date()).days

    # Map baseline tasks
    b_tasks = baseline.tasks
    u_tasks = update.tasks

    matched_activities: List[Dict[str, Any]] = []
    wbs_map: Dict[str, Dict[str, Any]] = {}

    # Pre-populate WBS structure
    for wid, wobj in update.wbs.items():
        wbs_map[wid] = {
            "wbs_id": wid,
            "wbs_name": wobj.get("wbs_name", "Unknown"),
            "parent_wbs_id": wobj.get("parent_wbs_id", ""),
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

        act_record = {
            "task_code": task_code,
            "task_name": u_task.get("task_name", ""),
            "wbs_name": u_task.get("wbs_name", ""),
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

    # Sort activities
    matched_activities.sort(key=lambda x: x["curr_start"])
    critical_activities.sort(key=lambda x: (x["total_float_days"], x["curr_start"]))
    delayed_activities.sort(key=lambda x: -x["finish_var_days"])
    milestones.sort(key=lambda x: x["curr_end"])

    # Generate Weekly S-Curve Data Points
    scurve = _generate_scurve(matched_activities, b_start, b_finish, u_finish, data_date)

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
        },
        "summary": {
            "overall_progress_planned": overall_planned,
            "overall_progress_actual": overall_actual,
            "schedule_variance_pct": schedule_variance_pct,
            "spi": spi,
            "status_indicator": "Behind Schedule" if overall_actual < overall_planned - 2.0 else ("Ahead of Schedule" if overall_actual > overall_planned + 2.0 else "On Track"),
            "total_activities": len(matched_activities),
            "total_milestones": len(milestones),
            "status_counts": status_counts,
            "delay_counts": delay_counts,
            "float_dist": float_dist,
        },
        "wbs_summary": wbs_summary,
        "milestones": milestones,
        "critical_activities": critical_activities,
        "delayed_activities": delayed_activities,
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

    # Weekly intervals
    curr = start_dt
    # align to start of week (Monday)
    curr = curr - timedelta(days=curr.weekday())

    labels: List[str] = []
    planned_curve: List[float] = []
    actual_curve: List[Optional[float]] = []
    forecast_curve: List[Optional[float]] = []

    # Calculate total weight
    total_weight = sum(a["weight"] for a in activities if not a["is_milestone"])
    if total_weight <= 0:
        total_weight = 1.0

    while curr <= end_dt + timedelta(days=7):
        labels.append(curr.strftime("%d-%b-%Y"))
        is_past_or_current = curr <= (data_date + timedelta(days=4))

        # Planned progress at this date
        p_earned = 0.0
        # Actual progress (up to data date)
        a_earned = 0.0
        # Forecast progress (full timeline)
        f_earned = 0.0

        for a in activities:
            if a["is_milestone"]:
                continue
            w = a["weight"]

            # Baseline planned %
            bl_s = datetime.strptime(a["bl_start"], "%Y-%m-%d") if a["bl_start"] != "-" else None
            bl_e = datetime.strptime(a["bl_end"], "%Y-%m-%d") if a["bl_end"] != "-" else None
            p_pct = _calc_planned_pct(bl_s, bl_e, curr)
            p_earned += w * (p_pct / 100.0)

            # Actual & Forecast
            cur_s = datetime.strptime(a["curr_start"], "%Y-%m-%d") if a["curr_start"] != "-" else None
            cur_e = datetime.strptime(a["curr_end"], "%Y-%m-%d") if a["curr_end"] != "-" else None

            if is_past_or_current:
                # Interpolate actual between actual start and data date
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

            # Forecast % for future
            f_pct = _calc_planned_pct(cur_s, cur_e, curr)
            # Adjust forecast based on current actual progress achieved
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

    # Link the forecast curve seamlessly to the last actual point
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
