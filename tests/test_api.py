"""
Integration tests for FastAPI endpoints
"""

import unittest
from fastapi.testclient import TestClient
from src.app import app

class TestAppEndpoints(unittest.TestCase):

    def setUp(self):
        self.client = TestClient(app)

    def test_get_root(self):
        res = self.client.get("/")
        self.assertEqual(res.status_code, 200)
        self.assertIn("Primavera P6 Continuance Reporting", res.text)
        self.assertIn("Upload & Set Baseline", res.text)
        self.assertIn("Upload Weekly Update", res.text)

    def test_get_sample_dashboard(self):
        res = self.client.get("/api/sample-dashboard")
        self.assertEqual(res.status_code, 200)
        self.assertIn("Project S-Curve", res.text)
        self.assertIn("Marina Commercial Tower", res.text)

    def test_post_generate_dashboard(self):
        with open("samples/baseline_project.xer", "rb") as fb, open("samples/update_week12.xer", "rb") as fu:
            files = {
                "baseline": ("baseline.xer", fb, "application/octet-stream"),
                "update": ("update.xer", fu, "application/octet-stream"),
            }
            res = self.client.post("/api/generate-dashboard", files=files)
            self.assertEqual(res.status_code, 200)
            self.assertIn("Project S-Curve", res.text)

    def test_post_analyze_json(self):
        with open("samples/baseline_project.xer", "rb") as fb, open("samples/update_week12.xer", "rb") as fu:
            files = {
                "baseline": ("baseline.xer", fb, "application/octet-stream"),
                "update": ("update.xer", fu, "application/octet-stream"),
            }
            res = self.client.post("/api/analyze-json", files=files)
            self.assertEqual(res.status_code, 200)
            data = res.json()
            self.assertIn("summary", data)
            self.assertEqual(data["summary"]["spi"], 0.85)


    def test_baseline_and_update_continuance_workflow(self):
        from src.app import app_state
        # Reset state
        app_state["baseline"] = None
        app_state["baseline_meta"] = None
        app_state["updates"] = []

        # Check initial baseline is empty
        res = self.client.get("/api/baseline")
        self.assertEqual(res.status_code, 200)
        self.assertFalse(res.json()["has_baseline"])

        # Attempting to upload update without baseline should fail
        with open("samples/update_week12.xer", "rb") as fu:
            files = {"update": ("update_week12.xer", fu, "application/octet-stream")}
            res = self.client.post("/api/upload-update", files=files)
            self.assertEqual(res.status_code, 400)
            self.assertIn("No baseline schedule active", res.json()["detail"])

        # Upload baseline
        with open("samples/baseline_project.xer", "rb") as fb:
            files = {"baseline": ("baseline_project.xer", fb, "application/octet-stream")}
            res = self.client.post("/api/upload-baseline", files=files)
            self.assertEqual(res.status_code, 200)
            data = res.json()
            self.assertEqual(data["status"], "success")
            self.assertEqual(data["baseline"]["filename"], "baseline_project.xer")

        # Check baseline state
        res = self.client.get("/api/baseline")
        self.assertEqual(res.status_code, 200)
        self.assertTrue(res.json()["has_baseline"])

        # Upload weekly update 1
        with open("samples/update_week12.xer", "rb") as fu:
            files = {"update": ("update_week12.xer", fu, "application/octet-stream")}
            res = self.client.post("/api/upload-update", files=files)
            self.assertEqual(res.status_code, 200)
            up_data = res.json()
            self.assertEqual(up_data["status"], "success")
            self.assertEqual(up_data["update_id"], 1)
            self.assertEqual(up_data["summary"]["spi"], 0.85)

        # Upload weekly update 2 for continuance
        with open("samples/update_week12.xer", "rb") as fu:
            files = {"update": ("update_week13.xer", fu, "application/octet-stream")}
            res = self.client.post("/api/upload-update", files=files)
            self.assertEqual(res.status_code, 200)
            up_data2 = res.json()
            self.assertEqual(up_data2["update_id"], 2)

        # Fetch updates list
        res = self.client.get("/api/updates")
        self.assertEqual(res.status_code, 200)
        updates_list = res.json()["updates"]
        self.assertEqual(len(updates_list), 2)
        self.assertEqual(updates_list[0]["id"], 1)
        self.assertEqual(updates_list[1]["id"], 2)

        # View update dashboard
        res = self.client.get("/api/dashboard/update/1")
        self.assertEqual(res.status_code, 200)
        self.assertIn("Project S-Curve", res.text)

        # Dashboard for non-existent update
        res = self.client.get("/api/dashboard/update/999")
        self.assertEqual(res.status_code, 404)

    def test_load_sample_flow(self):
        res = self.client.post("/api/load-sample")
        self.assertEqual(res.status_code, 200)
        data = res.json()
        self.assertEqual(data["status"], "success")
        self.assertIn("baseline", data)
        self.assertIn("update_id", data)


if __name__ == "__main__":
    unittest.main()
