import unittest
from src.xer_parser import parse_xer_content

class TestXERParserExtended(unittest.TestCase):
    def test_parse_activity_codes_and_resources(self):
        sample_xer = """ERMHDR\t8.3\t2024-03-29\tPROJECT
%T\tPROJECT
%F\tproj_id\tproj_short_name
%R\t1001\tTest Project
%T\tPROJWBS
%F\twbs_id\tproj_id\tparent_wbs_id\twbs_short_name\twbs_name
%R\t1\t1001\t\tROOT\tRoot Project
%R\t2\t1001\t1\tSUB\tSubstructure Works
%T\tACTVTYPE
%F\tactv_code_type_id\tactv_code_type
%R\t10\tArea
%R\t20\tDiscipline
%T\tACTVCODE
%F\tactv_code_id\tactv_code_type_id\tactv_code_val\tactv_code_name
%R\t101\t10\tZONE-A\tSubstructure Zone
%R\t201\t20\tCIVIL\tCivil & Structure
%T\tTASK
%F\ttask_id\tproj_id\twbs_id\ttask_code\ttask_name\ttarget_durn_hr_cnt\tstatus_code
%R\t501\t1001\t2\tACT100\tExcavation Work\t80.0\tTK_Active
%T\tTASKACTV
%F\ttask_id\tactv_code_type_id\tactv_code_id
%R\t501\t10\t101
%R\t501\t20\t201
%T\tRSRC
%F\trsrc_id\trsrc_short_name\trsrc_name\trsrc_type\tcost_rate
%R\t901\tLAB\tGeneral Labor\tRT_Labor\t45.0
%T\tTASKRSRC
%F\ttask_id\trsrc_id\ttarget_qty\ttarget_cost\tact_qty\tact_cost
%R\t501\t901\t160.0\t7200.0\t40.0\t1800.0
%E
"""
        doc = parse_xer_content(sample_xer, "test.xer")
        self.assertEqual(len(doc.activity_code_types), 2)
        self.assertEqual(len(doc.activity_codes), 2)
        self.assertIn("ACT100", doc.tasks)
        task = doc.tasks["ACT100"]
        self.assertEqual(task["area_code"], "ZONE-A")
        self.assertEqual(task["area_name"], "Substructure Zone")
        self.assertEqual(task["wbs_level"], 2)
        self.assertIn("Area", task["activity_codes"])
        self.assertIn("Discipline", task["activity_codes"])
        self.assertEqual(len(task["resources"]), 1)
        self.assertEqual(task["target_cost"], 7200.0)
        self.assertEqual(task["act_cost"], 1800.0)

if __name__ == "__main__":
    unittest.main()
