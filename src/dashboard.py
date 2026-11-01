"""
Construction Progress Report Dashboard Generator
Generates a standalone, interactive, executive-ready HTML progress report
with interactive S-Curve, WBS progress comparison, KPI scorecards,
Milestone tracker, and Critical/Delayed activity watchlists.
"""

import json
from typing import Dict, Any


def generate_html_dashboard(data: Dict[str, Any]) -> str:
    proj = data["project_info"]
    summary = data["summary"]
    wbs_list = data["wbs_summary"]
    milestones = data["milestones"]
    critical = data["critical_activities"]
    delayed = data["delayed_activities"]
    all_acts = data["all_activities"]
    scurve = data["scurve"]

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

    # Convert complex structures to JSON for embed
    scurve_json = json.dumps(scurve)
    wbs_json = json.dumps(wbs_list)

    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Progress Dashboard | {proj['project_name']}</title>
  <!-- Tailwind CSS -->
  <script src="https://cdn.tailwindcss.com"></script>
  <!-- Chart.js -->
  <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
  <style>
    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap');
    body {{
      font-family: 'Inter', sans-serif;
      background-color: #f8fafc;
      color: #1e293b;
    }}
    .metric-card {{
      box-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.05), 0 1px 2px 0 rgba(0, 0, 0, 0.03);
      transition: all 0.2s ease-in-out;
    }}
    .metric-card:hover {{
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.08), 0 2px 4px -1px rgba(0, 0, 0, 0.04);
    }}
    @media print {{
      .no-print {{ display: none !important; }}
      body {{ background-color: #ffffff; }}
      .page-break {{ page-break-before: always; }}
    }}
  </style>
</head>
<body class="min-h-screen flex flex-col">

  <!-- Top Navigation & Header -->
  <header class="bg-slate-900 text-white border-b border-slate-800 sticky top-0 z-50 shadow-md">
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-3.5 flex flex-wrap items-center justify-between gap-4">
      <div class="flex items-center space-x-3">
        <div class="p-2 bg-blue-600 rounded-lg text-white">
          <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
          </svg>
        </div>
        <div>
          <h1 class="text-lg font-bold tracking-tight text-white">{proj['project_name']}</h1>
          <div class="flex items-center space-x-2 text-xs text-slate-400">
            <span>Cut-off / Data Date: <strong class="text-slate-200">{proj['data_date']}</strong></span>
            <span>•</span>
            <span>Baseline: <strong class="text-slate-200">{proj['baseline_name']}</strong></span>
          </div>
        </div>
      </div>

      <div class="flex items-center gap-3 no-print">
        <button onclick="window.print()" class="inline-flex items-center px-3.5 py-1.5 border border-slate-700 rounded-md text-xs font-medium text-slate-200 bg-slate-800 hover:bg-slate-700 transition">
          <svg class="w-4 h-4 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z" />
          </svg>
          Print / PDF
        </button>
      </div>
    </div>
  </header>

  <!-- Main Content Area -->
  <main class="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">

    <!-- KPI Summary Row -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">

      <!-- Actual Progress Card -->
      <div class="bg-white rounded-xl p-5 border border-slate-200 metric-card">
        <div class="flex items-center justify-between">
          <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Overall Progress</span>
          <span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold {spi_color}">
            {summary['status_indicator']}
          </span>
        </div>
        <div class="mt-3 flex items-baseline justify-between">
          <div>
            <span class="text-3xl font-extrabold text-slate-900">{summary['overall_progress_actual']}%</span>
            <span class="text-xs text-slate-500 ml-1">Actual</span>
          </div>
          <div class="text-right">
            <span class="text-xl font-bold text-slate-600">{summary['overall_progress_planned']}%</span>
            <span class="text-xs text-slate-500 ml-1">Planned</span>
          </div>
        </div>
        <!-- Progress mini bar -->
        <div class="w-full bg-slate-100 rounded-full h-2.5 mt-3 overflow-hidden flex">
          <div class="bg-blue-600 h-2.5 rounded-full" style="width: {min(summary['overall_progress_actual'], 100.0)}%"></div>
        </div>
        <div class="mt-2 flex justify-between text-xs text-slate-500">
          <span>Variance: <strong class="{ 'text-emerald-600' if summary['schedule_variance_pct'] >= 0 else 'text-rose-600' }">{summary['schedule_variance_pct']:+}%</strong></span>
          <span>SPI: <strong class="text-slate-800">{summary['spi']}</strong></span>
        </div>
      </div>

      <!-- Schedule Performance Index (SPI) -->
      <div class="bg-white rounded-xl p-5 border border-slate-200 metric-card">
        <div class="flex items-center justify-between">
          <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Schedule Index (SPI)</span>
          <span class="h-2.5 w-2.5 rounded-full {spi_badge}"></span>
        </div>
        <div class="mt-3">
          <div class="text-3xl font-extrabold text-slate-900">{summary['spi']}</div>
          <p class="text-xs text-slate-500 mt-1">
            { "Progress is proceeding ahead of planned rate" if summary['spi'] > 1.0 else ("Progress matches planned schedule" if summary['spi'] == 1.0 else "Site progress is trending behind baseline target") }
          </p>
        </div>
        <div class="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-xs">
          <span class="text-slate-500">Target Range:</span>
          <span class="font-semibold text-slate-700">0.95 - 1.05</span>
        </div>
      </div>

      <!-- Schedule Dates & Delay -->
      <div class="bg-white rounded-xl p-5 border border-slate-200 metric-card">
        <div class="flex items-center justify-between">
          <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Project Completion</span>
          <span class="px-2 py-0.5 text-xs font-medium rounded { 'bg-rose-100 text-rose-700' if proj['schedule_slippage_days'] > 0 else 'bg-emerald-100 text-emerald-700' }">
            {f"+{proj['schedule_slippage_days']} Days Slip" if proj['schedule_slippage_days'] > 0 else ("Ahead" if proj['schedule_slippage_days'] < 0 else "On Target")}
          </span>
        </div>
        <div class="mt-3">
          <div class="text-2xl font-bold text-slate-900">{proj['forecast_finish']}</div>
          <div class="text-xs text-slate-500 mt-0.5">Forecast Completion</div>
        </div>
        <div class="mt-3 pt-2 border-t border-slate-100 flex justify-between text-xs text-slate-500">
          <span>BL Finish: <strong class="text-slate-700">{proj['baseline_finish']}</strong></span>
          <span>BL Start: <strong class="text-slate-700">{proj['baseline_start']}</strong></span>
        </div>
      </div>

      <!-- Activity Status Breakdown -->
      <div class="bg-white rounded-xl p-5 border border-slate-200 metric-card">
        <div class="flex items-center justify-between">
          <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Schedule Activities</span>
          <span class="text-xs font-bold text-slate-600">{summary['total_activities']} Total</span>
        </div>
        <div class="mt-3 grid grid-cols-3 gap-2 text-center">
          <div class="p-2 rounded bg-emerald-50 border border-emerald-100">
            <div class="text-lg font-bold text-emerald-700">{summary['status_counts']['Completed']}</div>
            <div class="text-[10px] text-emerald-600 uppercase font-semibold">Done</div>
          </div>
          <div class="p-2 rounded bg-blue-50 border border-blue-100">
            <div class="text-lg font-bold text-blue-700">{summary['status_counts']['In Progress']}</div>
            <div class="text-[10px] text-blue-600 uppercase font-semibold">Active</div>
          </div>
          <div class="p-2 rounded bg-slate-50 border border-slate-200">
            <div class="text-lg font-bold text-slate-700">{summary['status_counts']['Not Started']}</div>
            <div class="text-[10px] text-slate-500 uppercase font-semibold">Future</div>
          </div>
        </div>
        <div class="mt-3 pt-2 border-t border-slate-100 flex justify-between text-xs">
          <span class="text-slate-500">Critical Activities:</span>
          <span class="font-bold text-rose-600">{summary['float_dist']['Critical (<=0d)']} Active</span>
        </div>
      </div>

    </div>

    <!-- S-Curve Section -->
    <div class="bg-white rounded-xl p-6 border border-slate-200 shadow-sm">
      <div class="flex flex-wrap items-center justify-between gap-4 mb-4">
        <div>
          <h2 class="text-base font-bold text-slate-900">Project S-Curve (Planned vs Actual / Forecast)</h2>
          <p class="text-xs text-slate-500">Cumulative progress progression over project timeline based on baseline duration weights</p>
        </div>
        <div class="flex items-center space-x-4 text-xs font-medium">
          <span class="inline-flex items-center"><span class="w-3 h-3 rounded-full bg-slate-400 mr-1.5"></span> Planned (Baseline)</span>
          <span class="inline-flex items-center"><span class="w-3 h-3 rounded-full bg-blue-600 mr-1.5"></span> Actual to Date</span>
          <span class="inline-flex items-center"><span class="w-3 h-3 rounded-full border border-dashed border-amber-500 bg-amber-100 mr-1.5"></span> Forecast</span>
        </div>
      </div>
      <div class="w-full h-80 relative">
        <canvas id="scurveChart"></canvas>
      </div>
    </div>

    <!-- Two Column: WBS Breakdown & Milestones -->
    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">

      <!-- WBS Progress Breakdown -->
      <div class="bg-white rounded-xl p-6 border border-slate-200 shadow-sm flex flex-col justify-between">
        <div>
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-base font-bold text-slate-900">WBS Summary Progress</h2>
            <span class="text-xs font-semibold text-slate-400">Baseline Weight Rolled Up</span>
          </div>
          <div class="space-y-4 max-h-[380px] overflow-y-auto pr-1">
            {"".join([f'''
            <div class="p-3 rounded-lg bg-slate-50 border border-slate-100">
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
      </div>

      <!-- Key Project Milestones -->
      <div class="bg-white rounded-xl p-6 border border-slate-200 shadow-sm flex flex-col justify-between">
        <div>
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-base font-bold text-slate-900">Key Project Milestones</h2>
            <span class="text-xs text-slate-500">{len(milestones)} Key Dates</span>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-100 text-slate-600 font-semibold border-b border-slate-200 uppercase text-[10px]">
                <tr>
                  <th class="py-2.5 px-3">Milestone Name</th>
                  <th class="py-2.5 px-2">BL Date</th>
                  <th class="py-2.5 px-2">Current/Actual</th>
                  <th class="py-2.5 px-2 text-center">Variance</th>
                  <th class="py-2.5 px-2 text-right">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                {"".join([f'''
                <tr class="hover:bg-slate-50 transition">
                  <td class="py-2.5 px-3 font-semibold text-slate-800">
                    <div>{m['task_name']}</div>
                    <div class="text-[10px] text-slate-400 font-normal">{m['task_code']}</div>
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

    </div>

    <!-- Critical Path & Delayed Activities Table -->
    <div class="bg-white rounded-xl p-6 border border-slate-200 shadow-sm">
      <div class="flex flex-wrap items-center justify-between gap-4 mb-4">
        <div>
          <h2 class="text-base font-bold text-slate-900">Critical Path & Delayed Activities Watchlist</h2>
          <p class="text-xs text-slate-500">Activities with Total Float &le; 0 or finish date slipping behind baseline</p>
        </div>
        <div class="flex items-center gap-2">
          <input type="text" id="tableSearch" onkeyup="filterTable()" placeholder="Search activities or ID..." class="px-3 py-1.5 text-xs border border-slate-300 rounded-lg focus:outline-none focus:ring-1 focus:ring-blue-500 w-48 sm:w-64">
        </div>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full text-left text-xs border border-slate-100" id="activityTable">
          <thead class="bg-slate-100 text-slate-600 font-semibold uppercase text-[10px] border-b border-slate-200">
            <tr>
              <th class="py-2.5 px-3">Activity ID</th>
              <th class="py-2.5 px-3">Activity Description</th>
              <th class="py-2.5 px-3">WBS Category</th>
              <th class="py-2.5 px-2">Status</th>
              <th class="py-2.5 px-2">BL Start</th>
              <th class="py-2.5 px-2">BL Finish</th>
              <th class="py-2.5 px-2">Forecast Finish</th>
              <th class="py-2.5 px-2 text-center">Variance</th>
              <th class="py-2.5 px-2 text-center">Total Float</th>
              <th class="py-2.5 px-2 text-center">% Comp</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            {"".join([f'''
            <tr class="hover:bg-slate-50 {'bg-rose-50/40' if a['is_critical'] and a['status'] != 'Completed' else ''}">
              <td class="py-2.5 px-3 font-bold font-mono text-slate-700">{a['task_code']}</td>
              <td class="py-2.5 px-3 font-semibold text-slate-900">
                {a['task_name']}
                { '<span class="ml-1 px-1.5 py-0.2 bg-rose-600 text-white rounded text-[9px] font-bold">CRITICAL</span>' if a['is_critical'] and a['status'] != 'Completed' else '' }
              </td>
              <td class="py-2.5 px-3 text-slate-600">{a['wbs_name']}</td>
              <td class="py-2.5 px-2 whitespace-nowrap">
                <span class="inline-block px-2 py-0.5 rounded text-[10px] font-semibold {'bg-emerald-100 text-emerald-700' if a['status'] == 'Completed' else ('bg-blue-100 text-blue-700' if a['status'] == 'In Progress' else 'bg-slate-100 text-slate-600')}">
                  {a['status']}
                </span>
              </td>
              <td class="py-2.5 px-2 text-slate-500 whitespace-nowrap">{a['bl_start']}</td>
              <td class="py-2.5 px-2 text-slate-500 whitespace-nowrap">{a['bl_end']}</td>
              <td class="py-2.5 px-2 font-medium text-slate-900 whitespace-nowrap">{a['curr_end']}</td>
              <td class="py-2.5 px-2 text-center font-bold whitespace-nowrap { 'text-rose-600' if a['finish_var_days'] > 0 else ('text-emerald-600' if a['finish_var_days'] < 0 else 'text-slate-500') }">
                {f"+{a['finish_var_days']}d" if a['finish_var_days'] > 0 else (f"{a['finish_var_days']}d" if a['finish_var_days'] < 0 else "0d")}
              </td>
              <td class="py-2.5 px-2 text-center font-mono font-bold whitespace-nowrap { 'text-rose-600' if a['total_float_days'] <= 0 else ('text-amber-600' if a['total_float_days'] <= 10 else 'text-slate-600') }">
                {a['total_float_days']} d
              </td>
              <td class="py-2.5 px-2 text-center font-bold text-slate-800 whitespace-nowrap">
                {a['actual_pct']}%
              </td>
            </tr>
            ''' for a in (critical + [x for x in delayed if x not in critical])])}
          </tbody>
        </table>
      </div>
    </div>

  </main>

  <footer class="bg-white border-t border-slate-200 mt-12 py-4">
    <div class="max-w-7xl mx-auto px-4 text-center text-xs text-slate-400">
      Primavera P6 Progress Report Generator • Generated on {proj['data_date']}
    </div>
  </footer>

  <script>
    // Initialize Chart.js S-Curve
    const scurveData = {scurve_json};
    const ctx = document.getElementById('scurveChart').getContext('2d');

    new Chart(ctx, {{
      type: 'line',
      data: {{
        labels: scurveData.labels,
        datasets: [
          {{
            label: 'Planned Cumulative %',
            data: scurveData.planned,
            borderColor: '#94a3b8',
            backgroundColor: 'rgba(148, 163, 184, 0.1)',
            borderWidth: 2.5,
            fill: false,
            tension: 0.3,
            pointRadius: 2,
          }},
          {{
            label: 'Actual Cumulative %',
            data: scurveData.actual,
            borderColor: '#2563eb',
            backgroundColor: 'rgba(37, 99, 235, 0.1)',
            borderWidth: 3,
            fill: false,
            tension: 0.2,
            pointRadius: 4,
            pointHoverRadius: 6,
          }},
          {{
            label: 'Forecast Cumulative %',
            data: scurveData.forecast,
            borderColor: '#f59e0b',
            borderDash: [5, 5],
            borderWidth: 2.5,
            fill: false,
            tension: 0.3,
            pointRadius: 2,
          }}
        ]
      }},
      options: {{
        responsive: true,
        maintainAspectRatio: false,
        interaction: {{
          mode: 'index',
          intersect: false,
        }},
        plugins: {{
          tooltip: {{
            callbacks: {{
              label: function(context) {{
                return context.dataset.label + ': ' + (context.parsed.y !== null ? context.parsed.y + '%' : 'N/A');
              }}
            }}
          }},
          legend: {{
            display: false
          }}
        }},
        scales: {{
          y: {{
            min: 0,
            max: 100,
            ticks: {{
              callback: function(value) {{ return value + '%'; }}
            }},
            grid: {{
              color: '#f1f5f9'
            }}
          }},
          x: {{
            grid: {{
              display: false
            }},
            ticks: {{
              maxTicksLimit: 14,
              font: {{
                size: 10
              }}
            }}
          }}
        }}
      }}
    }});

    // Filter table function
    function filterTable() {{
      const input = document.getElementById('tableSearch');
      const filter = input.value.toUpperCase();
      const table = document.getElementById('activityTable');
      const tr = table.getElementsByTagName('tr');

      for (let i = 1; i < tr.length; i++) {{
        const tdId = tr[i].getElementsByTagName('td')[0];
        const tdName = tr[i].getElementsByTagName('td')[1];
        const tdWbs = tr[i].getElementsByTagName('td')[2];
        if (tdId || tdName || tdWbs) {{
          const txtId = tdId ? tdId.textContent || tdId.innerText : '';
          const txtName = tdName ? tdName.textContent || tdName.innerText : '';
          const txtWbs = tdWbs ? tdWbs.textContent || tdWbs.innerText : '';
          if (txtId.toUpperCase().indexOf(filter) > -1 ||
              txtName.toUpperCase().indexOf(filter) > -1 ||
              txtWbs.toUpperCase().indexOf(filter) > -1) {{
            tr[i].style.display = '';
          }} else {{
            tr[i].style.display = 'none';
          }}
        }}
      }}
    }}
  </script>
</body>
</html>
"""
    return html
