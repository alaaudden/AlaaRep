"""
Test Standalone Dashboard Generation and In-Browser Client-Side Engine
"""
import os
import pytest
from src.xer_parser import parse_xer
from src.progress_analyzer import compare_schedules
from src.dashboard import generate_html_dashboard


def test_html_dashboard_generation():
    baseline_path = os.path.join(os.path.dirname(__file__), "..", "samples", "baseline_project.xer")
    update_path = os.path.join(os.path.dirname(__file__), "..", "samples", "update_week12.xer")

    b = parse_xer(baseline_path)
    u = parse_xer(update_path)
    data = compare_schedules(b, u)

    html = generate_html_dashboard(data)
    assert "<!DOCTYPE html>" in html
    assert "parseXerProjectClient" in html
    assert "compareSchedulesClient" in html
    assert "uploadDashboardBaseline" in html
    assert "uploadDashboardUpdate" in html
    assert "ZONE-SUB" in html
    assert "Substructure & Foundation" in html
    assert "Lookahead Schedule" in html
    assert "Manpower & Labor" in html
    assert "Cashflow & Spend" in html
    assert "Invoice & Billing (IPC)" in html
