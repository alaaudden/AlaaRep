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

# State management for Active Baseline and Weekly Updates Continuance
app_state = {
    "baseline": None,  # Parsed XERProjectData
    "baseline_meta": None,  # Dict with metadata
    "updates": [],  # List of dicts with {id, filename, upload_time, data_date, summary, project_info, report_data}
}


def _decode_file_bytes(b_bytes: bytes) -> str:
    try:
        return b_bytes.decode("utf-8-sig")
    except UnicodeDecodeError:
        return b_bytes.decode("latin-1", errors="replace")

INDEX_HTML = """<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Primavera P6 Continuance Progress Reporting</title>
  <script src="https://cdn.tailwindcss.com"></script>
  <style>
    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');
    body { font-family: 'Inter', sans-serif; }
  </style>
</head>
<body class="bg-slate-50 min-h-screen text-slate-800 p-4 md:p-8">
  <div class="max-w-5xl mx-auto space-y-6">

    <!-- Header -->
    <header class="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 flex flex-wrap items-center justify-between gap-4">
      <div class="flex items-center space-x-4">
        <div class="p-3 bg-blue-50 text-blue-600 rounded-xl">
          <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
        </div>
        <div>
          <h1 class="text-2xl font-bold text-slate-900">Primavera P6 Continuance Reporting</h1>
          <p class="text-xs text-slate-500 mt-0.5">Upload project baseline once and upload weekly updates continuously to monitor progress & SPI</p>
        </div>
      </div>
      <div>
        <button onclick="loadSampleData()" class="px-4 py-2 border border-slate-200 bg-slate-50 hover:bg-slate-100 text-slate-700 text-xs font-semibold rounded-lg transition shadow-sm flex items-center gap-1.5">
          <svg class="w-4 h-4 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/></svg>
          Load Sample Tower Project
        </button>
      </div>
    </header>

    <!-- Upload Grid: Separate Baseline and Weekly Update upload buttons -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">

      <!-- Box 1: Baseline Upload Card -->
      <div class="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 flex flex-col justify-between">
        <div>
          <div class="flex items-center justify-between mb-2">
            <div class="flex items-center space-x-2">
              <span class="inline-flex items-center justify-center w-6 h-6 rounded-full bg-blue-100 text-blue-700 font-bold text-xs">1</span>
              <h2 class="text-base font-bold text-slate-900">Project Baseline</h2>
            </div>
            <span id="baselineBadge" class="text-[11px] px-2 py-0.5 rounded-full font-medium bg-slate-100 text-slate-600">No Baseline</span>
          </div>
          <p class="text-xs text-slate-500 mb-4">Set your planned targets, initial scopes, and baseline schedule (.xer).</p>

          <div id="baselineInfoCard" class="hidden mb-4 p-3.5 bg-slate-50 rounded-xl border border-slate-200 text-xs space-y-1">
            <div class="font-bold text-slate-800" id="baseProjName">-</div>
            <div class="text-slate-500 flex justify-between"><span>File: <span class="font-medium text-slate-700" id="baseFileName">-</span></span> <span>Activities: <span class="font-medium text-slate-700" id="baseTaskCount">-</span></span></div>
            <div class="text-slate-500 flex justify-between"><span>Target Start: <span class="font-medium text-slate-700" id="baseStartDate">-</span></span> <span>Target Finish: <span class="font-medium text-slate-700" id="baseFinishDate">-</span></span></div>
          </div>

          <form id="baselineForm" class="space-y-3">
            <input type="file" id="baselineFile" accept=".xer" required class="block w-full text-xs text-slate-500 file:mr-3 file:py-2 file:px-3 file:rounded-md file:border-0 file:text-xs file:font-semibold file:bg-slate-100 file:text-slate-700 hover:file:bg-slate-200 cursor-pointer">
            <button type="submit" id="btnUploadBaseline" class="w-full py-2.5 px-4 bg-slate-900 hover:bg-slate-800 text-white font-semibold rounded-lg shadow-sm transition text-xs flex items-center justify-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/></svg>
              <span>Upload & Set Baseline</span>
            </button>
          </form>
        </div>
        <div id="baselineMsg" class="mt-2 text-xs hidden"></div>
      </div>

      <!-- Box 2: Weekly Update Upload Card -->
      <div class="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 flex flex-col justify-between">
        <div>
          <div class="flex items-center justify-between mb-2">
            <div class="flex items-center space-x-2">
              <span class="inline-flex items-center justify-center w-6 h-6 rounded-full bg-emerald-100 text-emerald-700 font-bold text-xs">2</span>
              <h2 class="text-base font-bold text-slate-900">Weekly Progress Update</h2>
            </div>
            <span class="text-[11px] px-2 py-0.5 rounded-full font-medium bg-emerald-50 text-emerald-700">Continuance</span>
          </div>
          <p class="text-xs text-slate-500 mb-4">Upload weekly .xer cut-offs. Each week adds to your progress continuance timeline.</p>

          <form id="updateForm" class="space-y-3">
            <input type="file" id="updateFile" accept=".xer" required class="block w-full text-xs text-slate-500 file:mr-3 file:py-2 file:px-3 file:rounded-md file:border-0 file:text-xs file:font-semibold file:bg-emerald-50 file:text-emerald-700 hover:file:bg-emerald-100 cursor-pointer">
            <button type="submit" id="btnUploadUpdate" class="w-full py-2.5 px-4 bg-emerald-600 hover:bg-emerald-700 text-white font-semibold rounded-lg shadow-sm transition text-xs flex items-center justify-center gap-2">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/></svg>
              <span>Upload Weekly Update</span>
            </button>
          </form>
        </div>
        <div id="updateMsg" class="mt-2 text-xs hidden"></div>
      </div>

    </div>

    <!-- Continuance Reporting Log Table -->
    <div class="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
      <div class="flex items-center justify-between mb-4">
        <div>
          <h2 class="text-lg font-bold text-slate-900">Continuance Reporting Timeline</h2>
          <p class="text-xs text-slate-500">Weekly progression history against active baseline</p>
        </div>
        <button onclick="fetchUpdates()" class="text-xs font-semibold text-blue-600 hover:text-blue-800 flex items-center gap-1">
          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/></svg>
          Refresh Timeline
        </button>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full text-left text-xs border border-slate-100 rounded-lg overflow-hidden" id="updatesTable">
          <thead class="bg-slate-100 text-slate-600 uppercase text-[10px] font-semibold border-b border-slate-200">
            <tr>
              <th class="py-2.5 px-3">#</th>
              <th class="py-2.5 px-3">Data Date / Cut-Off</th>
              <th class="py-2.5 px-3">Update File</th>
              <th class="py-2.5 px-3 text-center">Planned %</th>
              <th class="py-2.5 px-3 text-center">Actual %</th>
              <th class="py-2.5 px-3 text-center">Variance</th>
              <th class="py-2.5 px-3 text-center">SPI</th>
              <th class="py-2.5 px-3 text-center">Slippage</th>
              <th class="py-2.5 px-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody id="updatesTableBody" class="divide-y divide-slate-100">
            <tr>
              <td colspan="9" class="py-6 text-center text-slate-400">
                No weekly updates uploaded yet. Upload your baseline and weekly update files above.
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

  </div>

  <script>
    async function init() {
      await fetchBaseline();
      await fetchUpdates();
    }

    async function fetchBaseline() {
      try {
        const res = await fetch('/api/baseline');
        const data = await res.json();
        const badge = document.getElementById('baselineBadge');
        const card = document.getElementById('baselineInfoCard');
        if (data.has_baseline && data.baseline) {
          badge.className = "text-[11px] px-2 py-0.5 rounded-full font-medium bg-emerald-100 text-emerald-800";
          badge.innerText = "Baseline Active";
          card.classList.remove('hidden');
          document.getElementById('baseProjName').innerText = data.baseline.project_name;
          document.getElementById('baseFileName').innerText = data.baseline.filename;
          document.getElementById('baseTaskCount').innerText = data.baseline.total_activities;
          document.getElementById('baseStartDate').innerText = data.baseline.plan_start_date;
          document.getElementById('baseFinishDate').innerText = data.baseline.plan_end_date;
        } else {
          badge.className = "text-[11px] px-2 py-0.5 rounded-full font-medium bg-slate-100 text-slate-600";
          badge.innerText = "No Baseline";
          card.classList.add('hidden');
        }
      } catch (err) {
        console.error(err);
      }
    }

    async function fetchUpdates() {
      try {
        const res = await fetch('/api/updates');
        const data = await res.json();
        const tbody = document.getElementById('updatesTableBody');
        tbody.innerHTML = '';

        if (!data.updates || data.updates.length === 0) {
          tbody.innerHTML = `<tr><td colspan="9" class="py-6 text-center text-slate-400">No weekly updates uploaded yet. Upload your baseline and weekly update files above.</td></tr>`;
          return;
        }

        data.updates.forEach((u, idx) => {
          const varColor = u.schedule_variance_pct >= 0 ? 'text-emerald-600 font-bold' : 'text-rose-600 font-bold';
          const spiColor = u.spi >= 1.0 ? 'bg-emerald-100 text-emerald-800' : (u.spi >= 0.9 ? 'bg-amber-100 text-amber-800' : 'bg-rose-100 text-rose-800');
          const row = document.createElement('tr');
          row.className = "hover:bg-slate-50 transition";
          row.innerHTML = `
            <td class="py-3 px-3 font-mono font-semibold text-slate-500">${u.id}</td>
            <td class="py-3 px-3 font-bold text-slate-800 whitespace-nowrap">${u.data_date}</td>
            <td class="py-3 px-3 text-slate-600 font-mono text-[11px]">${u.filename}</td>
            <td class="py-3 px-3 text-center text-slate-600">${u.overall_progress_planned}%</td>
            <td class="py-3 px-3 text-center font-bold text-slate-900">${u.overall_progress_actual}%</td>
            <td class="py-3 px-3 text-center ${varColor}">${u.schedule_variance_pct > 0 ? '+' : ''}${u.schedule_variance_pct}%</td>
            <td class="py-3 px-3 text-center whitespace-nowrap"><span class="px-2 py-0.5 rounded text-[11px] font-semibold ${spiColor}">${u.spi}</span></td>
            <td class="py-3 px-3 text-center text-slate-600 whitespace-nowrap">${u.schedule_slippage_days > 0 ? '+' + u.schedule_slippage_days + ' d' : (u.schedule_slippage_days < 0 ? u.schedule_slippage_days + ' d' : '0 d')}</td>
            <td class="py-3 px-3 text-right whitespace-nowrap">
              <a href="/api/dashboard/update/${u.id}" target="_blank" class="px-3 py-1 bg-blue-600 hover:bg-blue-700 text-white rounded text-xs font-semibold shadow-xs transition inline-flex items-center gap-1">
                <span>View Dashboard</span>
                <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"/></svg>
              </a>
            </td>
          `;
          tbody.appendChild(row);
        });
      } catch (err) {
        console.error(err);
      }
    }

    document.getElementById('baselineForm').onsubmit = async (e) => {
      e.preventDefault();
      const fileInput = document.getElementById('baselineFile');
      if (!fileInput.files[0]) return;

      const fd = new FormData();
      fd.append('baseline', fileInput.files[0]);

      const btn = document.getElementById('btnUploadBaseline');
      const msg = document.getElementById('baselineMsg');
      btn.innerText = 'Uploading Baseline...';

      try {
        const res = await fetch('/api/upload-baseline', { method: 'POST', body: fd });
        if (res.ok) {
          msg.className = "mt-2 text-xs text-emerald-600 font-semibold block";
          msg.innerText = "Baseline project uploaded successfully!";
          fileInput.value = "";
          await fetchBaseline();
        } else {
          const err = await res.json();
          msg.className = "mt-2 text-xs text-rose-600 font-semibold block";
          msg.innerText = err.detail || "Failed to upload baseline.";
        }
      } catch (e) {
        msg.className = "mt-2 text-xs text-rose-600 font-semibold block";
        msg.innerText = "Upload failed: " + e.message;
      }
      btn.innerHTML = `<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/></svg><span>Upload & Set Baseline</span>`;
    };

    document.getElementById('updateForm').onsubmit = async (e) => {
      e.preventDefault();
      const fileInput = document.getElementById('updateFile');
      if (!fileInput.files[0]) return;

      const fd = new FormData();
      fd.append('update', fileInput.files[0]);

      const btn = document.getElementById('btnUploadUpdate');
      const msg = document.getElementById('updateMsg');
      btn.innerText = 'Analyzing Weekly Update...';

      try {
        const res = await fetch('/api/upload-update', { method: 'POST', body: fd });
        if (res.ok) {
          const data = await res.json();
          msg.className = "mt-2 text-xs text-emerald-600 font-semibold block";
          msg.innerText = `Update for ${data.summary.data_date} recorded successfully!`;
          fileInput.value = "";
          await fetchUpdates();
          // Open dashboard in new tab
          window.open(`/api/dashboard/update/${data.update_id}`, '_blank');
        } else {
          const err = await res.json();
          msg.className = "mt-2 text-xs text-rose-600 font-semibold block";
          msg.innerText = err.detail || "Failed to process update.";
        }
      } catch (e) {
        msg.className = "mt-2 text-xs text-rose-600 font-semibold block";
        msg.innerText = "Upload failed: " + e.message;
      }
      btn.innerHTML = `<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/></svg><span>Upload Weekly Update</span>`;
    };

    async function loadSampleData() {
      try {
        const res = await fetch('/api/load-sample', { method: 'POST' });
        if (res.ok) {
          await fetchBaseline();
          await fetchUpdates();
          alert("Sample baseline and week 12 update loaded successfully!");
        } else {
          alert("Could not load sample data.");
        }
      } catch (e) {
        alert("Failed to load sample data: " + e.message);
      }
    }

    init();
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

    b_content = _decode_file_bytes(b_bytes)
    u_content = _decode_file_bytes(u_bytes)

    b_proj = parse_xer_content(b_content, baseline.filename or "baseline.xer")
    u_proj = parse_xer_content(u_content, update.filename or "update.xer")
    report_data = compare_schedules(b_proj, u_proj)
    return JSONResponse(content=report_data)

@app.post("/api/upload-baseline")
async def upload_baseline(baseline: UploadFile = File(...)):
    """Uploads and activates project baseline."""
    b_bytes = await baseline.read()
    b_content = _decode_file_bytes(b_bytes)
    filename = baseline.filename or "baseline.xer"

    b_proj = parse_xer_content(b_content, filename)
    meta = {
        "filename": filename,
        "project_name": b_proj.project_info.get("proj_short_name", "Baseline Project"),
        "total_activities": len(b_proj.tasks),
        "data_date": str(b_proj.project_info.get("data_date", "-")),
        "plan_start_date": str(b_proj.project_info.get("plan_start_date", "-")),
        "plan_end_date": str(b_proj.project_info.get("plan_end_date", "-")),
    }
    app_state["baseline"] = b_proj
    app_state["baseline_meta"] = meta
    return JSONResponse(content={"status": "success", "baseline": meta})

@app.get("/api/baseline")
def get_baseline():
    """Returns the current active baseline info or null if none uploaded."""
    if app_state["baseline_meta"]:
        return JSONResponse(content={"has_baseline": True, "baseline": app_state["baseline_meta"]})
    return JSONResponse(content={"has_baseline": False, "baseline": None})

@app.post("/api/upload-update")
async def upload_update(update: UploadFile = File(...)):
    """Uploads a weekly update XER file for continuance reporting."""
    if not app_state["baseline"]:
        raise HTTPException(
            status_code=400,
            detail="No baseline schedule active. Please upload a baseline file first."
        )

    u_bytes = await update.read()
    u_content = _decode_file_bytes(u_bytes)
    filename = update.filename or "update.xer"

    u_proj = parse_xer_content(u_content, filename)
    report_data = compare_schedules(app_state["baseline"], u_proj)

    update_id = len(app_state["updates"]) + 1
    record = {
        "id": update_id,
        "filename": filename,
        "data_date": report_data["project_info"]["data_date"],
        "project_name": report_data["project_info"]["project_name"],
        "overall_progress_planned": report_data["summary"]["overall_progress_planned"],
        "overall_progress_actual": report_data["summary"]["overall_progress_actual"],
        "schedule_variance_pct": report_data["summary"]["schedule_variance_pct"],
        "spi": report_data["summary"]["spi"],
        "status_indicator": report_data["summary"]["status_indicator"],
        "forecast_finish": report_data["project_info"]["forecast_finish"],
        "schedule_slippage_days": report_data["project_info"]["schedule_slippage_days"],
        "report_data": report_data,
    }
    app_state["updates"].append(record)

    return JSONResponse(content={
        "status": "success",
        "update_id": update_id,
        "summary": record,
    })

@app.get("/api/updates")
def get_updates():
    """Returns the chronological history of weekly updates for continuance reporting."""
    updates_summary = [
        {
            "id": u["id"],
            "filename": u["filename"],
            "data_date": u["data_date"],
            "project_name": u["project_name"],
            "overall_progress_planned": u["overall_progress_planned"],
            "overall_progress_actual": u["overall_progress_actual"],
            "schedule_variance_pct": u["schedule_variance_pct"],
            "spi": u["spi"],
            "status_indicator": u["status_indicator"],
            "forecast_finish": u["forecast_finish"],
            "schedule_slippage_days": u["schedule_slippage_days"],
        }
        for u in app_state["updates"]
    ]
    return JSONResponse(content={"updates": updates_summary})

@app.get("/api/dashboard/update/{update_id}", response_class=HTMLResponse)
def get_update_dashboard(update_id: int):
    """Renders the HTML dashboard for a specific weekly update in continuance history."""
    target = None
    for u in app_state["updates"]:
        if u["id"] == update_id:
            target = u
            break
    if not target:
        raise HTTPException(status_code=404, detail="Weekly update report not found")

    html = generate_html_dashboard(target["report_data"])
    return HTMLResponse(content=html)

@app.post("/api/load-sample")
def load_sample_flow():
    """Pre-populates baseline and sample weekly update into the app state."""
    base_path = "samples/baseline_project.xer"
    up_path = "samples/update_week12.xer"
    if not os.path.exists(base_path) or not os.path.exists(up_path):
        raise HTTPException(status_code=404, detail="Sample XER files not found")

    with open(base_path, "r", encoding="utf-8") as f:
        b_content = f.read()
    with open(up_path, "r", encoding="utf-8") as f:
        u_content = f.read()

    b_proj = parse_xer_content(b_content, "baseline_project.xer")
    meta = {
        "filename": "baseline_project.xer",
        "project_name": b_proj.project_info.get("proj_short_name", "Baseline Project"),
        "total_activities": len(b_proj.tasks),
        "data_date": str(b_proj.project_info.get("data_date", "-")),
        "plan_start_date": str(b_proj.project_info.get("plan_start_date", "-")),
        "plan_end_date": str(b_proj.project_info.get("plan_end_date", "-")),
    }
    app_state["baseline"] = b_proj
    app_state["baseline_meta"] = meta

    u_proj = parse_xer_content(u_content, "update_week12.xer")
    report_data = compare_schedules(b_proj, u_proj)
    update_id = len(app_state["updates"]) + 1
    record = {
        "id": update_id,
        "filename": "update_week12.xer",
        "data_date": report_data["project_info"]["data_date"],
        "project_name": report_data["project_info"]["project_name"],
        "overall_progress_planned": report_data["summary"]["overall_progress_planned"],
        "overall_progress_actual": report_data["summary"]["overall_progress_actual"],
        "schedule_variance_pct": report_data["summary"]["schedule_variance_pct"],
        "spi": report_data["summary"]["spi"],
        "status_indicator": report_data["summary"]["status_indicator"],
        "forecast_finish": report_data["project_info"]["forecast_finish"],
        "schedule_slippage_days": report_data["project_info"]["schedule_slippage_days"],
        "report_data": report_data,
    }
    app_state["updates"].append(record)

    return JSONResponse(content={
        "status": "success",
        "baseline": meta,
        "update_id": update_id,
        "summary": record,
    })
