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
        self.assertIn("Primavera P6 Progress Dashboard", res.text)

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


if __name__ == "__main__":
    unittest.main()
