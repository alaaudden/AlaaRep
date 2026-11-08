"""
Unit and Integration tests for Progress Analyzer, Dashboard Generator, and CLI
"""

import unittest
import os
import subprocess
from src.xer_parser import parse_xer
from src.progress_analyzer import compare_schedules
from src.dashboard import generate_html_dashboard


class TestProgressAnalyzer(unittest.TestCase):

    def setUp(self):
        self.baseline = parse_xer("samples/baseline_project.xer")
        self.update = parse_xer("samples/update_week12.xer")

    def test_compare_schedules(self):
        result = compare_schedules(self.baseline, self.update)

        self.assertIn("summary", result)
        self.assertIn("project_info", result)
        self.assertIn("wbs_summary", result)
        self.assertIn("milestones", result)
        self.assertIn("critical_activities", result)
        self.assertIn("delayed_activities", result)
        self.assertIn("scurve", result)

        summary = result["summary"]
        self.assertGreater(summary["overall_progress_planned"], 30.0)
        self.assertGreater(summary["overall_progress_actual"], 25.0)
        self.assertEqual(summary["status_indicator"], "Behind Schedule")
        self.assertLess(summary["spi"], 1.0)

        # Slippage check
        self.assertEqual(result["project_info"]["schedule_slippage_days"], 15)

        # S-curve points
        scurve = result["scurve"]
        self.assertGreater(len(scurve["labels"]), 10)
        self.assertEqual(len(scurve["labels"]), len(scurve["planned"]))
        self.assertEqual(len(scurve["labels"]), len(scurve["actual"]))

    def test_html_dashboard_generation(self):
        result = compare_schedules(self.baseline, self.update)
        html = generate_html_dashboard(result)

        self.assertIn("<!DOCTYPE html>", html)
        self.assertIn("Project S-Curve", html)
        self.assertIn("Marina Commercial Tower", html)
        self.assertIn("Key Project Milestones", html)
        self.assertIn("Critical Path & Delayed Activities Watchlist", html)

    def test_cli_execution(self):
        test_out = "dist/test_cli_dashboard.html"
        if os.path.exists(test_out):
            os.remove(test_out)

        cmd = [
            "python3", "main.py",
            "--baseline", "samples/baseline_project.xer",
            "--update", "samples/update_week12.xer",
            "--output", test_out
        ]
        res = subprocess.run(cmd, capture_output=True, text=True)
        self.assertEqual(res.returncode, 0, f"CLI error: {res.stderr}")
        self.assertTrue(os.path.exists(test_out))
        self.assertGreater(os.path.getsize(test_out), 20000)

        if os.path.exists(test_out):
            os.remove(test_out)


if __name__ == "__main__":
    unittest.main()
