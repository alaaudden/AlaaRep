import unittest
from datetime import datetime
from src.xer_parser import parse_xer_content
from src.progress_analyzer import compare_schedules

class TestProgressAnalyzerExtended(unittest.TestCase):
    def test_compare_schedules_areas_and_kpis(self):
        sample_base = """ERMHDR\t8.3\t2024-03-29\tPROJECT
%T\tPROJECT
%F\tproj_id\tproj_short_name\tplan_start_date\tplan_end_date
%R\t1001\tTower Base\t2024-01-08 08:00\t2024-06-30 17:00
%T\tPROJWBS
%F\twbs_id\tproj_id\twbs_short_name\twbs_name
%R\t1\t1001\tSUB\tSubstructure
%R\t2\t1001\tSUP\tSuperstructure
%T\tTASK
%F\ttask_id\tproj_id\twbs_id\ttask_code\ttask_name\ttarget_durn_hr_cnt\tstatus_code\ttarget_start_date\ttarget_end_date
%R\t10\t1001\t1\tACT1\tPiling Work\t80.0\tTK_NotStart\t2024-01-08 08:00\t2024-01-19 17:00
%R\t20\t1001\t2\tACT2\tColumns Work\t160.0\tTK_NotStart\t2024-01-22 08:00\t2024-02-16 17:00
%E
"""
        sample_up = """ERMHDR\t8.3\t2024-03-29\tPROJECT
%T\tPROJECT
%F\tproj_id\tproj_short_name\tlast_recalc_date
%R\t1001\tTower Update\t2024-01-20 17:00
%T\tPROJWBS
%F\twbs_id\tproj_id\twbs_short_name\twbs_name
%R\t1\t1001\tSUB\tSubstructure
%R\t2\t1001\tSUP\tSuperstructure
%T\tTASK
%F\ttask_id\tproj_id\twbs_id\ttask_code\ttask_name\ttarget_durn_hr_cnt\tstatus_code\tact_start_date\tact_end_date\tphys_complete_pct
%R\t10\t1001\t1\tACT1\tPiling Work\t80.0\tTK_Complete\t2024-01-08 08:00\t2024-01-19 17:00\t100.0
%R\t20\t1001\t2\tACT2\tColumns Work\t160.0\tTK_Active\t2024-01-20 08:00\t\t25.0
%E
"""
        b_doc = parse_xer_content(sample_base)
        u_doc = parse_xer_content(sample_up)
        res = compare_schedules(b_doc, u_doc)

        self.assertIn("areas_summary", res)
        self.assertIn("filters_meta", res)
        self.assertIn("lookahead", res)
        self.assertIn("manpower", res)
        self.assertIn("cashflow", res)
        self.assertIn("invoice", res)
        self.assertTrue(len(res["areas_summary"]) > 0)
        self.assertTrue(len(res["invoice"]) > 0)

if __name__ == "__main__":
    unittest.main()
