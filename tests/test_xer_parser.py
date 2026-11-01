"""
Unit tests for Primavera P6 XER Parser
"""

import unittest
import os
from src.xer_parser import parse_xer, parse_xer_content, parse_date, to_float, to_int

SAMPLE_XER = """ERMHDR\t8.3\t2024-03-29\tPROJECT
%T\tPROJECT
%F\tproj_id\tproj_short_name\tlast_recalc_date\tplan_start_date\tplan_end_date
%R\t999\tTest Bridge Project\t2024-04-01 08:00\t2024-04-01 08:00\t2024-12-31 17:00
%T\tPROJWBS
%F\twbs_id\tproj_id\tparent_wbs_id\twbs_name\tseq_num
%R\t10\t999\t\tSubstructure\t1
%R\t20\t999\t\tSuperstructure\t2
%T\tTASK
%F\ttask_id\tproj_id\twbs_id\ttask_code\ttask_name\ttask_type\tstatus_code\ttarget_durn_hr_cnt\tphys_complete_pct\ttotal_float_hr_cnt\ttarget_start_date\ttarget_end_date\tact_start_date\tact_end_date
%R\t1\t999\t10\tACT-01\tExcavation Works\tTT_Task\tTK_Complete\t80.0\t100.0\t0.0\t2024-04-01 08:00\t2024-04-12 17:00\t2024-04-01 08:00\t2024-04-12 17:00
%R\t2\t999\t10\tACT-02\tPiling Works\tTT_Task\tTK_Active\t160.0\t50.0\t-16.0\t2024-04-15 08:00\t2024-05-10 17:00\t2024-04-15 08:00\t
%R\t3\t999\t20\tMILE-01\tPier Milestone\tTT_Mile\tTK_NotStart\t0.0\t0.0\t-16.0\t2024-05-10 17:00\t2024-05-10 17:00\t\t
%E
"""

class TestXERParser(unittest.TestCase):

    def test_parse_date(self):
        dt = parse_date("2024-05-15 08:30")
        self.assertIsNotNone(dt)
        self.assertEqual(dt.year, 2024)
        self.assertEqual(dt.month, 5)
        self.assertEqual(dt.day, 15)
        self.assertEqual(dt.hour, 8)

        # Empty & invalid date strings
        self.assertIsNone(parse_date(None))
        self.assertIsNone(parse_date(""))
        self.assertIsNone(parse_date("not-a-date"))

    def test_to_float_and_int(self):
        self.assertEqual(to_float("12.5"), 12.5)
        self.assertEqual(to_float(None, 3.0), 3.0)
        self.assertEqual(to_float("invalid", 4.0), 4.0)

        self.assertEqual(to_int("42"), 42)
        self.assertEqual(to_int("42.7"), 42)
        self.assertEqual(to_int(None, 7), 7)

    def test_parse_xer_content(self):
        data = parse_xer_content(SAMPLE_XER, "test.xer")
        self.assertEqual(data.project_info["proj_id"], "999")
        self.assertEqual(data.project_info["proj_short_name"], "Test Bridge Project")
        self.assertIn("10", data.wbs)
        self.assertIn("20", data.wbs)
        self.assertEqual(len(data.tasks), 3)

        act1 = data.tasks["ACT-01"]
        self.assertEqual(act1["status"], "Completed")
        self.assertEqual(act1["pct_comp"], 100.0)
        self.assertEqual(act1["target_durn_days"], 10.0)

        act2 = data.tasks["ACT-02"]
        self.assertEqual(act2["status"], "In Progress")
        self.assertEqual(act2["pct_comp"], 50.0)
        self.assertEqual(act2["total_float_days"], -2.0)
        self.assertTrue(act2["is_critical"])

        mile = data.tasks["MILE-01"]
        self.assertTrue(mile["is_milestone"])

    def test_sample_files_exist_and_parse(self):
        b_path = "samples/baseline_project.xer"
        u_path = "samples/update_week12.xer"
        self.assertTrue(os.path.exists(b_path))
        self.assertTrue(os.path.exists(u_path))

        b_proj = parse_xer(b_path)
        u_proj = parse_xer(u_path)

        self.assertEqual(len(b_proj.tasks), 27)
        self.assertEqual(len(u_proj.tasks), 27)
        self.assertGreater(len(b_proj.relationships), 20)

if __name__ == "__main__":
    unittest.main()
