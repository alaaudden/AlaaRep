"""
FastAPI Web Application for Primavera P6 Progress Dashboard.
Enables uploading Baseline and Weekly Update XER files,
interactive preview, instant KPI calculation, and HTML report export.
"""

import os
from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.responses import HTMLResponse, JSONResponse
from src.xer_parser import parse_xer_content
from src.progress_analyzer import compare_schedules
from src.dashboard import generate_html_dashboard

app = FastAPI(title="Primavera P6 Progress Report Generator")

INDEX_HTML = """<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Primavera P6 Construction Progress Dashboard</title>
  <script src="https://cdn.tailwindcss.com"></script>
</head>
<body class="bg-slate-50 min-h-screen text-slate-800 flex flex-col items-center justify-center p-4">
  <div class="max-w-xl w-full bg-white rounded-2xl shadow-xl border border-slate-200 p-8 space-y-6">
    <div class="text-center">
      <div class="inline-flex p-3 bg-blue-50 text-blue-600 rounded-xl mb-3">
        <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
        </svg>
      </div>
      <h1 class="text-2xl font-bold text-slate-900">Primavera P6 Progress Dashboard</h1>
      <p class="text-sm text-slate-500 mt-1">Upload your Project Baseline and Weekly Update .XER files</p>
    </div>

    <form id="uploadForm" class="space-y-4" enctype="multipart/form-data">
      <div class="border border-slate-200 rounded-lg p-4 bg-slate-50/50">
        <label class="block text-xs font-bold uppercase text-slate-600 mb-1">1. Baseline Project (.xer)</label>
        <input type="file" id="baselineFile" name="baseline" accept=".xer" required class="block w-full text-xs text-slate-500 file:mr-4 file:py-2 file:px-4 file:rounded-md file:border-0 file:text-xs file:font-semibold file:bg-blue-600 file:text-white hover:file:bg-blue-700 cursor-pointer">
      </div>

      <div class="border border-slate-200 rounded-lg p-4 bg-slate-50/50">
        <label class="block text-xs font-bold uppercase text-slate-600 mb-1">2. Weekly Update (.xer)</label>
        <input type="file" id="updateFile" name="update" accept=".xer" required class="block w-full text-xs text-slate-500 file:mr-4 file:py-2 file:px-4 file:rounded-md file:border-0 file:text-xs file:font-semibold file:bg-blue-600 file:text-white hover:file:bg-blue-700 cursor-pointer">
      </div>

      <button type="submit" id="btnSubmit" class="w-full py-3 px-4 bg-blue-600 hover:bg-blue-700 text-white font-semibold rounded-lg shadow-sm transition flex items-center justify-center space-x-2">
        <span>Generate Progress Dashboard</span>
      </button>
    </form>

    <div class="pt-4 border-t border-slate-100 flex flex-col items-center">
      <span class="text-xs text-slate-400 mb-2">Or test immediately with sample project:</span>
      <button onclick="loadSample()" class="text-xs font-semibold text-blue-600 hover:text-blue-800 underline">
        Load Sample Commercial Tower (Week 12)
      </button>
    </div>
  </div>

  <script>
    document.getElementById('uploadForm').onsubmit = async (e) => {
      e.preventDefault();
      const bFile = document.getElementById('baselineFile').files[0];
      const uFile = document.getElementById('updateFile').files[0];
      if (!bFile || !uFile) return;

      const fd = new FormData();
      fd.append('baseline', bFile);
      fd.append('update', uFile);

      document.getElementById('btnSubmit').innerText = 'Processing Schedules...';
      const res = await fetch('/api/generate-dashboard', {
        method: 'POST',
        body: fd
      });
      if (res.ok) {
        const html = await res.text();
        const win = window.open('', '_blank');
        win.document.write(html);
        win.document.close();
      } else {
        alert('Failed to process XER files.');
      }
      document.getElementById('btnSubmit').innerText = 'Generate Progress Dashboard';
    };

    async function loadSample() {
      const res = await fetch('/api/sample-dashboard');
      if (res.ok) {
        const html = await res.text();
        const win = window.open('', '_blank');
        win.document.write(html);
        win.document.close();
      } else {
        alert('Could not load sample.');
      }
    }
  </script>
</body>
</html>
"""

@app.get("/", response_class=HTMLResponse)
def index():
    return HTMLResponse(content=INDEX_HTML)

@app.get("/api/sample-dashboard", response_class=HTMLResponse)
def sample_dashboard():
    base_path = "samples/baseline_project.xer"
    up_path = "samples/update_week12.xer"
    if not os.path.exists(base_path) or not os.path.exists(up_path):
        raise HTTPException(status_code=404, detail="Sample XER files not found")

    with open(base_path, "r", encoding="utf-8") as f:
        b_content = f.read()
    with open(up_path, "r", encoding="utf-8") as f:
        u_content = f.read()

    b_proj = parse_xer_content(b_content, "baseline.xer")
    u_proj = parse_xer_content(u_content, "update.xer")
    report_data = compare_schedules(b_proj, u_proj)
    html = generate_html_dashboard(report_data)
    return HTMLResponse(content=html)

@app.post("/api/generate-dashboard", response_class=HTMLResponse)
async def generate_dashboard_api(baseline: UploadFile = File(...), update: UploadFile = File(...)):
    b_bytes = await baseline.read()
    u_bytes = await update.read()

    try:
        b_content = b_bytes.decode("utf-8-sig")
    except UnicodeDecodeError:
        b_content = b_bytes.decode("latin-1", errors="replace")

    try:
        u_content = u_bytes.decode("utf-8-sig")
    except UnicodeDecodeError:
        u_content = u_bytes.decode("latin-1", errors="replace")

    b_proj = parse_xer_content(b_content, baseline.filename or "baseline.xer")
    u_proj = parse_xer_content(u_content, update.filename or "update.xer")
    report_data = compare_schedules(b_proj, u_proj)
    html = generate_html_dashboard(report_data)
    return HTMLResponse(content=html)

@app.post("/api/analyze-json")
async def analyze_json_api(baseline: UploadFile = File(...), update: UploadFile = File(...)):
    b_bytes = await baseline.read()
    u_bytes = await update.read()

    try:
        b_content = b_bytes.decode("utf-8-sig")
    except UnicodeDecodeError:
        b_content = b_bytes.decode("latin-1", errors="replace")

    try:
        u_content = u_bytes.decode("utf-8-sig")
    except UnicodeDecodeError:
        u_content = u_bytes.decode("latin-1", errors="replace")

    b_proj = parse_xer_content(b_content, baseline.filename or "baseline.xer")
    u_proj = parse_xer_content(u_content, update.filename or "update.xer")
    report_data = compare_schedules(b_proj, u_proj)
    return JSONResponse(content=report_data)
