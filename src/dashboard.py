"""
Construction Progress Report Dashboard Generator
Generates a modern, interactive, multi-view construction executive dashboard
featuring:
1. Side Panel Navigation:
   - Overview (Project KPIs, Interactive Layout Map with clickable areas, S-Curve, WBS breakdown)
   - Lookahead (30/60-day window, active & upcoming tasks, critical path)
   - Manpower (Monthly labor histogram, peak headcount, craft breakdown)
   - Cashflow (Planned vs actual spend, cumulative curve, budget tracking)
   - Invoice (Interim payment certificates, retention, billing status)
2. Interactive Project Layout Map:
   - Architectural zoning layout with interactive clickable areas updating area-specific KPIs
   - Ability to upload/load custom interactive layout SVG or diagram
3. Dynamic Activity Code & WBS Filtering:
   - Instant filters for WBS level and all project activity codes (Area, Discipline, Phase)
"""

import json
from typing import Dict, Any


def generate_html_dashboard(data: Dict[str, Any]) -> str:
    proj = data["project_info"]
    summary = data["summary"]
    wbs_list = data["wbs_summary"]
    areas_list = data.get("areas_summary", [])
    filters_meta = data.get("filters_meta", {})
    milestones = data["milestones"]
    critical = data["critical_activities"]
    delayed = data["delayed_activities"]
    all_acts = data["all_activities"]
    scurve = data["scurve"]
    lookahead = data.get("lookahead", {})
    manpower = data.get("manpower", {})
    cashflow = data.get("cashflow", {})
    invoice = data.get("invoice", [])

    # Color tokens based on SPI
    spi = summary["spi"]
    if spi >= 1.0:
        spi_color = "text-emerald-600 bg-emerald-50 border-emerald-200"
        spi_badge = "bg-emerald-500"
    elif spi >= 0.90:
        spi_color = "text-amber-600 bg-amber-50 border-amber-200"
        spi_badge = "bg-amber-500"
    else:
        spi_color = "text-rose-600 bg-rose-50 border-rose-200"
        spi_badge = "bg-rose-500"

    full_payload_json = json.dumps(data)

    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Construction Executive Dashboard | {proj['project_name']}</title>
  <!-- Tailwind CSS -->
  <script src="https://cdn.tailwindcss.com"></script>
  <!-- Chart.js -->
  <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
  <style>
    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&display=swap');
    body {{
      font-family: 'Inter', sans-serif;
      background-color: #f8fafc;
      color: #0f172a;
    }}
    .nav-link.active {{
      background-color: #1e293b;
      color: #38bdf8;
      border-left: 3px solid #38bdf8;
    }}
    .metric-card {{
      box-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.05), 0 1px 2px 0 rgba(0, 0, 0, 0.03);
      transition: all 0.2s ease-in-out;
    }}
    .metric-card:hover {{
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.08), 0 2px 4px -1px rgba(0, 0, 0, 0.04);
    }}
    .zone-polygon {{
      transition: all 0.25s ease;
      cursor: pointer;
    }}
    .zone-polygon:hover {{
      filter: brightness(1.15) drop-shadow(0 0 6px rgba(56, 189, 248, 0.6));
    }}
    .zone-polygon.selected {{
      stroke: #38bdf8 !important;
      stroke-width: 3.5 !important;
      filter: drop-shadow(0 0 10px rgba(56, 189, 248, 0.8));
    }}
    @media print {{
      .no-print {{ display: none !important; }}
      .sidebar {{ display: none !important; }}
      .main-viewport {{ margin-left: 0 !important; width: 100% !important; }}
      body {{ background-color: #ffffff; }}
    }}
  </style>
</head>
<body class="min-h-screen flex flex-col md:flex-row antialiased">

  <!-- SIDEBAR NAVIGATION PANEL -->
  <aside class="sidebar w-full md:w-64 bg-slate-900 text-slate-300 flex-shrink-0 flex flex-col justify-between border-r border-slate-800 z-40 md:min-h-screen sticky top-0">
    <div>
      <!-- Brand & Project Selector -->
      <div class="p-5 border-b border-slate-800">
        <div class="flex items-center space-x-3">
          <div class="w-10 h-10 rounded-xl bg-blue-600 flex items-center justify-center text-white font-black text-lg shadow-md shadow-blue-500/20">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
          </div>
          <div class="overflow-hidden">
            <div class="text-xs uppercase font-extrabold tracking-wider text-blue-400">Construction Suite</div>
            <h1 class="text-sm font-bold text-white truncate" title="{proj['project_name']}">{proj['project_name']}</h1>
          </div>
        </div>

        <div class="mt-4 pt-3 border-t border-slate-800/80 flex items-center justify-between text-[11px] text-slate-400">
          <span>Cut-off Date:</span>
          <span class="font-semibold text-slate-200">{proj['data_date']}</span>
        </div>
      </div>

      <!-- Navigation Menu Items -->
      <nav class="py-4 space-y-1">
        <button onclick="switchView('overview')" id="nav-overview" class="nav-link active w-full flex items-center px-5 py-3 text-sm font-medium text-slate-200 hover:bg-slate-800 hover:text-white transition">
          <svg class="w-5 h-5 mr-3 text-sky-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"/>
          </svg>
          <span>Overview & Layout</span>
        </button>

        <button onclick="switchView('lookahead')" id="nav-lookahead" class="nav-link w-full flex items-center px-5 py-3 text-sm font-medium text-slate-300 hover:bg-slate-800 hover:text-white transition">
          <svg class="w-5 h-5 mr-3 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>
          </svg>
          <span>Lookahead Schedule</span>
        </button>

        <button onclick="switchView('manpower')" id="nav-manpower" class="nav-link w-full flex items-center px-5 py-3 text-sm font-medium text-slate-300 hover:bg-slate-800 hover:text-white transition">
          <svg class="w-5 h-5 mr-3 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"/>
          </svg>
          <span>Manpower & Labor</span>
        </button>

        <button onclick="switchView('cashflow')" id="nav-cashflow" class="nav-link w-full flex items-center px-5 py-3 text-sm font-medium text-slate-300 hover:bg-slate-800 hover:text-white transition">
          <svg class="w-5 h-5 mr-3 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
          </svg>
          <span>Cashflow & Spend</span>
        </button>

        <button onclick="switchView('invoice')" id="nav-invoice" class="nav-link w-full flex items-center px-5 py-3 text-sm font-medium text-slate-300 hover:bg-slate-800 hover:text-white transition">
          <svg class="w-5 h-5 mr-3 text-violet-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
          </svg>
          <span>Invoice & Billing (IPC)</span>
        </button>

        <button onclick="switchView('fourd')" id="nav-fourd" class="nav-link w-full flex items-center px-5 py-3 text-sm font-medium text-slate-300 hover:bg-slate-800 hover:text-white transition">
          <svg class="w-5 h-5 mr-3 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4"/>
          </svg>
          <span>4D Modeling</span>
        </button>
      </nav>

      <!-- Sidebar Quick Controls: Load Custom Layout & Upload XER -->
      <div class="px-5 py-4 border-t border-slate-800 space-y-2 no-print">
        <label class="cursor-pointer w-full flex items-center justify-center gap-2 px-3 py-2 bg-slate-800 hover:bg-slate-700 text-xs font-semibold rounded-lg text-slate-200 border border-slate-700 transition shadow-xs">
          <svg class="w-4 h-4 text-sky-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg>
          <span>Load Custom Layout</span>
          <input type="file" id="customLayoutInput" accept=".svg,.png,.jpg,.jpeg" class="hidden" onchange="handleCustomLayoutUpload(this)">
        </label>

        <button onclick="window.print()" class="w-full flex items-center justify-center gap-2 px-3 py-2 bg-slate-800/60 hover:bg-slate-800 text-xs font-semibold rounded-lg text-slate-300 border border-slate-700/60 transition">
          <svg class="w-4 h-4 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z"/></svg>
          <span>Print / Export PDF</span>
        </button>
      </div>
    </div>

    <!-- Sidebar Bottom Info -->
    <div class="p-4 border-t border-slate-800/80 text-[11px] text-slate-400">
      <div class="flex justify-between items-center">
        <span>Baseline:</span>
        <span class="text-slate-300 font-medium truncate max-w-[120px]">{proj['baseline_name']}</span>
      </div>
      <div class="flex justify-between items-center mt-1">
        <span>Forecast Finish:</span>
        <span class="text-slate-200 font-semibold">{proj['forecast_finish']}</span>
      </div>
    </div>
  </aside>

  <!-- MAIN VIEWPORT -->
  <div class="main-viewport flex-1 flex flex-col min-w-0 overflow-y-auto">

    <!-- Top Sticky Header with Dynamic Filter Bar -->
    <header class="bg-white border-b border-slate-200 sticky top-0 z-30 shadow-xs px-4 sm:px-6 py-3.5">
      <div class="flex flex-wrap items-center justify-between gap-4">
        <!-- Title and Active View Breadcrumb -->
        <div class="flex items-center space-x-3">
          <h2 id="viewTitle" class="text-lg font-bold text-slate-900 tracking-tight">Overview & Interactive Layout</h2>
          <span id="activeAreaBadge" class="hidden px-2.5 py-0.5 rounded-full text-xs font-semibold bg-sky-100 text-sky-800 border border-sky-200">
            Filtered by Area: <span id="activeAreaNameText">All</span>
          </span>
        </div>

        <!-- Filter Bar: Activity Codes, WBS Levels, Area Dropdowns -->
        <div class="flex flex-wrap items-center gap-2.5 no-print text-xs">
          <!-- Button 1: Upload Baseline -->
          <label class="cursor-pointer inline-flex items-center px-2.5 py-1.5 bg-slate-800 hover:bg-slate-700 border border-slate-700 rounded-lg text-xs font-semibold text-slate-200 transition shadow-xs gap-1.5">
            <svg class="w-3.5 h-3.5 text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/></svg>
            <span>Upload Baseline</span>
            <input type="file" id="dashBaselineFile" accept=".xer" class="hidden" onchange="uploadDashboardBaseline(this)">
          </label>

          <!-- Button 2: Upload Update -->
          <label class="cursor-pointer inline-flex items-center px-2.5 py-1.5 bg-emerald-600 hover:bg-emerald-700 rounded-lg text-xs font-semibold text-white transition shadow-xs gap-1.5">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/></svg>
            <span>Upload Update</span>
            <input type="file" id="dashUpdateFile" accept=".xer" class="hidden" onchange="uploadDashboardUpdate(this)">
          </label>

          <!-- WBS Level Filter -->
          <div class="flex items-center space-x-1.5 bg-slate-100 px-2.5 py-1.5 rounded-lg border border-slate-200">
            <span class="text-slate-500 font-semibold">WBS:</span>
            <select id="filterWbsLevel" onchange="applyGlobalFilters()" class="bg-transparent font-medium text-slate-800 focus:outline-none cursor-pointer">
              <option value="">All Levels</option>
              {"".join([f'<option value="{lvl}">Level {lvl}</option>' for lvl in filters_meta.get("wbs_levels", [1, 2])])}
            </select>
          </div>

          <!-- Dynamic Activity Code Filters -->
          {"".join([f'''
          <div class="flex items-center space-x-1.5 bg-slate-100 px-2.5 py-1.5 rounded-lg border border-slate-200">
            <span class="text-slate-500 font-semibold">{code_type}:</span>
            <select id="filterActv_{code_type.replace(' ', '_')}" onchange="applyGlobalFilters()" class="bg-transparent font-medium text-slate-800 focus:outline-none cursor-pointer">
              <option value="">All {code_type}</option>
              {''.join([f'<option value="{val}">{val}</option>' for val in val_list])}
            </select>
          </div>
          ''' for code_type, val_list in filters_meta.get("activity_codes", {}).items()])}

          <!-- Reset Filters Button -->
          <button onclick="resetAllFilters()" class="px-2.5 py-1.5 text-slate-600 hover:text-slate-900 hover:bg-slate-200 font-semibold rounded-lg transition">
            Reset Filters
          </button>
        </div>
      </div>
    </header>

    <!-- CONTENT VIEWS CONTAINER -->
    <main class="flex-1 p-4 sm:p-6 lg:p-8 space-y-6">

      <!-- ========================================== -->
      <!-- VIEW 1: OVERVIEW & INTERACTIVE LAYOUT -->
      <!-- ========================================== -->
      <div id="view-overview" class="view-panel space-y-6">

        <!-- Area KPI Scorecard (Updates dynamically when clicking on Layout Area) -->
        <div class="bg-gradient-to-r from-slate-900 via-slate-800 to-slate-900 text-white rounded-2xl p-6 shadow-md border border-slate-700/60">
          <div class="flex flex-wrap items-center justify-between gap-4 mb-4 pb-3 border-b border-slate-700/80">
            <div>
              <div class="text-xs font-semibold text-sky-400 uppercase tracking-wider flex items-center gap-1.5">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/></svg>
                <span id="selectedZoneLabel">Entire Project Performance</span>
              </div>
              <h3 id="selectedZoneName" class="text-xl font-bold text-white mt-0.5">Marina Commercial Tower (All Zones)</h3>
            </div>
            <div class="flex items-center gap-2">
              <button onclick="resetAreaSelection()" id="btnResetArea" class="hidden px-3 py-1.5 text-xs font-semibold rounded-lg bg-sky-500/20 text-sky-300 border border-sky-400/30 hover:bg-sky-500/30 transition">
                Show Overall Project
              </button>
              <span id="selectedZoneStatusBadge" class="px-3 py-1 text-xs font-bold rounded-full {spi_color}">
                {summary['status_indicator']}
              </span>
            </div>
          </div>

          <!-- Dynamic Area / Project KPI Cards Grid -->
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div class="bg-slate-800/70 p-4 rounded-xl border border-slate-700">
              <div class="text-xs text-slate-400 uppercase font-medium">Actual Progress</div>
              <div class="mt-1 flex items-baseline gap-2">
                <span id="kpiActualPct" class="text-2xl font-extrabold text-white">{summary['overall_progress_actual']}%</span>
                <span class="text-xs text-slate-400">/ <span id="kpiPlannedPct">{summary['overall_progress_planned']}%</span> Plan</span>
              </div>
              <div class="w-full bg-slate-700 rounded-full h-1.5 mt-2 overflow-hidden">
                <div id="kpiProgressBar" class="bg-sky-400 h-1.5 rounded-full" style="width: {min(summary['overall_progress_actual'], 100.0)}%"></div>
              </div>
            </div>

            <div class="bg-slate-800/70 p-4 rounded-xl border border-slate-700">
              <div class="text-xs text-slate-400 uppercase font-medium">Schedule Index (SPI)</div>
              <div class="mt-1 flex items-baseline gap-2">
                <span id="kpiSpi" class="text-2xl font-extrabold text-white">{summary['spi']}</span>
                <span id="kpiVarianceDays" class="text-xs font-semibold { 'text-rose-400' if proj['schedule_slippage_days'] > 0 else 'text-emerald-400' }">
                  {f"+{proj['schedule_slippage_days']}d slip" if proj['schedule_slippage_days'] > 0 else "On Target"}
                </span>
              </div>
              <div class="text-[11px] text-slate-400 mt-1">Variance: <span id="kpiVariancePct" class="font-bold">{summary['schedule_variance_pct']:+}%</span></div>
            </div>

            <div class="bg-slate-800/70 p-4 rounded-xl border border-slate-700">
              <div class="text-xs text-slate-400 uppercase font-medium">Active Tasks</div>
              <div class="mt-1 flex items-baseline gap-2">
                <span id="kpiActiveTasks" class="text-2xl font-extrabold text-white">{summary['status_counts']['In Progress']}</span>
                <span class="text-xs text-slate-400">/ <span id="kpiTotalTasks">{summary['total_activities']}</span> total</span>
              </div>
              <div class="text-[11px] text-slate-400 mt-1"><span id="kpiDoneTasks" class="text-emerald-400 font-bold">{summary['status_counts']['Completed']}</span> completed</div>
            </div>

            <div class="bg-slate-800/70 p-4 rounded-xl border border-slate-700">
              <div class="text-xs text-slate-400 uppercase font-medium">Critical Items</div>
              <div class="mt-1 flex items-baseline gap-2">
                <span id="kpiCriticalTasks" class="text-2xl font-extrabold text-rose-400">{summary['float_dist']['Critical (<=0d)']}</span>
                <span class="text-xs text-slate-400">Total Float &le; 0</span>
              </div>
              <div class="text-[11px] text-slate-400 mt-1"><span id="kpiDelayedTasks" class="text-amber-400 font-bold">{summary['delay_counts']['Delayed']}</span> tasks delayed</div>
            </div>
          </div>
        </div>

        <!-- Interactive Layout Section & S-Curve 2-Column -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">

          <!-- Column 1: Interactive Layout Map (Clickable Areas) -->
          <div class="lg:col-span-7 bg-white rounded-2xl p-6 border border-slate-200 shadow-sm flex flex-col justify-between">
            <div>
              <div class="flex flex-wrap items-center justify-between gap-3 mb-4">
                <div>
                  <h3 class="text-base font-bold text-slate-900 flex items-center gap-2">
                    <span>Interactive Construction Layout</span>
                    <span class="text-xs font-normal text-slate-500">(Click any area to inspect its KPI)</span>
                  </h3>
                  <p class="text-xs text-slate-500">Zoning plan linked directly to Primavera P6 activity codes and schedule packages</p>
                </div>
                <div class="flex items-center gap-2">
                  <label class="cursor-pointer inline-flex items-center px-2.5 py-1 text-xs font-semibold rounded bg-sky-50 text-sky-700 border border-sky-200 hover:bg-sky-100 transition gap-1">
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"/></svg>
                    <span>Load Custom Layout</span>
                    <input type="file" accept=".svg,.png,.jpg" class="hidden" onchange="handleCustomLayoutUpload(this)">
                  </label>
                </div>
              </div>

              <!-- SVG Interactive Layout Container -->
              <div id="layoutContainer" class="w-full bg-slate-950 rounded-xl p-4 border border-slate-800 relative overflow-hidden flex items-center justify-center min-h-[320px]">
                <svg id="interactiveSiteSvg" viewBox="0 0 800 480" class="w-full h-auto max-h-[360px] select-none">
                  <!-- Architectural Grid Background -->
                  <defs>
                    <pattern id="siteGrid" width="40" height="40" patternUnits="userSpaceOnUse">
                      <path d="M 40 0 L 0 0 0 40" fill="none" stroke="#1e293b" stroke-width="0.75" />
                    </pattern>
                  </defs>
                  <rect width="800" height="480" fill="url(#siteGrid)" />

                  <!-- ZONE 1: Substructure & Foundation (ZONE-SUB) -->
                  <g class="zone-group" data-zone="ZONE-SUB" onclick="selectArea('ZONE-SUB', 'Substructure & Foundation')">
                    <polygon class="zone-polygon" points="60,260 340,260 340,420 60,420" fill="#3b82f6" fill-opacity="0.3" stroke="#60a5fa" stroke-width="2"/>
                    <text x="200" y="330" fill="#93c5fd" font-weight="bold" font-size="14" text-anchor="middle">ZONE-SUB</text>
                    <text x="200" y="352" fill="#cbd5e1" font-size="11" text-anchor="middle">Substructure & Foundation</text>
                  </g>

                  <!-- ZONE 2: Superstructure Tower (ZONE-TOW) -->
                  <g class="zone-group" data-zone="ZONE-TOW" onclick="selectArea('ZONE-TOW', 'Superstructure Tower')">
                    <polygon class="zone-polygon" points="120,60 340,60 340,240 120,240" fill="#8b5cf6" fill-opacity="0.35" stroke="#a78bfa" stroke-width="2"/>
                    <text x="230" y="140" fill="#c4b5fd" font-weight="bold" font-size="15" text-anchor="middle">ZONE-TOW</text>
                    <text x="230" y="162" fill="#cbd5e1" font-size="11" text-anchor="middle">Superstructure Tower Core</text>
                  </g>

                  <!-- ZONE 3: MEP Plant & Core (ZONE-MEP) -->
                  <g class="zone-group" data-zone="ZONE-MEP" onclick="selectArea('ZONE-MEP', 'MEP Plant & Core')">
                    <polygon class="zone-polygon" points="360,60 520,60 520,240 360,240" fill="#f59e0b" fill-opacity="0.3" stroke="#fbbf24" stroke-width="2"/>
                    <text x="440" y="140" fill="#fde68a" font-weight="bold" font-size="14" text-anchor="middle">ZONE-MEP</text>
                    <text x="440" y="162" fill="#cbd5e1" font-size="11" text-anchor="middle">Central MEP Plant</text>
                  </g>

                  <!-- ZONE 4: Interior Finishes & Facade (ZONE-FIN) -->
                  <g class="zone-group" data-zone="ZONE-FIN" onclick="selectArea('ZONE-FIN', 'Interior Finishes & Facade')">
                    <polygon class="zone-polygon" points="360,260 580,260 580,420 360,420" fill="#10b981" fill-opacity="0.3" stroke="#34d399" stroke-width="2"/>
                    <text x="470" y="330" fill="#a7f3d0" font-weight="bold" font-size="14" text-anchor="middle">ZONE-FIN</text>
                    <text x="470" y="352" fill="#cbd5e1" font-size="11" text-anchor="middle">Finishes & Curtain Wall</text>
                  </g>

                  <!-- ZONE 5: Site Works & Logistics (ZONE-SITE) -->
                  <g class="zone-group" data-zone="ZONE-SITE" onclick="selectArea('ZONE-SITE', 'Site Works & Infrastructure')">
                    <polygon class="zone-polygon" points="540,60 740,60 740,240 540,240" fill="#06b6d4" fill-opacity="0.25" stroke="#22d3ee" stroke-width="2"/>
                    <polygon class="zone-polygon" points="600,260 740,260 740,420 600,420" fill="#06b6d4" fill-opacity="0.25" stroke="#22d3ee" stroke-width="2"/>
                    <text x="640" y="140" fill="#a5f3fc" font-weight="bold" font-size="14" text-anchor="middle">ZONE-SITE</text>
                    <text x="640" y="162" fill="#cbd5e1" font-size="11" text-anchor="middle">Infrastructure & Logistics</text>
                  </g>
                </svg>
              </div>
            </div>

            <!-- Area Quick Badges -->
            <div class="mt-4 pt-3 border-t border-slate-100 flex flex-wrap items-center gap-2">
              <span class="text-xs font-semibold text-slate-500">Fast Area Select:</span>
              {"".join([f'''
              <button onclick="selectArea('{a['area_code']}', '{a['area_name']}')" class="px-2.5 py-1 text-xs font-medium rounded-lg bg-slate-100 hover:bg-sky-100 hover:text-sky-800 text-slate-700 transition">
                {a['area_name']} ({a['actual_pct']}%)
              </button>
              ''' for a in areas_list])}
            </div>
          </div>

          <!-- Column 2: Cumulative S-Curve Chart -->
          <div class="lg:col-span-5 bg-white rounded-2xl p-6 border border-slate-200 shadow-sm flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between mb-2">
                <h3 class="text-base font-bold text-slate-900">Project S-Curve</h3>
                <span class="text-xs font-semibold text-slate-400">Cut-off {proj['data_date']}</span>
              </div>
              <p class="text-xs text-slate-500 mb-4">Planned baseline cumulative progression vs actual earned progress</p>
              <div class="w-full h-64 relative">
                <canvas id="overviewScurveChart"></canvas>
              </div>
            </div>

            <div class="mt-4 pt-3 border-t border-slate-100 flex justify-between text-xs text-slate-500">
              <span class="inline-flex items-center"><span class="w-2.5 h-2.5 rounded-full bg-slate-400 mr-1.5"></span> Planned</span>
              <span class="inline-flex items-center"><span class="w-2.5 h-2.5 rounded-full bg-blue-600 mr-1.5"></span> Actual to Date</span>
              <span class="inline-flex items-center"><span class="w-2.5 h-2.5 rounded-full bg-amber-500 mr-1.5"></span> Forecast</span>
            </div>
          </div>

        </div>

        <!-- Two Column: WBS Breakdown & Milestones -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">

          <!-- WBS Progress Breakdown -->
          <div class="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm">
            <div class="flex items-center justify-between mb-4">
              <div>
                <h3 class="text-base font-bold text-slate-900">WBS Level Progress Rollup</h3>
                <p class="text-xs text-slate-500">Weight-based duration aggregation per work package</p>
              </div>
            </div>
            <div class="space-y-3.5 max-h-[380px] overflow-y-auto pr-1" id="wbsProgressContainer">
              {"".join([f'''
              <div class="p-3 rounded-xl bg-slate-50 border border-slate-100">
                <div class="flex justify-between items-center text-xs mb-1.5">
                  <span class="font-bold text-slate-800">{w['wbs_name']}</span>
                  <span class="text-slate-500 font-medium">{w['actual_pct']}% / <span class="text-slate-400">{w['planned_pct']}% Plan</span></span>
                </div>
                <div class="w-full bg-slate-200 rounded-full h-2 overflow-hidden flex">
                  <div class="{'bg-emerald-500' if w['actual_pct'] >= w['planned_pct'] else 'bg-blue-600'} h-2 rounded-full" style="width: {min(w['actual_pct'], 100.0)}%"></div>
                </div>
                <div class="flex justify-between items-center text-[11px] text-slate-500 mt-1.5">
                  <span>{w['total_tasks']} Tasks ({w['completed_tasks']} Done, {w['in_progress_tasks']} Active)</span>
                  <span class="font-semibold { 'text-rose-600' if w['var_pct'] < -2.0 else ('text-emerald-600' if w['var_pct'] >= 0 else 'text-slate-600') }">
                    Var: {w['var_pct']:+}%
                  </span>
                </div>
              </div>
              ''' for w in wbs_list])}
            </div>
          </div>

          <!-- Key Project Milestones -->
          <div class="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm">
            <div class="flex items-center justify-between mb-4">
              <div>
                <h3 class="text-base font-bold text-slate-900">Key Project Milestones</h3>
                <p class="text-xs text-slate-500">Critical target completions vs forecasted actuals</p>
              </div>
              <span class="text-xs font-semibold px-2 py-0.5 rounded bg-slate-100 text-slate-600">{len(milestones)} Milestones</span>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-100 text-slate-600 font-semibold border-b border-slate-200 uppercase text-[10px]">
                  <tr>
                    <th class="py-2.5 px-3">Milestone Name</th>
                    <th class="py-2.5 px-2">BL Date</th>
                    <th class="py-2.5 px-2">Forecast</th>
                    <th class="py-2.5 px-2 text-center">Variance</th>
                    <th class="py-2.5 px-2 text-right">Status</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-100">
                  {"".join([f'''
                  <tr class="hover:bg-slate-50 transition">
                    <td class="py-2.5 px-3 font-semibold text-slate-800">
                      <div>{m['task_name']}</div>
                      <div class="text-[10px] text-slate-400 font-mono">{m['task_code']}</div>
                    </td>
                    <td class="py-2.5 px-2 text-slate-600 whitespace-nowrap">{m['bl_end']}</td>
                    <td class="py-2.5 px-2 font-medium text-slate-900 whitespace-nowrap">{m['curr_end']}</td>
                    <td class="py-2.5 px-2 text-center font-bold whitespace-nowrap { 'text-rose-600' if m['finish_var_days'] > 0 else ('text-emerald-600' if m['finish_var_days'] < 0 else 'text-slate-500') }">
                      {f"+{m['finish_var_days']}d" if m['finish_var_days'] > 0 else (f"{m['finish_var_days']}d" if m['finish_var_days'] < 0 else "0d")}
                    </td>
                    <td class="py-2.5 px-2 text-right whitespace-nowrap">
                      <span class="inline-block px-2 py-0.5 rounded text-[10px] font-semibold {'bg-emerald-100 text-emerald-700' if m['status'] == 'Completed' else 'bg-amber-100 text-amber-800'}">
                        {m['status']}
                      </span>
                    </td>
                  </tr>
                  ''' for m in milestones])}
                </tbody>
              </table>
            </div>
          </div>

        </div>

        <!-- Critical Path & Delayed Activities Watchlist -->
        <div class="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm">
          <div class="flex flex-wrap items-center justify-between gap-4 mb-4">
            <div>
              <h3 class="text-base font-bold text-slate-900">Critical Path & Delayed Activities Watchlist</h3>
              <p class="text-xs text-slate-500">Activities with Total Float &le; 0 or finish date slipping behind baseline</p>
            </div>
            <div class="flex items-center gap-2">
              <input type="text" id="overviewTableSearch" onkeyup="filterActivitiesTable()" placeholder="Search activities, ID or zone..." class="px-3 py-1.5 text-xs border border-slate-300 rounded-lg focus:outline-none focus:ring-1 focus:ring-blue-500 w-48 sm:w-64">
            </div>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs border border-slate-100 rounded-xl" id="masterActivityTable">
              <thead class="bg-slate-100 text-slate-600 font-semibold uppercase text-[10px] border-b border-slate-200">
                <tr>
                  <th class="py-2.5 px-3">Activity ID</th>
                  <th class="py-2.5 px-3">Activity Description</th>
                  <th class="py-2.5 px-2">Area / Zone</th>
                  <th class="py-2.5 px-2">WBS</th>
                  <th class="py-2.5 px-2">Status</th>
                  <th class="py-2.5 px-2">Target Finish</th>
                  <th class="py-2.5 px-2">Forecast Finish</th>
                  <th class="py-2.5 px-2 text-center">Variance</th>
                  <th class="py-2.5 px-2 text-center">Total Float</th>
                  <th class="py-2.5 px-2 text-center">% Comp</th>
                </tr>
              </thead>
              <tbody id="masterTableBody" class="divide-y divide-slate-100">
                <!-- Dynamically populated via JavaScript for instant filtering -->
              </tbody>
            </table>
          </div>
        </div>

      </div>

      <!-- ========================================== -->
      <!-- VIEW 2: LOOKAHEAD SCHEDULE -->
      <!-- ========================================== -->
      <div id="view-lookahead" class="view-panel hidden space-y-6">
        <div class="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm">
          <div class="flex flex-wrap items-center justify-between gap-4 mb-4">
            <div>
              <h3 class="text-base font-bold text-slate-900">Lookahead Schedule Management</h3>
              <p class="text-xs text-slate-500">Activities currently in progress or scheduled to commence within the rolling window</p>
            </div>
            <div class="flex items-center space-x-2">
              <button onclick="setLookaheadDays(30)" id="btnLookahead30" class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-blue-600 text-white transition">30-Day Window</button>
              <button onclick="setLookaheadDays(60)" id="btnLookahead60" class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-100 text-slate-700 hover:bg-slate-200 transition">60-Day Window</button>
            </div>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs border border-slate-100" id="lookaheadTable">
              <thead class="bg-slate-100 text-slate-600 font-semibold uppercase text-[10px] border-b border-slate-200">
                <tr>
                  <th class="py-2.5 px-3">Activity ID</th>
                  <th class="py-2.5 px-3">Activity Description</th>
                  <th class="py-2.5 px-2">Area</th>
                  <th class="py-2.5 px-2">Forecast Start</th>
                  <th class="py-2.5 px-2">Forecast Finish</th>
                  <th class="py-2.5 px-2 text-center">Remaining Days</th>
                  <th class="py-2.5 px-2 text-center">Total Float</th>
                  <th class="py-2.5 px-2 text-center">% Comp</th>
                  <th class="py-2.5 px-3">Lookahead Status</th>
                </tr>
              </thead>
              <tbody id="lookaheadTableBody" class="divide-y divide-slate-100">
                <!-- Injected via JS -->
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- ========================================== -->
      <!-- VIEW 3: MANPOWER & LABOR PROFILE -->
      <!-- ========================================== -->
      <div id="view-manpower" class="view-panel hidden space-y-6">
        <!-- Manpower KPI Row -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div class="bg-white rounded-xl p-5 border border-slate-200 metric-card">
            <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Peak Site Headcount</span>
            <div class="mt-2 text-3xl font-extrabold text-slate-900">{manpower.get('peak_labor', 0)} Workers</div>
            <p class="text-xs text-slate-500 mt-1">Projected peak workforce staffing requirement</p>
          </div>

          <div class="bg-white rounded-xl p-5 border border-slate-200 metric-card">
            <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Actual Labor Hours Spent</span>
            <div class="mt-2 text-3xl font-extrabold text-blue-600">{manpower.get('total_actual_hours', 0):,} hrs</div>
            <p class="text-xs text-slate-500 mt-1">Cumulative manhours recorded through cut-off</p>
          </div>

          <div class="bg-white rounded-xl p-5 border border-slate-200 metric-card">
            <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Total Planned Budget Hours</span>
            <div class="mt-2 text-3xl font-extrabold text-slate-700">{manpower.get('total_planned_hours', 0):,} hrs</div>
            <p class="text-xs text-slate-500 mt-1">Total baseline resource allowance</p>
          </div>
        </div>

        <!-- Manpower Monthly Histogram Chart -->
        <div class="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm">
          <div class="flex items-center justify-between mb-4">
            <div>
              <h3 class="text-base font-bold text-slate-900">Monthly Labor Hours & Headcount Distribution</h3>
              <p class="text-xs text-slate-500">Planned vs actual monthly labor hours with headcount curve</p>
            </div>
          </div>
          <div class="w-full h-80 relative">
            <canvas id="manpowerChart"></canvas>
          </div>
        </div>

        <!-- Craft Distribution Breakdown Table -->
        <div class="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm">
          <h3 class="text-base font-bold text-slate-900 mb-4">Trade & Craft Labor Hours Allocation</h3>
          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs border border-slate-100">
              <thead class="bg-slate-100 text-slate-600 font-semibold uppercase text-[10px] border-b border-slate-200">
                <tr>
                  <th class="py-2.5 px-3">Trade / Craft Discipline</th>
                  <th class="py-2.5 px-3 text-right">Planned Hours</th>
                  <th class="py-2.5 px-3 text-right">Actual Hours</th>
                  <th class="py-2.5 px-3 text-right">% Completed</th>
                  <th class="py-2.5 px-3">Progress</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                {"".join([f'''
                <tr class="hover:bg-slate-50">
                  <td class="py-3 px-3 font-semibold text-slate-800">{c['craft']}</td>
                  <td class="py-3 px-3 text-right font-mono text-slate-600">{c['planned_hours']:,} hrs</td>
                  <td class="py-3 px-3 text-right font-mono font-bold text-blue-600">{c['actual_hours']:,} hrs</td>
                  <td class="py-3 px-3 text-right font-bold text-slate-900">{c['percent_complete']}%</td>
                  <td class="py-3 px-3 w-48">
                    <div class="w-full bg-slate-200 rounded-full h-2 overflow-hidden">
                      <div class="bg-blue-600 h-2 rounded-full" style="width: {min(c['percent_complete'], 100.0)}%"></div>
                    </div>
                  </td>
                </tr>
                ''' for c in manpower.get('craft_distribution', [])])}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- ========================================== -->
      <!-- VIEW 4: CASHFLOW & SPEND PROFILE -->
      <!-- ========================================== -->
      <div id="view-cashflow" class="view-panel hidden space-y-6">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div class="bg-white rounded-xl p-5 border border-slate-200 metric-card">
            <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Total Contract Budget</span>
            <div class="mt-2 text-3xl font-extrabold text-slate-900">${cashflow.get('total_budget', 0):,.2f}</div>
            <p class="text-xs text-slate-500 mt-1">Total contract baseline valuation</p>
          </div>

          <div class="bg-white rounded-xl p-5 border border-slate-200 metric-card">
            <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Earned Value (EV)</span>
            <div class="mt-2 text-3xl font-extrabold text-blue-600">${proj.get('earned_value', 0):,.2f}</div>
            <p class="text-xs text-slate-500 mt-1">Budget cost of work performed to date</p>
          </div>

          <div class="bg-white rounded-xl p-5 border border-slate-200 metric-card">
            <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Cost Performance Index (CPI)</span>
            <div class="mt-2 text-3xl font-extrabold text-emerald-600">{proj.get('cpi', 1.0)}</div>
            <p class="text-xs text-slate-500 mt-1">Earned value / actual cost spent efficiency</p>
          </div>
        </div>

        <div class="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm">
          <div class="flex items-center justify-between mb-4">
            <div>
              <h3 class="text-base font-bold text-slate-900">Cumulative Cashflow Curve & Monthly Billing</h3>
              <p class="text-xs text-slate-500">Planned cumulative spend vs actual/forecast expenditures</p>
            </div>
          </div>
          <div class="w-full h-80 relative">
            <canvas id="cashflowChart"></canvas>
          </div>
        </div>
      </div>

      <!-- ========================================== -->
      <!-- VIEW 5: INVOICE & BILLING (IPC) -->
      <!-- ========================================== -->
      <div id="view-invoice" class="view-panel hidden space-y-6">
        <div class="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm">
          <div class="flex items-center justify-between mb-4">
            <div>
              <h3 class="text-base font-bold text-slate-900">Contractor Interim Payment Certificates (IPC)</h3>
              <p class="text-xs text-slate-500">Progress billing summary, 10% retainage withholding, and certified payments</p>
            </div>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs border border-slate-100">
              <thead class="bg-slate-100 text-slate-600 font-semibold uppercase text-[10px] border-b border-slate-200">
                <tr>
                  <th class="py-2.5 px-3">Certificate No.</th>
                  <th class="py-2.5 px-3">Valuation Period</th>
                  <th class="py-2.5 px-3 text-right">Gross Claimed</th>
                  <th class="py-2.5 px-3 text-right">Retention (10%)</th>
                  <th class="py-2.5 px-3 text-right">Advance Recovery (5%)</th>
                  <th class="py-2.5 px-3 text-right">Net Payable</th>
                  <th class="py-2.5 px-3 text-right">Cumulative Certified</th>
                  <th class="py-2.5 px-3 text-right">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                {"".join([f'''
                <tr class="hover:bg-slate-50">
                  <td class="py-3 px-3 font-mono font-bold text-slate-900">{inv['ipc_number']}</td>
                  <td class="py-3 px-3 font-medium text-slate-700">{inv['period']}</td>
                  <td class="py-3 px-3 text-right font-mono text-slate-600">${inv['gross_amount']:,.2f}</td>
                  <td class="py-3 px-3 text-right font-mono text-rose-600">-${inv['retention']:,.2f}</td>
                  <td class="py-3 px-3 text-right font-mono text-amber-600">-${inv['advance_recovery']:,.2f}</td>
                  <td class="py-3 px-3 text-right font-mono font-bold text-emerald-700">${inv['net_payable']:,.2f}</td>
                  <td class="py-3 px-3 text-right font-mono text-slate-800 font-semibold">${inv['cum_certified']:,.2f}</td>
                  <td class="py-3 px-3 text-right whitespace-nowrap">
                    <span class="inline-block px-2.5 py-0.5 rounded text-[11px] font-bold {'bg-emerald-100 text-emerald-800' if inv['status'] == 'Paid' else ('bg-blue-100 text-blue-800' if 'Pending' in inv['status'] else 'bg-amber-100 text-amber-800')}">
                      {inv['status']}
                    </span>
                  </td>
                </tr>
                ''' for inv in invoice])}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- ========================================== -->
      <!-- VIEW 6: 4D MODELING & TIMELINE SLIDER -->
      <!-- ========================================== -->
      <div id="view-fourd" class="view-panel hidden space-y-6">

        <!-- Top Timeline Slipper / Slider Bar Controls Card -->
        <div class="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm space-y-4">
          <div class="flex flex-wrap items-center justify-between gap-4">
            <div>
              <div class="flex items-center gap-2">
                <span class="inline-flex items-center justify-center w-7 h-7 rounded-lg bg-cyan-100 text-cyan-700 font-bold text-xs">4D</span>
                <h3 class="text-base font-bold text-slate-900">4D Construction Timeline Simulation</h3>
              </div>
              <p class="text-xs text-slate-500 mt-0.5">Scrub the timeline slider to simulate architectural building progression, activity sequences, and tile status over time.</p>
            </div>

            <!-- Date & Progress Live Badge -->
            <div class="flex items-center gap-3">
              <div class="bg-slate-50 border border-slate-200 rounded-xl px-4 py-2 text-right">
                <div class="text-[10px] font-semibold uppercase tracking-wider text-slate-400">Simulation Date</div>
                <div id="fourdSimDateBadge" class="text-base font-extrabold font-mono text-cyan-700">2024-01-08</div>
              </div>
              <div class="bg-slate-50 border border-slate-200 rounded-xl px-4 py-2 text-right">
                <div class="text-[10px] font-semibold uppercase tracking-wider text-slate-400">Cumulative Progress</div>
                <div id="fourdSimPctBadge" class="text-base font-extrabold font-mono text-blue-600">0.0%</div>
              </div>
            </div>
          </div>

          <!-- Timeline Tile Slipper Bar / Slider Component -->
          <div class="bg-slate-900 rounded-xl p-4 text-white shadow-inner">
            <div class="flex items-center justify-between text-xs text-slate-400 font-mono mb-2">
              <span id="fourdStartDateLabel">Start: 2024-01-08</span>
              <span class="text-cyan-400 font-semibold flex items-center gap-1.5">
                <span class="w-2 h-2 rounded-full bg-cyan-400 animate-ping"></span>
                <span>Time Slipper Bar</span>
              </span>
              <span id="fourdEndDateLabel">Target Finish: 2024-11-15</span>
            </div>

            <!-- Range Slider -->
            <div class="relative flex items-center py-2">
              <input type="range" id="fourdTimelineSlider" min="0" max="100" value="0" step="1"
                class="w-full h-3 bg-slate-700 rounded-lg appearance-none cursor-pointer accent-cyan-400 focus:outline-none focus:ring-2 focus:ring-cyan-400"
                oninput="onFourdSliderChange(this.value)">
            </div>

            <!-- Playback Controls -->
            <div class="flex flex-wrap items-center justify-between gap-3 pt-2 mt-2 border-t border-slate-800 text-xs">
              <div class="flex items-center gap-2">
                <button id="fourdBtnPlay" onclick="toggleFourdPlay()" class="flex items-center gap-1.5 px-3 py-1.5 bg-cyan-500 hover:bg-cyan-400 text-slate-950 font-bold rounded-lg transition shadow-xs">
                  <svg id="fourdPlayIcon" class="w-4 h-4" fill="currentColor" viewBox="0 0 20 20"><path d="M4 4l12 6-12 6z"/></svg>
                  <span id="fourdPlayText">Play Simulation</span>
                </button>
                <button onclick="stepFourdSlider(-1)" title="Previous Step" class="px-2.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 font-bold rounded-lg transition border border-slate-700">
                  &#9664;
                </button>
                <button onclick="stepFourdSlider(1)" title="Next Step" class="px-2.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 font-bold rounded-lg transition border border-slate-700">
                  &#9654;
                </button>
                <button onclick="resetFourdTimeline()" class="px-2.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white rounded-lg transition border border-slate-700 text-[11px]">
                  Reset to Start
                </button>
                <button onclick="jumpFourdToCutoff()" class="px-2.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-cyan-300 hover:text-cyan-200 rounded-lg transition border border-slate-700 text-[11px]">
                  Jump to Data Date
                </button>
              </div>

              <!-- Speed multi toggles -->
              <div class="flex items-center gap-1 bg-slate-800 p-1 rounded-lg border border-slate-700">
                <span class="text-[10px] text-slate-400 px-2 uppercase font-semibold">Speed:</span>
                <button onclick="setFourdSpeed(1)" id="fourdSpeed1" class="px-2 py-0.5 rounded text-[11px] font-bold bg-cyan-500 text-slate-950">1x</button>
                <button onclick="setFourdSpeed(2)" id="fourdSpeed2" class="px-2 py-0.5 rounded text-[11px] font-bold text-slate-400 hover:text-white">2x</button>
                <button onclick="setFourdSpeed(5)" id="fourdSpeed5" class="px-2 py-0.5 rounded text-[11px] font-bold text-slate-400 hover:text-white">5x</button>
              </div>
            </div>
          </div>
        </div>

        <!-- 4D Modeling Viewport & Status Panel (2-column layout) -->
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">

          <!-- Interactive 4D Isometric BIM Viewport (2 cols) -->
          <div class="lg:col-span-2 bg-slate-950 rounded-2xl p-6 border border-slate-800 shadow-xl flex flex-col justify-between">
            <div class="flex flex-wrap items-center justify-between gap-2 mb-4">
              <div>
                <span class="text-xs font-bold uppercase tracking-wider text-cyan-400">4D BIM Model Viewport</span>
                <h4 class="text-sm font-semibold text-white">Project Structural Zones & Architectural Elements</h4>
              </div>

              <!-- Status Legend -->
              <div class="flex items-center flex-wrap gap-2 text-[10px] font-semibold">
                <span class="flex items-center gap-1 text-slate-400"><span class="w-2.5 h-2.5 rounded bg-slate-700 border border-slate-600"></span> Unstarted</span>
                <span class="flex items-center gap-1 text-amber-400"><span class="w-2.5 h-2.5 rounded bg-amber-500"></span> In Progress</span>
                <span class="flex items-center gap-1 text-emerald-400"><span class="w-2.5 h-2.5 rounded bg-emerald-500"></span> Completed</span>
                <span class="flex items-center gap-1 text-rose-400"><span class="w-2.5 h-2.5 rounded bg-rose-500"></span> Delayed / Critical</span>
              </div>
            </div>

            <!-- 4D Isometric SVG Model Canvas -->
            <div class="relative w-full aspect-[16/10] bg-gradient-to-b from-slate-950 via-slate-900 to-slate-950 rounded-xl overflow-hidden border border-slate-800 flex items-center justify-center p-4">
              <svg id="fourdModelSvg" viewBox="0 0 900 550" class="w-full h-full select-none" style="filter: drop-shadow(0 10px 25px rgba(0,0,0,0.6));">
                <defs>
                  <linearGradient id="gridGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                    <stop offset="0%" stop-color="#0f172a" />
                    <stop offset="100%" stop-color="#020617" />
                  </linearGradient>
                  <filter id="tileGlow" x="-20%" y="-20%" width="140%" height="140%">
                    <feGaussianBlur stdDeviation="3" result="blur" />
                    <feComposite in="SourceGraphic" in2="blur" operator="over" />
                  </filter>
                </defs>

                <!-- Ground Grid Plane -->
                <g class="ground-grid" opacity="0.4">
                  <path d="M 150 420 L 450 530 L 750 420 L 450 310 Z" fill="url(#gridGrad)" stroke="#1e293b" stroke-width="2"/>
                  <line x1="250" y1="383" x2="550" y2="493" stroke="#1e293b" stroke-width="1"/>
                  <line x1="350" y1="347" x2="650" y2="457" stroke="#1e293b" stroke-width="1"/>
                  <line x1="550" y1="347" x2="250" y2="457" stroke="#1e293b" stroke-width="1"/>
                  <line x1="650" y1="383" x2="350" y2="493" stroke="#1e293b" stroke-width="1"/>
                </g>

                <!-- Zone 1: Site Infrastructure & Logistics (Surrounding apron) -->
                <g id="fourdTile-ZONE-SITE" class="fourd-tile cursor-pointer transition-all duration-300" onclick="selectFourdTile('ZONE-SITE', 'Site Works & Infrastructure')">
                  <!-- Base Plate Outer -->
                  <polygon points="120,410 450,540 780,410 450,280" fill="#1e293b" stroke="#334155" stroke-width="2" class="tile-face base-plane" opacity="0.6"/>
                  <text x="210" y="470" fill="#94a3b8" font-size="11" font-weight="bold" font-family="monospace">ZONE-SITE (Infrastructure)</text>
                </g>

                <!-- Zone 2: Substructure & Foundation (Below ground slab & piles) -->
                <g id="fourdTile-ZONE-SUB" class="fourd-tile cursor-pointer transition-all duration-300" onclick="selectFourdTile('ZONE-SUB', 'Substructure & Foundation')">
                  <!-- Isometric Box for Foundation Block -->
                  <!-- Top Face -->
                  <polygon points="310,360 450,410 590,360 450,310" fill="#334155" stroke="#475569" stroke-width="2" class="tile-top"/>
                  <!-- Left Face -->
                  <polygon points="310,360 450,410 450,440 310,390" fill="#1e293b" stroke="#475569" stroke-width="2" class="tile-left"/>
                  <!-- Right Face -->
                  <polygon points="450,410 590,360 590,390 450,440" fill="#0f172a" stroke="#475569" stroke-width="2" class="tile-right"/>
                  <text x="450" y="380" fill="#ffffff" font-size="12" font-weight="bold" text-anchor="middle" font-family="sans-serif">SUBSTRUCTURE</text>
                  <text x="450" y="396" fill="#cbd5e1" font-size="10" text-anchor="middle" font-family="monospace">Piles, Raft & Retaining</text>
                </g>

                <!-- Zone 3: Superstructure Podium / Tower Level 1-3 -->
                <g id="fourdTile-ZONE-TOW-L1" class="fourd-tile cursor-pointer transition-all duration-300" onclick="selectFourdTile('ZONE-TOW', 'Superstructure Tower Core')">
                  <polygon points="330,300 450,345 570,300 450,255" fill="#334155" stroke="#475569" stroke-width="2" class="tile-top"/>
                  <polygon points="330,300 450,345 450,375 330,330" fill="#1e293b" stroke="#475569" stroke-width="2" class="tile-left"/>
                  <polygon points="450,345 570,300 570,330 450,375" fill="#0f172a" stroke="#475569" stroke-width="2" class="tile-right"/>
                  <text x="450" y="315" fill="#ffffff" font-size="11" font-weight="bold" text-anchor="middle">TOWER FRAMING L1-L3</text>
                </g>

                <!-- Zone 4: Superstructure High-Rise Floors & Roof -->
                <g id="fourdTile-ZONE-TOW" class="fourd-tile cursor-pointer transition-all duration-300" onclick="selectFourdTile('ZONE-TOW', 'Superstructure Tower Core')">
                  <polygon points="350,230 450,270 550,230 450,190" fill="#334155" stroke="#475569" stroke-width="2" class="tile-top"/>
                  <polygon points="350,230 450,270 450,295 350,255" fill="#1e293b" stroke="#475569" stroke-width="2" class="tile-left"/>
                  <polygon points="450,270 550,230 550,255 450,295" fill="#0f172a" stroke="#475569" stroke-width="2" class="tile-right"/>
                  <text x="450" y="240" fill="#ffffff" font-size="11" font-weight="bold" text-anchor="middle">ROOF SLAB & TOPPING</text>
                </g>

                <!-- Zone 5: Central MEP Plant & Risers (Core block right) -->
                <g id="fourdTile-ZONE-MEP" class="fourd-tile cursor-pointer transition-all duration-300" onclick="selectFourdTile('ZONE-MEP', 'Central MEP Plant')">
                  <polygon points="575,280 675,320 675,270 575,230" fill="#1e293b" stroke="#475569" stroke-width="2" class="tile-left"/>
                  <polygon points="675,320 735,295 735,245 675,270" fill="#0f172a" stroke="#475569" stroke-width="2" class="tile-right"/>
                  <polygon points="575,230 675,270 735,245 635,205" fill="#334155" stroke="#475569" stroke-width="2" class="tile-top"/>
                  <text x="655" y="250" fill="#ffffff" font-size="10" font-weight="bold" text-anchor="middle">MEP PLANT</text>
                  <text x="655" y="265" fill="#cbd5e1" font-size="8" text-anchor="middle">HVAC & RISERS</text>
                </g>

                <!-- Zone 6: Finishes, Curtain Wall & Glazing Envelope (Front-left facade) -->
                <g id="fourdTile-ZONE-FIN" class="fourd-tile cursor-pointer transition-all duration-300" onclick="selectFourdTile('ZONE-FIN', 'Finishes & Curtain Wall')">
                  <!-- Glass / Cladding Panels on facade -->
                  <polygon points="240,290 340,330 340,250 240,210" fill="#1e293b" stroke="#475569" stroke-width="2" class="tile-left" opacity="0.85"/>
                  <polygon points="340,330 370,315 370,235 340,250" fill="#0f172a" stroke="#475569" stroke-width="2" class="tile-right" opacity="0.85"/>
                  <polygon points="240,210 340,250 370,235 270,195" fill="#334155" stroke="#475569" stroke-width="2" class="tile-top" opacity="0.85"/>
                  <text x="305" y="265" fill="#ffffff" font-size="10" font-weight="bold" text-anchor="middle">FACADE & FIN</text>
                  <text x="305" y="280" fill="#cbd5e1" font-size="8" text-anchor="middle">Curtain Wall</text>
                </g>

                <!-- Construction Crane Tower on roof/site -->
                <g class="crane-structure" opacity="0.75">
                  <line x1="500" y1="190" x2="500" y2="70" stroke="#f59e0b" stroke-width="3"/>
                  <line x1="430" y1="80" x2="560" y2="80" stroke="#f59e0b" stroke-width="2.5"/>
                  <line x1="500" y1="70" x2="560" y2="80" stroke="#d97706" stroke-width="1.5"/>
                  <line x1="500" y1="70" x2="430" y2="80" stroke="#d97706" stroke-width="1.5"/>
                  <line x1="540" y1="80" x2="540" y2="120" stroke="#f59e0b" stroke-width="1" stroke-dasharray="2 2"/>
                  <circle cx="540" cy="122" r="3" fill="#f59e0b"/>
                </g>
              </svg>

              <!-- Floating Simulation HUD Pill -->
              <div class="absolute top-4 left-4 bg-slate-900/90 backdrop-blur-md border border-slate-700/80 rounded-xl px-3 py-2 text-xs flex items-center gap-3 text-slate-300">
                <div>
                  <span class="text-[10px] text-slate-400 uppercase font-semibold">Stage:</span>
                  <span id="fourdStageLabel" class="font-bold text-white ml-1">Pre-construction</span>
                </div>
                <div class="w-px h-4 bg-slate-700"></div>
                <div>
                  <span class="text-[10px] text-slate-400 uppercase font-semibold">Active Tasks:</span>
                  <span id="fourdActiveTaskCount" class="font-bold text-cyan-400 ml-1">0</span>
                </div>
              </div>
            </div>

            <!-- Tile Status Quick Strip -->
            <div class="grid grid-cols-5 gap-2 mt-4 text-center">
              <div id="tileCard-ZONE-SUB" class="bg-slate-900 border border-slate-800 rounded-xl p-2 cursor-pointer hover:border-cyan-500/50 transition" onclick="selectFourdTile('ZONE-SUB', 'Substructure')">
                <div class="text-[10px] font-mono text-slate-400">ZONE-SUB</div>
                <div id="tilePct-ZONE-SUB" class="text-xs font-bold text-slate-300">0%</div>
                <div id="tileState-ZONE-SUB" class="text-[9px] uppercase font-semibold text-slate-500">Unstarted</div>
              </div>

              <div id="tileCard-ZONE-TOW" class="bg-slate-900 border border-slate-800 rounded-xl p-2 cursor-pointer hover:border-cyan-500/50 transition" onclick="selectFourdTile('ZONE-TOW', 'Superstructure')">
                <div class="text-[10px] font-mono text-slate-400">ZONE-TOW</div>
                <div id="tilePct-ZONE-TOW" class="text-xs font-bold text-slate-300">0%</div>
                <div id="tileState-ZONE-TOW" class="text-[9px] uppercase font-semibold text-slate-500">Unstarted</div>
              </div>

              <div id="tileCard-ZONE-MEP" class="bg-slate-900 border border-slate-800 rounded-xl p-2 cursor-pointer hover:border-cyan-500/50 transition" onclick="selectFourdTile('ZONE-MEP', 'MEP Plant')">
                <div class="text-[10px] font-mono text-slate-400">ZONE-MEP</div>
                <div id="tilePct-ZONE-MEP" class="text-xs font-bold text-slate-300">0%</div>
                <div id="tileState-ZONE-MEP" class="text-[9px] uppercase font-semibold text-slate-500">Unstarted</div>
              </div>

              <div id="tileCard-ZONE-FIN" class="bg-slate-900 border border-slate-800 rounded-xl p-2 cursor-pointer hover:border-cyan-500/50 transition" onclick="selectFourdTile('ZONE-FIN', 'Finishes & Curtain Wall')">
                <div class="text-[10px] font-mono text-slate-400">ZONE-FIN</div>
                <div id="tilePct-ZONE-FIN" class="text-xs font-bold text-slate-300">0%</div>
                <div id="tileState-ZONE-FIN" class="text-[9px] uppercase font-semibold text-slate-500">Unstarted</div>
              </div>

              <div id="tileCard-ZONE-SITE" class="bg-slate-900 border border-slate-800 rounded-xl p-2 cursor-pointer hover:border-cyan-500/50 transition" onclick="selectFourdTile('ZONE-SITE', 'Site & Infrastructure')">
                <div class="text-[10px] font-mono text-slate-400">ZONE-SITE</div>
                <div id="tilePct-ZONE-SITE" class="text-xs font-bold text-slate-300">0%</div>
                <div id="tileState-ZONE-SITE" class="text-[9px] uppercase font-semibold text-slate-500">Unstarted</div>
              </div>
            </div>
          </div>

          <!-- Active Construction Activities at Current Timeline Date (1 col) -->
          <div class="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between pb-3 border-b border-slate-100 mb-4">
                <div>
                  <h4 class="text-sm font-bold text-slate-900">Active Activities at Date</h4>
                  <p class="text-xs text-slate-500">Tasks executing during current timeline frame</p>
                </div>
                <span id="fourdActiveBadgeCount" class="px-2.5 py-0.5 rounded-full text-xs font-bold bg-cyan-100 text-cyan-800">0 Tasks</span>
              </div>

              <!-- Selected Tile Inspector Card -->
              <div id="fourdSelectedTileBox" class="p-3.5 rounded-xl bg-cyan-50 border border-cyan-100 mb-4">
                <div class="text-[10px] uppercase font-extrabold text-cyan-700 tracking-wider">Inspected Zone / Tile</div>
                <div id="fourdSelectedTileName" class="text-sm font-bold text-slate-900 mt-0.5">All Zones (Entire Project)</div>
                <div class="flex justify-between items-center text-xs text-slate-600 mt-2">
                  <span>Simulated Earned %:</span>
                  <span id="fourdSelectedTileProgress" class="font-bold text-cyan-700">0.0%</span>
                </div>
              </div>

              <!-- Task List Scroll Area -->
              <div class="space-y-2.5 max-h-[380px] overflow-y-auto pr-1" id="fourdActiveTasksList">
                <!-- Dynamically populated by JS simulation engine -->
              </div>
            </div>

            <div class="pt-4 border-t border-slate-100 mt-4 text-[11px] text-slate-400 flex items-center justify-between">
              <span>XER 4D Engine Synced</span>
              <button onclick="selectFourdTile(null, 'All Zones (Entire Project)')" class="text-blue-600 font-semibold hover:underline">Reset Selection</button>
            </div>
          </div>

        </div>

      </div>

    </main>

    <!-- Footer -->
    <footer class="bg-white border-t border-slate-200 mt-auto py-3 px-6 text-center text-xs text-slate-400">
      Primavera P6 Continuance Executive Construction Dashboard • Data Date: {proj['data_date']}
    </footer>

  </div>

  <!-- EMBEDDED PROJECT DATA & CLIENT LOGIC -->
  <script>
    const projectData = {full_payload_json};
    let currentSelectedArea = null;
    let lookaheadDays = 30;

    // Charts instances
    let scurveChartInstance = null;
    let manpowerChartInstance = null;
    let cashflowChartInstance = null;

    // View Switching Logic
    function switchView(viewName) {{
      document.querySelectorAll('.view-panel').forEach(p => p.classList.add('hidden'));
      document.querySelectorAll('.nav-link').forEach(l => l.classList.remove('active'));

      const targetView = document.getElementById('view-' + viewName);
      const targetNav = document.getElementById('nav-' + viewName);
      if (targetView) targetView.classList.remove('hidden');
      if (targetNav) targetNav.classList.add('active');

      const titles = {{
        overview: 'Overview & Interactive Layout',
        lookahead: 'Lookahead Schedule Management',
        manpower: 'Manpower & Labor Resources',
        cashflow: 'Cashflow Spend & Cost Curves',
        invoice: 'Contractor Payment Applications (IPC)',
        fourd: '4D Construction Timeline Simulation & Tile Slipper'
      }};
      document.getElementById('viewTitle').innerText = titles[viewName] || 'Construction Dashboard';

      if (viewName === 'manpower' && !manpowerChartInstance) {{
        initManpowerChart();
      }}
      if (viewName === 'cashflow' && !cashflowChartInstance) {{
        initCashflowChart();
      }}
      if (viewName === 'fourd') {{
        initFourdSimulation();
      }}
    }}

    // Area Selection Logic
    function selectArea(areaCode, areaName) {{
      currentSelectedArea = areaCode;
      document.getElementById('btnResetArea').classList.remove('hidden');
      document.getElementById('activeAreaBadge').classList.remove('hidden');
      document.getElementById('activeAreaNameText').innerText = areaName;
      document.getElementById('selectedZoneLabel').innerText = 'Selected Area Performance';
      document.getElementById('selectedZoneName').innerText = areaName + ' (' + areaCode + ')';

      // Highlight SVG polygon
      document.querySelectorAll('.zone-polygon').forEach(p => p.classList.remove('selected'));
      const activeGroup = document.querySelector(`.zone-group[data-zone="${{areaCode}}"]`);
      if (activeGroup) {{
        const polygon = activeGroup.querySelector('.zone-polygon');
        if (polygon) polygon.classList.add('selected');
      }}

      // Calculate area-specific KPIs
      updateAreaKpis(areaCode);
      applyGlobalFilters();
    }}

    function resetAreaSelection() {{
      currentSelectedArea = null;
      document.getElementById('btnResetArea').classList.add('hidden');
      document.getElementById('activeAreaBadge').classList.add('hidden');
      document.getElementById('selectedZoneLabel').innerText = 'Entire Project Performance';
      document.getElementById('selectedZoneName').innerText = projectData.project_info.project_name + ' (All Zones)';

      document.querySelectorAll('.zone-polygon').forEach(p => p.classList.remove('selected'));
      updateAreaKpis(null);
      applyGlobalFilters();
    }}

    function updateAreaKpis(areaCode) {{
      if (!areaCode) {{
        // Reset to overall project summary
        const s = projectData.summary;
        const p = projectData.project_info;
        document.getElementById('kpiActualPct').innerText = s.overall_progress_actual + '%';
        document.getElementById('kpiPlannedPct').innerText = s.overall_progress_planned + '%';
        document.getElementById('kpiProgressBar').style.width = Math.min(s.overall_progress_actual, 100) + '%';
        document.getElementById('kpiSpi').innerText = s.spi;
        document.getElementById('kpiVariancePct').innerText = (s.schedule_variance_pct >= 0 ? '+' : '') + s.schedule_variance_pct + '%';
        document.getElementById('kpiVarianceDays').innerText = (p.schedule_slippage_days > 0 ? '+' + p.schedule_slippage_days + 'd slip' : 'On Target');
        document.getElementById('kpiActiveTasks').innerText = s.status_counts['In Progress'];
        document.getElementById('kpiTotalTasks').innerText = s.total_activities;
        document.getElementById('kpiDoneTasks').innerText = s.status_counts['Completed'];
        document.getElementById('kpiCriticalTasks').innerText = s.float_dist['Critical (<=0d)'];
        document.getElementById('kpiDelayedTasks').innerText = s.delay_counts['Delayed'];
        document.getElementById('selectedZoneStatusBadge').innerText = s.status_indicator;
        return;
      }}

      const area = projectData.areas_summary.find(a => a.area_code === areaCode);
      if (area) {{
        document.getElementById('kpiActualPct').innerText = area.actual_pct + '%';
        document.getElementById('kpiPlannedPct').innerText = area.planned_pct + '%';
        document.getElementById('kpiProgressBar').style.width = Math.min(area.actual_pct, 100) + '%';
        document.getElementById('kpiSpi').innerText = area.spi;
        document.getElementById('kpiVariancePct').innerText = (area.var_pct >= 0 ? '+' : '') + area.var_pct + '%';
        document.getElementById('kpiVarianceDays').innerText = area.status_indicator;
        document.getElementById('kpiActiveTasks').innerText = area.in_progress_tasks;
        document.getElementById('kpiTotalTasks').innerText = area.total_tasks;
        document.getElementById('kpiDoneTasks').innerText = area.completed_tasks;
        document.getElementById('kpiCriticalTasks').innerText = area.critical_tasks;
        document.getElementById('kpiDelayedTasks').innerText = area.delayed_tasks;
        document.getElementById('selectedZoneStatusBadge').innerText = area.status_indicator;
      }}
    }}

    // Global Activity Code & WBS Filtering
    function applyGlobalFilters() {{
      const selectedWbs = document.getElementById('filterWbsLevel')?.value || '';
      const filterSelects = document.querySelectorAll('[id^="filterActv_"]');
      const actvFilters = {{}};
      filterSelects.forEach(sel => {{
        const key = sel.id.replace('filterActv_', '').replace('_', ' ');
        if (sel.value) actvFilters[key] = sel.value;
      }});

      const tbody = document.getElementById('masterTableBody');
      tbody.innerHTML = '';

      let filteredActs = projectData.all_activities.filter(a => {{
        // Area filter
        if (currentSelectedArea && a.area_code !== currentSelectedArea) return false;
        // WBS level filter
        if (selectedWbs && String(a.wbs_level) !== String(selectedWbs)) return false;
        // Activity code filters
        for (const [tName, val] of Object.entries(actvFilters)) {{
          const taskCodes = a.activity_codes || {{}};
          const hasCode = Object.entries(taskCodes).some(([k, obj]) => {{
            return (obj.code_val === val || obj.code_name === val);
          }});
          if (!hasCode) return false;
        }}
        return true;
      }});

      filteredActs.forEach(a => {{
        const tr = document.createElement('tr');
        tr.className = "hover:bg-slate-50 " + (a.is_critical && a.status !== 'Completed' ? 'bg-rose-50/40' : '');
        tr.innerHTML = `
          <td class="py-2.5 px-3 font-mono font-bold text-slate-700">${{a.task_code}}</td>
          <td class="py-2.5 px-3 font-semibold text-slate-900">
            ${{a.task_name}}
            ${{a.is_critical && a.status !== 'Completed' ? '<span class="ml-1 px-1.5 py-0.2 bg-rose-600 text-white rounded text-[9px] font-bold">CRITICAL</span>' : ''}}
          </td>
          <td class="py-2.5 px-2 text-slate-600 whitespace-nowrap"><span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-slate-100 text-slate-700">${{a.area_name}}</span></td>
          <td class="py-2.5 px-2 text-slate-600 whitespace-nowrap">${{a.wbs_name}}</td>
          <td class="py-2.5 px-2 whitespace-nowrap">
            <span class="inline-block px-2 py-0.5 rounded text-[10px] font-semibold ${{a.status === 'Completed' ? 'bg-emerald-100 text-emerald-700' : (a.status === 'In Progress' ? 'bg-blue-100 text-blue-700' : 'bg-slate-100 text-slate-600')}}">
              ${{a.status}}
            </span>
          </td>
          <td class="py-2.5 px-2 text-slate-500 whitespace-nowrap">${{a.bl_end}}</td>
          <td class="py-2.5 px-2 font-medium text-slate-900 whitespace-nowrap">${{a.curr_end}}</td>
          <td class="py-2.5 px-2 text-center font-bold whitespace-nowrap ${{a.finish_var_days > 0 ? 'text-rose-600' : (a.finish_var_days < 0 ? 'text-emerald-600' : 'text-slate-500')}}">
            ${{a.finish_var_days > 0 ? '+' + a.finish_var_days + 'd' : (a.finish_var_days < 0 ? a.finish_var_days + 'd' : '0d')}}
          </td>
          <td class="py-2.5 px-2 text-center font-mono font-bold whitespace-nowrap ${{a.total_float_days <= 0 ? 'text-rose-600' : (a.total_float_days <= 10 ? 'text-amber-600' : 'text-slate-600')}}">
            ${{a.total_float_days}} d
          </td>
          <td class="py-2.5 px-2 text-center font-bold text-slate-800 whitespace-nowrap">
            ${{a.actual_pct}}%
          </td>
        `;
        tbody.appendChild(tr);
      }});

      // Update lookahead table as well
      updateLookaheadTable(filteredActs);
    }}

    function resetAllFilters() {{
      const wbsSelect = document.getElementById('filterWbsLevel');
      if (wbsSelect) wbsSelect.value = '';
      document.querySelectorAll('[id^="filterActv_"]').forEach(sel => sel.value = '');
      resetAreaSelection();
    }}

    function filterActivitiesTable() {{
      const query = document.getElementById('overviewTableSearch').value.toLowerCase();
      const rows = document.getElementById('masterTableBody').getElementsByTagName('tr');
      for (let i = 0; i < rows.length; i++) {{
        const txt = rows[i].textContent.toLowerCase();
        rows[i].style.display = txt.includes(query) ? '' : 'none';
      }}
    }}

    // Lookahead Schedule Logic
    function setLookaheadDays(days) {{
      lookaheadDays = days;
      document.getElementById('btnLookahead30').className = days === 30 ? 'px-3 py-1.5 text-xs font-semibold rounded-lg bg-blue-600 text-white transition' : 'px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-100 text-slate-700 hover:bg-slate-200 transition';
      document.getElementById('btnLookahead60').className = days === 60 ? 'px-3 py-1.5 text-xs font-semibold rounded-lg bg-blue-600 text-white transition' : 'px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-100 text-slate-700 hover:bg-slate-200 transition';
      applyGlobalFilters();
    }}

    function updateLookaheadTable(acts) {{
      const key = lookaheadDays === 30 ? 'lookahead_30' : 'lookahead_60';
      const lookaheadSource = projectData.lookahead[key] || [];
      const tbody = document.getElementById('lookaheadTableBody');
      tbody.innerHTML = '';

      const matchedLookahead = lookaheadSource.filter(l => acts.some(a => a.task_code === l.task_code));
      if (matchedLookahead.length === 0) {{
        tbody.innerHTML = `<tr><td colspan="9" class="py-6 text-center text-slate-400">No activities scheduled within this ${{lookaheadDays}}-day window.</td></tr>`;
        return;
      }}

      matchedLookahead.forEach(item => {{
        const tr = document.createElement('tr');
        tr.className = "hover:bg-slate-50 " + (item.is_critical ? 'bg-rose-50/40' : '');
        tr.innerHTML = `
          <td class="py-2.5 px-3 font-mono font-bold text-slate-700">${{item.task_code}}</td>
          <td class="py-2.5 px-3 font-semibold text-slate-900">${{item.task_name}}</td>
          <td class="py-2.5 px-2 text-slate-600"><span class="px-2 py-0.5 rounded text-[10px] bg-slate-100 font-medium">${{item.area_name}}</span></td>
          <td class="py-2.5 px-2 text-slate-600 whitespace-nowrap">${{item.curr_start}}</td>
          <td class="py-2.5 px-2 font-medium text-slate-900 whitespace-nowrap">${{item.curr_end}}</td>
          <td class="py-2.5 px-2 text-center font-bold text-slate-700">${{item.remain_durn_days}} d</td>
          <td class="py-2.5 px-2 text-center font-bold ${{item.total_float_days <= 0 ? 'text-rose-600' : 'text-slate-600'}}">${{item.total_float_days}} d</td>
          <td class="py-2.5 px-2 text-center font-bold text-blue-600">${{item.actual_pct}}%</td>
          <td class="py-2.5 px-3"><span class="px-2 py-0.5 rounded text-[11px] font-semibold bg-blue-50 text-blue-700 border border-blue-200">${{item.window_status}}</span></td>
        `;
        tbody.appendChild(tr);
      }});
    }}

    // Custom Interactive Layout Upload Handler
    function handleCustomLayoutUpload(input) {{
      if (!input.files || !input.files[0]) return;
      const file = input.files[0];
      const reader = new FileReader();

      if (file.name.endsWith('.svg')) {{
        reader.onload = function(e) {{
          const content = e.target.result;
          const container = document.getElementById('layoutContainer');
          container.innerHTML = content;
          // Wire up any zones inside custom SVG
          container.querySelectorAll('[data-zone], polygon, path, rect').forEach(el => {{
            const zId = el.getAttribute('data-zone') || el.id;
            if (zId) {{
              el.classList.add('zone-polygon');
              el.onclick = () => selectArea(zId, el.getAttribute('data-name') || zId);
            }}
          }});
          alert('Custom interactive SVG layout loaded successfully!');
        }};
        reader.readAsText(file);
      }} else {{
        // Image layout (PNG/JPG)
        reader.onload = function(e) {{
          const imgUrl = e.target.result;
          const container = document.getElementById('layoutContainer');
          container.innerHTML = `
            <div class="relative w-full h-full flex items-center justify-center">
              <img src="${{imgUrl}}" alt="Custom Site Layout" class="max-h-[360px] object-contain rounded-lg">
              <div class="absolute inset-0 grid grid-cols-2 grid-rows-2 gap-2 p-4 pointer-events-none">
                <button class="pointer-events-auto bg-blue-600/30 hover:bg-blue-600/50 border border-blue-400 rounded-lg p-2 text-white font-bold text-xs" onclick="selectArea('ZONE-SUB', 'Substructure Area')">Zone 1: Substructure</button>
                <button class="pointer-events-auto bg-purple-600/30 hover:bg-purple-600/50 border border-purple-400 rounded-lg p-2 text-white font-bold text-xs" onclick="selectArea('ZONE-TOW', 'Superstructure Area')">Zone 2: Tower</button>
                <button class="pointer-events-auto bg-amber-600/30 hover:bg-amber-600/50 border border-amber-400 rounded-lg p-2 text-white font-bold text-xs" onclick="selectArea('ZONE-MEP', 'MEP Area')">Zone 3: MEP Plant</button>
                <button class="pointer-events-auto bg-emerald-600/30 hover:bg-emerald-600/50 border border-emerald-400 rounded-lg p-2 text-white font-bold text-xs" onclick="selectArea('ZONE-FIN', 'Finishes Area')">Zone 4: Finishes</button>
              </div>
            </div>
          `;
          alert('Custom layout image loaded with interactive zones overlay!');
        }};
        reader.readAsDataURL(file);
      }}
      input.value = '';
    }}

    // S-Curve Chart Initialization
    function initOverviewScurve() {{
      const scurveData = projectData.scurve;
      const ctx = document.getElementById('overviewScurveChart').getContext('2d');
      scurveChartInstance = new Chart(ctx, {{
        type: 'line',
        data: {{
          labels: scurveData.labels,
          datasets: [
            {{
              label: 'Planned %',
              data: scurveData.planned,
              borderColor: '#94a3b8',
              backgroundColor: 'rgba(148, 163, 184, 0.1)',
              borderWidth: 2,
              fill: false,
              tension: 0.3,
              pointRadius: 1,
            }},
            {{
              label: 'Actual %',
              data: scurveData.actual,
              borderColor: '#2563eb',
              backgroundColor: 'rgba(37, 99, 235, 0.1)',
              borderWidth: 2.8,
              fill: false,
              tension: 0.2,
              pointRadius: 3,
            }},
            {{
              label: 'Forecast %',
              data: scurveData.forecast,
              borderColor: '#f59e0b',
              borderDash: [5, 5],
              borderWidth: 2,
              fill: false,
              tension: 0.3,
              pointRadius: 1,
            }}
          ]
        }},
        options: {{
          responsive: true,
          maintainAspectRatio: false,
          plugins: {{ legend: {{ display: false }} }},
          scales: {{
            y: {{ min: 0, max: 100, ticks: {{ callback: v => v + '%' }} }},
            x: {{ ticks: {{ maxTicksLimit: 8, font: {{ size: 9 }} }} }}
          }}
        }}
      }});
    }}

    // Manpower Chart Initialization
    function initManpowerChart() {{
      const mp = projectData.manpower;
      const ctx = document.getElementById('manpowerChart').getContext('2d');
      manpowerChartInstance = new Chart(ctx, {{
        type: 'bar',
        data: {{
          labels: mp.months,
          datasets: [
            {{
              type: 'bar',
              label: 'Planned Labor Hours',
              data: mp.planned_hours,
              backgroundColor: 'rgba(148, 163, 184, 0.4)',
              borderRadius: 4,
            }},
            {{
              type: 'bar',
              label: 'Actual Labor Hours',
              data: mp.actual_hours,
              backgroundColor: '#2563eb',
              borderRadius: 4,
            }},
            {{
              type: 'line',
              label: 'Peak Headcount',
              data: mp.headcount_planned,
              borderColor: '#f59e0b',
              borderWidth: 2.5,
              yAxisID: 'y1',
              tension: 0.3,
            }}
          ]
        }},
        options: {{
          responsive: true,
          maintainAspectRatio: false,
          scales: {{
            y: {{ title: {{ display: true, text: 'Labor Hours' }} }},
            y1: {{ position: 'right', grid: {{ drawOnChartArea: false }}, title: {{ display: true, text: 'Workers' }} }}
          }}
        }}
      }});
    }}

    // Cashflow Chart Initialization
    function initCashflowChart() {{
      const cf = projectData.cashflow;
      const ctx = document.getElementById('cashflowChart').getContext('2d');
      cashflowChartInstance = new Chart(ctx, {{
        type: 'line',
        data: {{
          labels: cf.months,
          datasets: [
            {{
              label: 'Cumulative Planned ($)',
              data: cf.cum_planned,
              borderColor: '#64748b',
              borderWidth: 2,
              tension: 0.3,
            }},
            {{
              label: 'Cumulative Actual ($)',
              data: cf.cum_actual,
              borderColor: '#2563eb',
              borderWidth: 3,
              tension: 0.3,
            }}
          ]
        }},
        options: {{
          responsive: true,
          maintainAspectRatio: false,
          scales: {{
            y: {{ ticks: {{ callback: v => '$' + (v / 1000).toLocaleString() + 'k' }} }}
          }}
        }}
      }});
    }}

        // ========================================================
    // 4D MODELING & TIMELINE SLIDER SIMULATION ENGINE
    // ========================================================
    let fourdSimTimer = null;
    let fourdIsPlaying = false;
    let fourdPlaySpeed = 1; // 1x, 2x, 5x
    let fourdSimStartDate = null;
    let fourdSimEndDate = null;
    let fourdTotalDays = 0;
    let fourdSelectedZone = null;
    let fourdCurrentSimDate = null;

    function initFourdSimulation() {{
      const pInfo = projectData.project_info || {{}};
      const startStr = pInfo.baseline_start || '2024-01-08';
      const finishStr = pInfo.forecast_finish || pInfo.baseline_finish || '2024-11-15';
      fourdSimStartDate = new Date(startStr + 'T00:00:00');
      fourdSimEndDate = new Date(finishStr + 'T00:00:00');

      document.getElementById('fourdStartDateLabel').innerText = 'Start: ' + startStr;
      document.getElementById('fourdEndDateLabel').innerText = 'Target Finish: ' + finishStr;

      fourdTotalDays = Math.max(1, Math.round((fourdSimEndDate - fourdSimStartDate) / (1000 * 60 * 60 * 24)));
      const slider = document.getElementById('fourdTimelineSlider');
      slider.min = 0;
      slider.max = fourdTotalDays;

      if (!fourdCurrentSimDate) {{
        // Jump to data date or start
        const dataDtStr = pInfo.data_date;
        if (dataDtStr) {{
          jumpFourdToCutoff();
        }} else {{
          slider.value = 0;
          onFourdSliderChange(0);
        }}
      }} else {{
        const daysDiff = Math.round((fourdCurrentSimDate - fourdSimStartDate) / (1000 * 60 * 60 * 24));
        slider.value = Math.max(0, Math.min(daysDiff, fourdTotalDays));
        onFourdSliderChange(slider.value);
      }}
    }}

    function onFourdSliderChange(dayIndex) {{
      const dIndex = parseInt(dayIndex, 10);
      if (!fourdSimStartDate) return;

      const simTime = fourdSimStartDate.getTime() + (dIndex * 24 * 60 * 60 * 1000);
      fourdCurrentSimDate = new Date(simTime);

      const yyyy = fourdCurrentSimDate.getFullYear();
      const mm = String(fourdCurrentSimDate.getMonth() + 1).padStart(2, '0');
      const dd = String(fourdCurrentSimDate.getDate()).padStart(2, '0');
      const dateStr = `${{yyyy}}-${{mm}}-${{dd}}`;

      document.getElementById('fourdSimDateBadge').innerText = dateStr;

      // Update 4D elements status and active activities
      updateFourdElementsState(fourdCurrentSimDate);
    }}

    function updateFourdElementsState(currentDate) {{
      const activities = projectData.all_activities || [];
      const zones = ['ZONE-SUB', 'ZONE-TOW', 'ZONE-MEP', 'ZONE-FIN', 'ZONE-SITE'];
      const zoneStats = {{}};
      zones.forEach(z => {{
        zoneStats[z] = {{ total: 0, started: 0, completed: 0, active: 0, delayed: 0, sumWeight: 0, earnedWeight: 0 }};
      }});

      let totalProjectWeight = 0;
      let earnedProjectWeight = 0;
      const activeTasksAtDate = [];

      activities.forEach(act => {{
        const stStr = act.curr_start || act.bl_start;
        const fnStr = act.curr_end || act.bl_end;
        if (!stStr || !fnStr) return;

        const st = new Date(stStr + 'T00:00:00');
        const fn = new Date(fnStr + 'T23:59:59');
        const durn = Math.max(1, (fn - st) / (1000 * 60 * 60 * 24));
        const wt = act.weight > 0 ? act.weight : durn;
        totalProjectWeight += wt;

        const zCode = act.area_code;
        const zObj = zoneStats[zCode];
        if (zObj) {{
          zObj.total++;
          zObj.sumWeight += wt;
        }}

        if (currentDate < st) {{
          // Not started yet
        }} else if (currentDate >= fn) {{
          // Completed at this simulation date
          earnedProjectWeight += wt;
          if (zObj) {{
            zObj.completed++;
            zObj.earnedWeight += wt;
          }}
        }} else {{
          // In progress at this simulation date
          const elapsed = (currentDate - st) / (1000 * 60 * 60 * 24);
          const progRatio = Math.min(1.0, Math.max(0.0, elapsed / durn));
          earnedProjectWeight += (wt * progRatio);

          if (zObj) {{
            zObj.active++;
            zObj.started++;
            zObj.earnedWeight += (wt * progRatio);
            if (act.is_critical || (act.finish_var_days && act.finish_var_days > 0)) {{
              zObj.delayed++;
            }}
          }}

          activeTasksAtDate.push({{
            ...act,
            simPct: Math.round(progRatio * 100),
            simStatus: 'In Progress'
          }});
        }}
      }});

      const cumPct = totalProjectWeight > 0 ? Math.min(100.0, (earnedProjectWeight / totalProjectWeight) * 100).toFixed(1) : '0.0';
      document.getElementById('fourdSimPctBadge').innerText = cumPct + '%';

      // Determine Construction Stage Label
      let stage = 'Pre-construction';
      const numPct = parseFloat(cumPct);
      if (numPct >= 95) stage = 'Testing, Commissioning & Handover';
      else if (numPct >= 65) stage = 'Interior Finishes & Facades';
      else if (numPct >= 35) stage = 'Superstructure Framing';
      else if (numPct >= 10) stage = 'Substructure & Underground MEP';
      else if (numPct > 0) stage = 'Mobilization & Earthwork';
      document.getElementById('fourdStageLabel').innerText = stage;
      document.getElementById('fourdActiveTaskCount').innerText = activeTasksAtDate.length;
      document.getElementById('fourdActiveBadgeCount').innerText = activeTasksAtDate.length + ' Tasks';

      // Update 3D Tile visuals and bottom cards
      zones.forEach(z => {{
        const st = zoneStats[z];
        const pct = st.sumWeight > 0 ? Math.min(100, Math.round((st.earnedWeight / st.sumWeight) * 100)) : 0;
        let colorStatus = 'unstarted'; // unstarted, progress, complete, delayed

        if (pct >= 100 || (st.total > 0 && st.completed === st.total)) {{
          colorStatus = 'complete';
        }} else if (st.delayed > 0) {{
          colorStatus = 'delayed';
        }} else if (st.active > 0 || pct > 0) {{
          colorStatus = 'progress';
        }}

        // Color palettes
        const colors = {{
          unstarted: {{ top: '#334155', left: '#1e293b', right: '#0f172a', stroke: '#475569', opacity: 0.35, textClass: 'text-slate-500' }},
          progress: {{ top: '#0284c7', left: '#0369a1', right: '#075985', stroke: '#38bdf8', opacity: 0.95, textClass: 'text-cyan-400' }},
          complete: {{ top: '#059669', left: '#047857', right: '#065f46', stroke: '#34d399', opacity: 1.0, textClass: 'text-emerald-400' }},
          delayed: {{ top: '#e11d48', left: '#be123c', right: '#9f1239', stroke: '#fb7185', opacity: 1.0, textClass: 'text-rose-400' }}
        }};

        const c = colors[colorStatus];

        // Apply to 3D SVG tile elements
        const tileGroup = document.getElementById('fourdTile-' + z);
        if (tileGroup) {{
          const topFace = tileGroup.querySelector('.tile-top');
          const leftFace = tileGroup.querySelector('.tile-left');
          const rightFace = tileGroup.querySelector('.tile-right');
          const basePlane = tileGroup.querySelector('.base-plane');

          if (topFace) topFace.setAttribute('fill', c.top);
          if (leftFace) leftFace.setAttribute('fill', c.left);
          if (rightFace) rightFace.setAttribute('fill', c.right);
          if (basePlane) basePlane.setAttribute('fill', colorStatus === 'complete' ? '#065f46' : (colorStatus === 'progress' ? '#0369a1' : '#1e293b'));

          tileGroup.style.opacity = c.opacity;
        }}

        // If Superstructure L1 sub-group exists
        if (z === 'ZONE-TOW') {{
          const towL1 = document.getElementById('fourdTile-ZONE-TOW-L1');
          if (towL1) {{
            const towPct = pct;
            const towL1c = towPct >= 50 ? colors.complete : (towPct > 0 ? colors.progress : colors.unstarted);
            towL1.querySelectorAll('.tile-top').forEach(el => el.setAttribute('fill', towL1c.top));
            towL1.querySelectorAll('.tile-left').forEach(el => el.setAttribute('fill', towL1c.left));
            towL1.querySelectorAll('.tile-right').forEach(el => el.setAttribute('fill', towL1c.right));
            towL1.style.opacity = towL1c.opacity;
          }}
        }}

        // Update Bottom Card
        const pctEl = document.getElementById('tilePct-' + z);
        const stateEl = document.getElementById('tileState-' + z);
        const cardEl = document.getElementById('tileCard-' + z);
        if (pctEl) pctEl.innerText = pct + '%';
        if (stateEl) {{
          stateEl.innerText = colorStatus.toUpperCase();
          stateEl.className = `text-[9px] uppercase font-semibold ${{c.textClass}}`;
        }}
        if (cardEl) {{
          cardEl.className = `bg-slate-900 border rounded-xl p-2 cursor-pointer transition ${{fourdSelectedZone === z ? 'border-cyan-400 shadow-md shadow-cyan-500/20' : 'border-slate-800 hover:border-slate-700'}}`;
        }}
      }});

      // Render Active Activities List
      renderFourdTaskList(activeTasksAtDate);
    }}

    function renderFourdTaskList(taskList) {{
      const container = document.getElementById('fourdActiveTasksList');
      if (!container) return;

      let filtered = taskList;
      if (fourdSelectedZone) {{
        filtered = taskList.filter(t => t.area_code === fourdSelectedZone);
      }}

      if (filtered.length === 0) {{
        container.innerHTML = `
          <div class="p-6 text-center text-slate-400 border border-dashed border-slate-200 rounded-xl">
            <svg class="w-8 h-8 mx-auto text-slate-300 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>
            <p class="text-xs font-semibold">No active tasks executing on this date</p>
            <p class="text-[11px] text-slate-400 mt-0.5">Scrub the time slipper bar to step forward</p>
          </div>
        `;
        return;
      }}

      container.innerHTML = filtered.map(t => `
        <div class="p-2.5 rounded-xl border border-slate-100 bg-slate-50 hover:bg-white hover:border-cyan-300 transition shadow-2xs">
          <div class="flex items-center justify-between text-xs mb-1">
            <div class="flex items-center gap-1.5 font-bold text-slate-800">
              <span class="font-mono text-cyan-700 font-bold">${{t.task_code}}</span>
              <span class="truncate max-w-[170px]" title="${{t.task_name}}">${{t.task_name}}</span>
            </div>
            <span class="font-mono text-[11px] font-bold text-cyan-600">${{t.simPct}}%</span>
          </div>
          <div class="w-full bg-slate-200 rounded-full h-1.5 overflow-hidden">
            <div class="bg-cyan-500 h-1.5 rounded-full transition-all duration-200" style="width: ${{t.simPct}}%"></div>
          </div>
          <div class="flex items-center justify-between text-[10px] text-slate-500 mt-1.5">
            <span>${{t.area_name || t.area_code}}</span>
            <span class="font-mono text-slate-400">${{t.curr_start}} &rarr; ${{t.curr_end}}</span>
          </div>
        </div>
      `).join('');
    }}

    function selectFourdTile(zoneCode, zoneName) {{
      fourdSelectedZone = zoneCode;
      document.getElementById('fourdSelectedTileName').innerText = zoneName || 'All Zones (Entire Project)';

      // Calculate progress of selected zone
      let progText = '0.0%';
      if (zoneCode) {{
        const pctEl = document.getElementById('tilePct-' + zoneCode);
        if (pctEl) progText = pctEl.innerText;
      }} else {{
        progText = document.getElementById('fourdSimPctBadge').innerText;
      }}
      document.getElementById('fourdSelectedTileProgress').innerText = progText;

      // Re-filter the active tasks list
      if (fourdCurrentSimDate) {{
        updateFourdElementsState(fourdCurrentSimDate);
      }}
    }}

    function toggleFourdPlay() {{
      fourdIsPlaying = !fourdIsPlaying;
      const btn = document.getElementById('fourdBtnPlay');
      const text = document.getElementById('fourdPlayText');
      const icon = document.getElementById('fourdPlayIcon');

      if (fourdIsPlaying) {{
        text.innerText = 'Pause';
        btn.classList.remove('bg-cyan-500', 'text-slate-950');
        btn.classList.add('bg-amber-500', 'text-slate-950');
        icon.innerHTML = '<rect x="4" y="3" width="4" height="14"/><rect x="12" y="3" width="4" height="14"/>';
        startFourdPlayback();
      }} else {{
        text.innerText = 'Play Simulation';
        btn.classList.remove('bg-amber-500');
        btn.classList.add('bg-cyan-500', 'text-slate-950');
        icon.innerHTML = '<path d="M4 4l12 6-12 6z"/>';
        stopFourdPlayback();
      }}
    }}

    function startFourdPlayback() {{
      stopFourdPlayback();
      const slider = document.getElementById('fourdTimelineSlider');
      const intervalMs = Math.max(40, Math.round(250 / fourdPlaySpeed));

      fourdSimTimer = setInterval(() => {{
        let currVal = parseInt(slider.value, 10);
        if (currVal >= parseInt(slider.max, 10)) {{
          currVal = 0; // Loop or stop
        }} else {{
          currVal += 1;
        }}
        slider.value = currVal;
        onFourdSliderChange(currVal);
      }}, intervalMs);
    }}

    function stopFourdPlayback() {{
      if (fourdSimTimer) {{
        clearInterval(fourdSimTimer);
        fourdSimTimer = null;
      }}
    }}

    function stepFourdSlider(delta) {{
      if (fourdIsPlaying) toggleFourdPlay();
      const slider = document.getElementById('fourdTimelineSlider');
      let currVal = parseInt(slider.value, 10) + delta;
      currVal = Math.max(0, Math.min(currVal, parseInt(slider.max, 10)));
      slider.value = currVal;
      onFourdSliderChange(currVal);
    }}

    function resetFourdTimeline() {{
      if (fourdIsPlaying) toggleFourdPlay();
      const slider = document.getElementById('fourdTimelineSlider');
      slider.value = 0;
      onFourdSliderChange(0);
    }}

    function jumpFourdToCutoff() {{
      if (fourdIsPlaying) toggleFourdPlay();
      const pInfo = projectData.project_info || {{}};
      const dataDtStr = pInfo.data_date;
      if (!dataDtStr || !fourdSimStartDate) return;

      const dataDt = new Date(dataDtStr + 'T00:00:00');
      const dayDiff = Math.max(0, Math.round((dataDt - fourdSimStartDate) / (1000 * 60 * 60 * 24)));
      const slider = document.getElementById('fourdTimelineSlider');
      slider.value = Math.min(dayDiff, parseInt(slider.max, 10));
      onFourdSliderChange(slider.value);
    }}

    function setFourdSpeed(speed) {{
      fourdPlaySpeed = speed;
      [1, 2, 5].forEach(s => {{
        const btn = document.getElementById('fourdSpeed' + s);
        if (btn) {{
          if (s === speed) {{
            btn.className = 'px-2 py-0.5 rounded text-[11px] font-bold bg-cyan-500 text-slate-950';
          }} else {{
            btn.className = 'px-2 py-0.5 rounded text-[11px] font-bold text-slate-400 hover:text-white';
          }}
        }}
      }});
      if (fourdIsPlaying) {{
        startFourdPlayback();
      }}
    }}


    // ========================================================
    // CLIENT-SIDE XER ENGINE (Supports Offline / Standalone file:// mode)
    // ========================================================
    let clientRawBaselineText = null;

    function parseXerTextClient(text) {{
      const lines = text.split(String.fromCharCode(10));
      const tables = {{}};
      let currentTable = null;
      let currentFields = [];

      for (let line of lines) {{
        line = line.trim();
        if (!line) continue;
        const parts = line.split(String.fromCharCode(9));
        const tag = parts[0];

        if (tag === '%T') {{
          currentTable = parts[1];
          tables[currentTable] = [];
          currentFields = [];
        }} else if (tag === '%F' && currentTable) {{
          currentFields = parts.slice(1);
        }} else if (tag === '%R' && currentTable && currentFields.length > 0) {{
          const row = {{}};
          const vals = parts.slice(1);
          for (let i = 0; i < currentFields.length; i++) {{
            row[currentFields[i]] = vals[i] || '';
          }}
          tables[currentTable].push(row);
        }}
      }}
      return tables;
    }}

    function parseDateClient(str) {{
      if (!str) return null;
      const d = new Date(str.replace(' ', 'T'));
      return isNaN(d.getTime()) ? null : d;
    }}

    function formatDateClient(d) {{
      if (!d) return '-';
      const pad = n => String(n).padStart(2, '0');
      return `${{d.getFullYear()}}-${{pad(d.getMonth() + 1)}}-${{pad(d.getDate())}}`;
    }}

    function calcPlannedPctClient(startDt, finishDt, dataDt) {{
      if (!startDt || !finishDt) return 0.0;
      if (dataDt <= startDt) return 0.0;
      if (dataDt >= finishDt) return 100.0;
      const total = finishDt.getTime() - startDt.getTime();
      if (total <= 0) return 100.0;
      const elapsed = dataDt.getTime() - startDt.getTime();
      return Math.min(100.0, Math.max(0.0, Math.round((elapsed / total) * 1000) / 10.0));
    }}

    function parseXerProjectClient(text) {{
      const tables = parseXerTextClient(text);
      const projRow = (tables['PROJECT'] && tables['PROJECT'][0]) || {{}};
      const dataDate = parseDateClient(projRow.last_recalc_date) || new Date();

      const wbs = {{}};
      (tables['PROJWBS'] || []).forEach(w => {{
        wbs[w.wbs_id] = {{
          wbs_id: w.wbs_id,
          wbs_short_name: w.wbs_short_name || '',
          wbs_name: w.wbs_name || 'WBS',
          parent_wbs_id: w.parent_wbs_id || '',
          level: 1
        }};
      }});
      Object.values(wbs).forEach(w => {{
        let lvl = 1;
        let curr = w.parent_wbs_id;
        let guard = 0;
        while (curr && wbs[curr] && guard < 10) {{
          lvl++;
          curr = wbs[curr].parent_wbs_id;
          guard++;
        }}
        w.level = lvl;
      }});

      const actvTypes = {{}};
      (tables['ACTVTYPE'] || []).forEach(t => {{
        actvTypes[t.actv_code_type_id] = t.actv_code_type || '';
      }});
      const actvCodes = {{}};
      (tables['ACTVCODE'] || []).forEach(c => {{
        actvCodes[c.actv_code_id] = {{
          code_id: c.actv_code_id,
          type_id: c.actv_code_type_id,
          type_name: actvTypes[c.actv_code_type_id] || 'General',
          code_val: c.actv_code_val || '',
          code_name: c.actv_code_name || ''
        }};
      }});
      const taskActv = {{}};
      (tables['TASKACTV'] || []).forEach(ta => {{
        if (!taskActv[ta.task_id]) taskActv[ta.task_id] = {{}};
        const cObj = actvCodes[ta.actv_code_id];
        if (cObj) {{
          taskActv[ta.task_id][cObj.type_name] = cObj;
        }}
      }});

      const rsrcs = {{}};
      (tables['RSRC'] || []).forEach(r => {{
        rsrcs[r.rsrc_id] = {{
          rsrc_id: r.rsrc_id,
          rsrc_short_name: r.rsrc_short_name || '',
          rsrc_name: r.rsrc_name || '',
          rsrc_type: r.rsrc_type || 'RT_Labor'
        }};
      }});

      const taskRsrc = {{}};
      (tables['TASKRSRC'] || []).forEach(tr => {{
        if (!taskRsrc[tr.task_id]) taskRsrc[tr.task_id] = [];
        const rObj = rsrcs[tr.rsrc_id] || {{}};
        taskRsrc[tr.task_id].push({{
          rsrc_id: tr.rsrc_id,
          rsrc_name: rObj.rsrc_name || 'Labor',
          target_qty: parseFloat(tr.target_qty || 0),
          act_qty: parseFloat(tr.act_qty || 0),
          target_cost: parseFloat(tr.target_cost || 0),
          act_cost: parseFloat(tr.act_cost || 0)
        }});
      }});

      const tasks = {{}};
      (tables['TASK'] || []).forEach(t => {{
        const isMile = (t.task_type === 'TT_Mile' || t.task_type === 'TT_FinMile');
        const statusMap = {{ 'TK_NotStart': 'Not Started', 'TK_Active': 'In Progress', 'TK_Complete': 'Completed' }};
        const status = statusMap[t.status_code] || 'Not Started';
        const targetDurn = (parseFloat(t.target_durn_hr_cnt || 0) / 8.0);
        const remainDurn = (parseFloat(t.remain_durn_hr_cnt || 0) / 8.0);
        const pctComp = parseFloat(t.phys_complete_pct || 0);
        const tf = (parseFloat(t.total_float_hr_cnt || 0) / 8.0);

        const actCodes = taskActv[t.task_id] || {{}};
        let areaCode = 'ZONE-GEN';
        let areaName = 'General Site';
        if (actCodes['Project Area']) {{
          areaCode = actCodes['Project Area'].code_val;
          areaName = actCodes['Project Area'].code_name;
        }}

        const wObj = wbs[t.wbs_id] || {{}};

        tasks[t.task_code] = {{
          task_id: t.task_id,
          task_code: t.task_code,
          task_name: t.task_name || 'Task',
          task_type: t.task_type || 'TT_Task',
          is_milestone: isMile,
          status: status,
          wbs_id: t.wbs_id,
          wbs_name: wObj.wbs_name || 'WBS',
          wbs_level: wObj.level || 1,
          area_code: areaCode,
          area_name: areaName,
          target_durn_days: targetDurn,
          remain_durn_days: remainDurn,
          pct_comp: pctComp,
          total_float_days: tf,
          is_critical: tf <= 0 && status !== 'Completed',
          target_start: parseDateClient(t.target_start_date),
          target_end: parseDateClient(t.target_end_date),
          curr_start: parseDateClient(t.act_start_date || t.early_start_date || t.target_start_date),
          curr_end: parseDateClient(t.act_end_date || t.early_end_date || t.target_end_date),
          activity_codes: actCodes,
          resources: taskRsrc[t.task_id] || []
        }};
      }});

      return {{
        project_info: {{
          project_name: projRow.proj_short_name || 'Construction Project',
          data_date: formatDateClient(dataDate),
          data_date_raw: dataDate,
          plan_start_date: parseDateClient(projRow.plan_start_date),
          plan_end_date: parseDateClient(projRow.plan_end_date),
          scd_end_date: parseDateClient(projRow.scd_end_date)
        }},
        wbs,
        tasks,
        actv_types: actvTypes,
        actv_codes: actvCodes
      }};
    }}

    function compareSchedulesClient(baseline, update) {{
      const dataDate = update.project_info.data_date_raw || baseline.project_info.data_date_raw || new Date();
      const bTasks = baseline.tasks;
      const uTasks = update.tasks;

      const matchedActivities = [];
      const wbsMap = {{}};
      const areasMap = {{}};

      Object.values(update.wbs).forEach(w => {{
        wbsMap[w.wbs_id] = {{
          wbs_id: w.wbs_id,
          wbs_code: w.wbs_short_name,
          wbs_name: w.wbs_name,
          level: w.level,
          weight: 0,
          planned_earned_dur: 0,
          actual_earned_dur: 0,
          total_tasks: 0,
          completed_tasks: 0,
          in_progress_tasks: 0,
          critical_tasks: 0,
          delayed_tasks: 0
        }};
      }});

      let totalWeight = 0;
      let plannedEarnedWeight = 0;
      let actualEarnedWeight = 0;

      const criticalActivities = [];
      const delayedActivities = [];
      const milestones = [];

      const statusCounts = {{ 'Completed': 0, 'In Progress': 0, 'Not Started': 0 }};
      const delayCounts = {{ 'Delayed': 0, 'On Track': 0, 'Ahead': 0 }};
      const floatDist = {{ 'Critical (<=0d)': 0, 'Near Critical (1-10d)': 0, 'Healthy (>10d)': 0 }};

      const allWbsLevels = new Set();
      const activityCodeFilters = {{}};

      Object.keys(uTasks).forEach(taskCode => {{
        const uTask = uTasks[taskCode];
        const bTask = bTasks[taskCode] || {{}};
        const status = uTask.status;
        statusCounts[status] = (statusCounts[status] || 0) + 1;

        const bDur = (bTask.target_durn_days !== undefined ? bTask.target_durn_days : uTask.target_durn_days) || 0;
        const isMile = uTask.is_milestone;
        const weight = (!isMile && bDur > 0) ? bDur : (!isMile ? 1.0 : 0.0);

        const blStart = bTask.target_start || bTask.curr_start;
        const blEnd = bTask.target_end || bTask.curr_end;
        const upStart = uTask.curr_start;
        const upEnd = uTask.curr_end;

        const plannedPct = calcPlannedPctClient(blStart, blEnd, dataDate);
        const actualPct = uTask.pct_comp;

        let finishVarDays = 0;
        if (upEnd && blEnd) {{
          finishVarDays = Math.round((upEnd.getTime() - blEnd.getTime()) / (1000 * 60 * 60 * 24));
        }}

        let delayStatus = 'On Track';
        if (status === 'Completed') {{
          delayStatus = finishVarDays > 0 ? 'Delayed' : 'On Track';
        }} else {{
          if (finishVarDays > 2 || (actualPct < plannedPct - 5.0)) delayStatus = 'Delayed';
          else if (finishVarDays < -2 || (actualPct > plannedPct + 5.0)) delayStatus = 'Ahead';
          else delayStatus = 'On Track';
        }}

        if (!isMile) {{
          delayCounts[delayStatus] = (delayCounts[delayStatus] || 0) + 1;
        }}

        const tf = uTask.total_float_days;
        if (status !== 'Completed') {{
          if (tf <= 0) floatDist['Critical (<=0d)']++;
          else if (tf <= 10) floatDist['Near Critical (1-10d)']++;
          else floatDist['Healthy (>10d)']++;
        }}

        totalWeight += weight;
        plannedEarnedWeight += weight * (plannedPct / 100.0);
        actualEarnedWeight += weight * (actualPct / 100.0);

        allWbsLevels.add(uTask.wbs_level);
        if (wbsMap[uTask.wbs_id]) {{
          const wentry = wbsMap[uTask.wbs_id];
          wentry.weight += weight;
          wentry.planned_earned_dur += weight * (plannedPct / 100.0);
          wentry.actual_earned_dur += weight * (actualPct / 100.0);
          wentry.total_tasks++;
          if (status === 'Completed') wentry.completed_tasks++;
          else if (status === 'In Progress') wentry.in_progress_tasks++;
          if (uTask.is_critical) wentry.critical_tasks++;
          if (delayStatus === 'Delayed') wentry.delayed_tasks++;
        }}

        const aCode = uTask.area_code;
        const aName = uTask.area_name;
        if (!areasMap[aCode]) {{
          areasMap[aCode] = {{
            area_code: aCode,
            area_name: aName,
            weight: 0,
            planned_earned_dur: 0,
            actual_earned_dur: 0,
            total_tasks: 0,
            completed_tasks: 0,
            in_progress_tasks: 0,
            critical_tasks: 0,
            delayed_tasks: 0
          }};
        }}
        const aentry = areasMap[aCode];
        aentry.weight += weight;
        aentry.planned_earned_dur += weight * (plannedPct / 100.0);
        aentry.actual_earned_dur += weight * (actualPct / 100.0);
        aentry.total_tasks++;
        if (status === 'Completed') aentry.completed_tasks++;
        else if (status === 'In Progress') aentry.in_progress_tasks++;
        if (uTask.is_critical) aentry.critical_tasks++;
        if (delayStatus === 'Delayed') aentry.delayed_tasks++;

        Object.entries(uTask.activity_codes).forEach(([typeName, cObj]) => {{
          if (!activityCodeFilters[typeName]) activityCodeFilters[typeName] = new Set();
          activityCodeFilters[typeName].add(cObj.code_val);
        }});

        const actRow = {{
          task_id: uTask.task_id,
          task_code: uTask.task_code,
          task_name: uTask.task_name,
          is_milestone: isMile,
          status: status,
          wbs_id: uTask.wbs_id,
          wbs_name: uTask.wbs_name,
          wbs_level: uTask.wbs_level,
          area_code: aCode,
          area_name: aName,
          bl_start: formatDateClient(blStart),
          bl_end: formatDateClient(blEnd),
          curr_start: formatDateClient(upStart),
          curr_end: formatDateClient(upEnd),
          target_durn_days: Math.round(uTask.target_durn_days * 10) / 10,
          remain_durn_days: Math.round(uTask.remain_durn_days * 10) / 10,
          planned_pct: Math.round(plannedPct * 10) / 10,
          actual_pct: Math.round(actualPct * 10) / 10,
          finish_var_days: finishVarDays,
          total_float_days: Math.round(tf * 10) / 10,
          is_critical: uTask.is_critical,
          delay_status: delayStatus,
          activity_codes: uTask.activity_codes,
          resources: uTask.resources
        }};

        matchedActivities.push(actRow);
        if (isMile) milestones.push(actRow);
        if (uTask.is_critical && status !== 'Completed') criticalActivities.push(actRow);
        if (delayStatus === 'Delayed' && status !== 'Completed') delayedActivities.push(actRow);
      }});

      const overallPlanned = totalWeight > 0 ? Math.round((plannedEarnedWeight / totalWeight) * 1000) / 10.0 : 0.0;
      const overallActual = totalWeight > 0 ? Math.round((actualEarnedWeight / totalWeight) * 1000) / 10.0 : 0.0;
      const spi = overallPlanned > 0 ? Math.round((overallActual / overallPlanned) * 100) / 100.0 : 1.0;
      const svPct = Math.round((overallActual - overallPlanned) * 10) / 10.0;

      const wbsSummary = Object.values(wbsMap).filter(w => w.total_tasks > 0).map(w => {{
        const pPct = w.weight > 0 ? Math.round((w.planned_earned_dur / w.weight) * 1000) / 10.0 : 0.0;
        const aPct = w.weight > 0 ? Math.round((w.actual_earned_dur / w.weight) * 1000) / 10.0 : 0.0;
        return {{
          wbs_id: w.wbs_id,
          wbs_code: w.wbs_code,
          wbs_name: w.wbs_name,
          level: w.level,
          total_tasks: w.total_tasks,
          completed_tasks: w.completed_tasks,
          in_progress_tasks: w.in_progress_tasks,
          critical_tasks: w.critical_tasks,
          delayed_tasks: w.delayed_tasks,
          planned_pct: pPct,
          actual_pct: aPct,
          var_pct: Math.round((aPct - pPct) * 10) / 10.0
        }};
      }});

      const areasSummary = Object.values(areasMap).map(a => {{
        const pPct = a.weight > 0 ? Math.round((a.planned_earned_dur / a.weight) * 1000) / 10.0 : 0.0;
        const aPct = a.weight > 0 ? Math.round((a.actual_earned_dur / a.weight) * 1000) / 10.0 : 0.0;
        const aSpi = pPct > 0 ? Math.round((aPct / pPct) * 100) / 100.0 : 1.0;
        return {{
          area_code: a.area_code,
          area_name: a.area_name,
          total_tasks: a.total_tasks,
          completed_tasks: a.completed_tasks,
          in_progress_tasks: a.in_progress_tasks,
          critical_tasks: a.critical_tasks,
          delayed_tasks: a.delayed_tasks,
          planned_pct: pPct,
          actual_pct: aPct,
          spi: aSpi,
          var_pct: Math.round((aPct - pPct) * 10) / 10.0,
          status_indicator: aSpi >= 1.0 ? 'Ahead / On Track' : (aSpi >= 0.9 ? 'Minor Delay' : 'Behind Schedule')
        }};
      }});

      // Generate Lookahead client-side
      const lookahead30 = [];
      const lookahead60 = [];
      matchedActivities.forEach(a => {{
        if (a.is_milestone || a.status === 'Completed') return;
        const cStart = parseDateClient(a.curr_start);
        const cEnd = parseDateClient(a.curr_end);
        if (!cStart || !cEnd) return;

        const diffStartDays = (cStart.getTime() - dataDate.getTime()) / (1000 * 60 * 60 * 24);
        const in30 = (a.status === 'In Progress' && cEnd >= dataDate) || (diffStartDays >= 0 && diffStartDays <= 30);
        const in60 = (a.status === 'In Progress' && cEnd >= dataDate) || (diffStartDays >= 0 && diffStartDays <= 60);

        const winStatus = a.status === 'In Progress' ? 'Active In Progress' : 'Starting in Window';
        const item = {{ ...a, window_status: winStatus }};
        if (in30) lookahead30.push(item);
        if (in60) lookahead60.push(item);
      }});

      const filterCodesObj = {{}};
      Object.keys(activityCodeFilters).sort().forEach(k => {{
        filterCodesObj[k] = Array.from(activityCodeFilters[k]).sort();
      }});

      return {{
        project_info: {{
          project_name: update.project_info.project_name,
          baseline_name: baseline.project_info.project_name,
          data_date: formatDateClient(dataDate),
          plan_finish: formatDateClient(baseline.project_info.plan_end_date),
          forecast_finish: formatDateClient(update.project_info.scd_end_date || update.project_info.plan_end_date),
          schedule_slippage_days: (update.project_info.scd_end_date && baseline.project_info.plan_end_date)
            ? Math.round((update.project_info.scd_end_date.getTime() - baseline.project_info.plan_end_date.getTime()) / (1000 * 60 * 60 * 24))
            : 0
        }},
        summary: {{
          overall_progress_planned: overallPlanned,
          overall_progress_actual: overallActual,
          spi: spi,
          schedule_variance_pct: svPct,
          status_indicator: spi >= 1.0 ? 'Ahead / On Track' : (spi >= 0.9 ? 'Minor Delay' : 'Behind Schedule'),
          total_activities: matchedActivities.length,
          status_counts: statusCounts,
          delay_counts: delayCounts,
          float_dist: floatDist
        }},
        wbs_summary: wbsSummary,
        areas_summary: areasSummary,
        filters_meta: {{
          wbs_levels: Array.from(allWbsLevels).sort((a,b) => a - b),
          activity_codes: filterCodesObj
        }},
        lookahead: {{
          lookahead_30: lookahead30,
          lookahead_60: lookahead60
        }},
        milestones,
        critical_activities: criticalActivities,
        delayed_activities: delayedActivities,
        all_activities: matchedActivities,
        scurve: projectData.scurve,
        manpower: projectData.manpower,
        cashflow: projectData.cashflow,
        invoice: projectData.invoice
      }};
    }}

    function rebindDashboardUI(newData) {{
      Object.assign(projectData, newData);

      // Refresh Area KPIs
      updateAreaKpis(currentSelectedArea);

      // Rebuild Filter UI dropdowns
      rebuildFilterDropdowns(projectData.filters_meta);

      // Rebuild WBS cards
      const wbsContainer = document.getElementById('wbsProgressContainer');
      if (wbsContainer && projectData.wbs_summary) {{
        wbsContainer.innerHTML = projectData.wbs_summary.map(w => `
          <div class="p-3 rounded-xl bg-slate-50 border border-slate-100">
            <div class="flex justify-between items-center text-xs mb-1.5">
              <span class="font-bold text-slate-800">${{w.wbs_name}}</span>
              <span class="text-slate-500 font-medium">${{w.actual_pct}}% / <span class="text-slate-400">${{w.planned_pct}}% Plan</span></span>
            </div>
            <div class="w-full bg-slate-200 rounded-full h-2 overflow-hidden flex">
              <div class="${{w.actual_pct >= w.planned_pct ? 'bg-emerald-500' : 'bg-blue-600'}} h-2 rounded-full" style="width: ${{Math.min(w.actual_pct, 100.0)}}%"></div>
            </div>
            <div class="flex justify-between items-center text-[11px] text-slate-500 mt-1.5">
              <span>${{w.total_tasks}} Tasks (${{w.completed_tasks}} Done, ${{w.in_progress_tasks}} Active)</span>
              <span class="font-semibold ${{w.var_pct < -2.0 ? 'text-rose-600' : (w.var_pct >= 0 ? 'text-emerald-600' : 'text-slate-600')}}">
                Var: ${{w.var_pct >= 0 ? '+' : ''}}${{w.var_pct}}%
              </span>
            </div>
          </div>
        `).join('');
      }}

      applyGlobalFilters();
    }}

    function rebuildFilterDropdowns(filtersMeta) {{
      if (!filtersMeta) return;
      const wbsSelect = document.getElementById('filterWbsLevel');
      if (wbsSelect && filtersMeta.wbs_levels) {{
        const curVal = wbsSelect.value;
        wbsSelect.innerHTML = '<option value="">All Levels</option>' + filtersMeta.wbs_levels.map(l => `<option value="${{l}}">Level ${{l}}</option>`).join('');
        wbsSelect.value = curVal;
      }}
    }}

    // ========================================================
    // UPLOAD HANDLERS (With Automated Offline / Standalone Fallback)
    // ========================================================
    async function uploadDashboardBaseline(input) {{
      if (!input.files || !input.files[0]) return;
      const file = input.files[0];

      const isFileProtocol = window.location.protocol === 'file:';
      if (isFileProtocol) {{
        const reader = new FileReader();
        reader.onload = function(e) {{
          clientRawBaselineText = e.target.result;
          alert('Baseline XER parsed successfully in standalone mode! You can now load an update XER.');
        }};
        reader.readAsText(file);
        input.value = '';
        return;
      }}

      const fd = new FormData();
      fd.append('baseline', file);
      try {{
        const res = await fetch('/api/upload-baseline', {{ method: 'POST', body: fd }});
        if (res.ok) {{
          alert('Baseline updated successfully! You can now upload weekly updates.');
        }} else {{
          const err = await res.json();
          alert('Error: ' + (err.detail || 'Failed to upload baseline'));
        }}
      }} catch (e) {{
        console.warn('Backend fetch failed, falling back to in-browser parsing:', e);
        const reader = new FileReader();
        reader.onload = function(evt) {{
          clientRawBaselineText = evt.target.result;
          alert('Baseline XER parsed successfully in offline mode! You can now upload weekly updates.');
        }};
        reader.readAsText(file);
      }}
      input.value = '';
    }}

    async function uploadDashboardUpdate(input) {{
      if (!input.files || !input.files[0]) return;
      const file = input.files[0];

      const isFileProtocol = window.location.protocol === 'file:';
      if (isFileProtocol) {{
        const reader = new FileReader();
        reader.onload = function(e) {{
          const updateText = e.target.result;
          processClientSideUpdate(updateText);
        }};
        reader.readAsText(file);
        input.value = '';
        return;
      }}

      const fd = new FormData();
      fd.append('update', file);
      try {{
        const res = await fetch('/api/upload-update', {{ method: 'POST', body: fd }});
        if (res.ok) {{
          const data = await res.json();
          window.location.href = `/api/dashboard/update/${{data.update_id}}`;
          return;
        }} else {{
          const err = await res.json();
          alert('Server notice: ' + (err.detail || 'Processing via client-side engine'));
        }}
      }} catch (e) {{
        console.warn('Backend fetch failed, falling back to in-browser processing:', e);
      }}

      const reader = new FileReader();
      reader.onload = function(evt) {{
        processClientSideUpdate(evt.target.result);
      }};
      reader.readAsText(file);
      input.value = '';
    }}

    function processClientSideUpdate(updateText) {{
      try {{
        const updateProj = parseXerProjectClient(updateText);
        let baseProj;
        if (clientRawBaselineText) {{
          baseProj = parseXerProjectClient(clientRawBaselineText);
        }} else {{
          baseProj = updateProj;
        }}
        const compared = compareSchedulesClient(baseProj, updateProj);
        rebindDashboardUI(compared);
        alert('Update XER loaded and analyzed successfully! Dashboard and area KPIs updated.');
      }} catch (err) {{
        console.error('Client XER processing error:', err);
        alert('Could not parse update XER: ' + err.message);
      }}
    }}

    // Initialize View on Page Load
    window.addEventListener('DOMContentLoaded', () => {{
      initOverviewScurve();
      applyGlobalFilters();
    }});
  </script>
</body>
</html>
"""
    return html
