// AlaaRep Schedule & Earned Value Dashboard Controller
let scheduleData = null;
let sCurveChart = null;
let currentMetricMode = 'units'; // 'units' or 'cost'
let selectedLookaheadDays = 30;
let currentPeriodFilter = 'ALL';

document.addEventListener('DOMContentLoaded', () => {
  initEventListeners();
  loadDefaultData();
});

function initEventListeners() {
  const fileInput = document.getElementById('excelFileInput');
  fileInput.addEventListener('change', handleFileUpload);

  const btnToggleMatrixSCurve = document.getElementById('btnToggleMatrixSCurve');
  if (btnToggleMatrixSCurve) {
    btnToggleMatrixSCurve.addEventListener('click', toggleMatrixSCurve);
  }

  // Redraw matrix overlay on scroll and window resize
  const scrollContainer = document.getElementById('matrixScrollContainer');
  if (scrollContainer) {
    scrollContainer.addEventListener('scroll', () => {
      // Reposition canvas overlay transform based on scroll
      const canvas = document.getElementById('matrixSCurveCanvas');
      if (canvas) {
        canvas.style.transform = `translate(${-scrollContainer.scrollLeft}px, ${-scrollContainer.scrollTop}px)`;
      }
    });
  }

  window.addEventListener('resize', () => {
    drawMatrixSCurveOverlay();
  });

  const btnPrintPdf = document.getElementById('btnPrintPdf');
  if (btnPrintPdf) {
    btnPrintPdf.addEventListener('click', () => {
      window.print();
    });
  }

  const btnDownloadSample = document.getElementById('btnDownloadSample');
  btnDownloadSample.addEventListener('click', () => {
    window.location.href = '/api/schedule/sample';
  });

  // Metric mode toggles (Units vs Cost)
  const btnModeUnits = document.getElementById('btnModeUnits');
  const btnModeCost = document.getElementById('btnModeCost');

  btnModeUnits.addEventListener('click', () => {
    if (currentMetricMode !== 'units') {
      currentMetricMode = 'units';
      btnModeUnits.classList.add('active');
      btnModeCost.classList.remove('active');
      updateModeLabels();
      renderDashboard();
    }
  });

  btnModeCost.addEventListener('click', () => {
    if (currentMetricMode !== 'cost') {
      currentMetricMode = 'cost';
      btnModeCost.classList.add('active');
      btnModeUnits.classList.remove('active');
      updateModeLabels();
      renderDashboard();
    }
  });

  // Lookahead window pills
  const lookaheadBtns = document.querySelectorAll('.btn-lookahead');
  lookaheadBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      lookaheadBtns.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      selectedLookaheadDays = parseInt(btn.getAttribute('data-days'), 10);
      fetchLookaheadData(selectedLookaheadDays);
    });
  });

  // Lookahead Export button
  const btnExportLookahead = document.getElementById('btnExportLookahead');
  btnExportLookahead.addEventListener('click', exportLookaheadToCsv);

  // Period filter tabs
  const tabBtns = document.querySelectorAll('.period-filter-tabs .tab-btn');
  tabBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      tabBtns.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      currentPeriodFilter = btn.getAttribute('data-filter');
      renderPeriodSummaryTable();
    });
  });

  // Search and filter
  const searchInput = document.getElementById('searchInput');
  const statusFilter = document.getElementById('statusFilter');
  searchInput.addEventListener('input', () => renderMatrixTable());
  statusFilter.addEventListener('change', () => renderMatrixTable());

  // Drag and drop dropzone
  const dropZone = document.getElementById('dropZone');
  dropZone.addEventListener('dragover', (e) => {
    e.preventDefault();
    dropZone.classList.add('dragover');
  });

  dropZone.addEventListener('dragleave', () => {
    dropZone.classList.remove('dragover');
  });

  dropZone.addEventListener('drop', (e) => {
    e.preventDefault();
    dropZone.classList.remove('dragover');
    if (e.dataTransfer.files.length > 0) {
      uploadFile(e.dataTransfer.files[0]);
    }
  });

  dropZone.addEventListener('click', () => {
    fileInput.click();
  });
}

function updateModeLabels() {
  const isUnits = currentMetricMode === 'units';
  document.getElementById('kpiBacTitle').textContent = isUnits ? 'Total BAC (Units)' : 'Total BAC ($)';
  document.getElementById('kpiPvTitle').textContent = isUnits ? 'Planned Units (PV)' : 'Planned Cost (PV)';
  document.getElementById('kpiEvTitle').textContent = isUnits ? 'Earned Units (EV)' : 'Earned Cost (EV)';
  document.getElementById('kpiSvTitle').textContent = isUnits ? 'Schedule Variance (Units)' : 'Schedule Variance ($)';

  document.getElementById('labelPlannedLegend').textContent = isUnits ? 'Labor unit - Planned S curve' : 'Cost ($) - Planned S curve';
  document.getElementById('labelEarnedLegend').textContent = isUnits ? 'Labor unit - Earned S curve' : 'Cost ($) - Earned S curve';

  document.getElementById('sCurveTitle').textContent = isUnits
    ? 'Monthly Planned vs. Earned Values & S-Curve (Labor Units)'
    : 'Monthly Planned vs. Earned Values & S-Curve (Cost $)';

  document.getElementById('lhUnitsCostLabel').textContent = isUnits
    ? 'Planned Units in Window'
    : 'Planned Cost in Window';

  document.getElementById('lhHeaderTotal').textContent = isUnits ? 'BAC (Units)' : 'BAC ($)';
  document.getElementById('lhHeaderRemaining').textContent = isUnits ? 'Rem. Units' : 'Rem. Cost';
  document.getElementById('lhHeaderWindow').textContent = isUnits ? 'In Window (Units)' : 'In Window ($)';
}

async function loadDefaultData() {
  try {
    const res = await fetch('/api/schedule/default-data');
    if (res.ok) {
      scheduleData = await res.json();
      renderDashboard();
    }
  } catch (err) {
    console.error('Failed to load default schedule data:', err);
  }
}

async function handleFileUpload(e) {
  const files = e.target.files;
  if (files.length > 0) {
    uploadFile(files[0]);
  }
}

async function uploadFile(file) {
  const statusDiv = document.getElementById('uploadStatus');
  statusDiv.style.display = 'block';
  statusDiv.className = 'upload-status';
  statusDiv.textContent = `Reading and parsing ${file.name}...`;

  const formData = new FormData();
  formData.append('file', file);

  try {
    const response = await fetch('/api/schedule/upload', {
      method: 'POST',
      body: formData
    });

    if (response.ok) {
      scheduleData = await response.json();
      statusDiv.className = 'upload-status success';
      statusDiv.textContent = `Successfully updated schedule from "${file.name}"!`;
      renderDashboard();
    } else {
      const err = await response.json();
      statusDiv.className = 'upload-status error';
      statusDiv.textContent = `Upload failed: ${err.error || 'Server error'}`;
    }
  } catch (err) {
    statusDiv.className = 'upload-status error';
    statusDiv.textContent = `Upload failed: ${err.message}`;
  }
}

async function fetchLookaheadData(days) {
  try {
    const res = await fetch(`/api/schedule/lookahead?days=${days}`);
    if (res.ok) {
      const items = await res.json();
      if (scheduleData) {
        scheduleData.lookaheadActivities = items;
      }
      renderLookaheadSection();
    }
  } catch (e) {
    console.error('Failed fetching lookahead data', e);
  }
}

function renderDashboard() {
  if (!scheduleData) return;

  renderProjectHeader();
  renderKpis();
  renderSCurveChart();
  renderPeriodSummarySection();
  renderLookaheadSection();
  renderMatrixTable();
  setTimeout(() => drawMatrixSCurveOverlay(), 150);
}

function renderProjectHeader() {
  document.getElementById('projectTitle').textContent = scheduleData.projectTitle || 'Schedule & EVM Analysis';

  const fmt = (d) => d ? new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : 'N/A';

  document.getElementById('projDataDateSpan').textContent = fmt(scheduleData.dataDate);
  document.getElementById('projPlannedSpan').textContent = `${fmt(scheduleData.projectPlannedStart)} - ${fmt(scheduleData.projectPlannedFinish)}`;
  document.getElementById('projForecastSpan').textContent = `${fmt(scheduleData.projectForecastStart)} - ${fmt(scheduleData.projectForecastFinish)}`;

  const varChip = document.getElementById('varianceChip');
  const varSpan = document.getElementById('projVarianceSpan');
  const vDays = scheduleData.projectVarianceDays || 0;

  if (vDays < 0) {
    varChip.className = 'chip chip-delayed';
    varSpan.textContent = `${Math.abs(vDays)} days delayed`;
  } else if (vDays > 0) {
    varChip.className = 'chip chip-ontrack';
    varSpan.textContent = `${vDays} days ahead`;
  } else {
    varChip.className = 'chip';
    varSpan.textContent = '0 days (on baseline)';
  }

  document.getElementById('cntTotal').textContent = scheduleData.totalActivities;
  document.getElementById('cntCompleted').textContent = scheduleData.completedCount;
  document.getElementById('cntInProgress').textContent = scheduleData.inProgressCount;
  document.getElementById('cntDelayed').textContent = scheduleData.delayedCount;
  document.getElementById('cntNotStarted').textContent = scheduleData.notStartedCount;
}

function renderKpis() {
  const isUnits = currentMetricMode === 'units';

  const bac = isUnits ? scheduleData.totalUnitsBudgetAtCompletion : scheduleData.totalCostBudgetAtCompletion;
  const pv = isUnits ? scheduleData.totalUnitsPlannedValue : scheduleData.totalCostPlannedValue;
  const ev = isUnits ? scheduleData.totalUnitsEarnedValue : scheduleData.totalCostEarnedValue;
  const sv = isUnits ? scheduleData.unitsScheduleVariance : scheduleData.costScheduleVariance;
  const spi = isUnits ? scheduleData.unitsSchedulePerformanceIndex : scheduleData.costSchedulePerformanceIndex;
  const progress = isUnits ? scheduleData.overallUnitsProgressPercent : scheduleData.overallCostProgressPercent;

  const numFmt = (val) => isUnits ? val.toLocaleString(undefined, { maximumFractionDigits: 1 }) : '$' + val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

  document.getElementById('kpiBac').textContent = numFmt(bac);
  document.getElementById('kpiPv').textContent = numFmt(pv);
  document.getElementById('kpiEv').textContent = numFmt(ev);

  const svEl = document.getElementById('kpiSv');
  const svCard = document.getElementById('kpiCardSv');
  svEl.textContent = (sv >= 0 ? '+' : '') + numFmt(sv);
  svCard.className = sv >= 0 ? 'kpi-card kpi-positive' : 'kpi-card kpi-negative';

  const spiEl = document.getElementById('kpiSpi');
  const spiCard = document.getElementById('kpiCardSpi');
  spiEl.textContent = spi.toFixed(2);
  spiCard.className = spi >= 1.0 ? 'kpi-card kpi-positive' : 'kpi-card kpi-negative';

  document.getElementById('kpiProgress').textContent = `${progress.toFixed(1)}%`;
}

function renderSCurveChart() {
  const ctx = document.getElementById('sCurveChart').getContext('2d');
  const isUnits = currentMetricMode === 'units';

  // Use MonthlySeries or fallback to WeeklySeries
  const series = (scheduleData.monthlySeries && scheduleData.monthlySeries.length > 0)
    ? scheduleData.monthlySeries
    : (scheduleData.weeklySeries || []);

  const labels = series.map(s => s.periodLabel || s.weekLabel || `P${s.periodIndex || s.weekNumber}`);

  const plannedBars = series.map(s => isUnits ? (s.periodicPlannedUnits ?? s.weeklyPlannedValue) : (s.periodicPlannedCost ?? s.weeklyPlannedValue));
  const earnedBars = series.map(s => isUnits ? (s.periodicEarnedUnits ?? s.weeklyEarnedValue) : (s.periodicEarnedCost ?? s.weeklyEarnedValue));

  const plannedCurve = series.map(s => isUnits ? (s.cumulativePlannedUnits ?? s.cumulativePlannedValue) : (s.cumulativePlannedCost ?? s.cumulativePlannedValue));
  const earnedCurve = series.map(s => {
    const val = isUnits ? (s.cumulativeEarnedUnits ?? s.cumulativeEarnedValue) : (s.cumulativeEarnedCost ?? s.cumulativeEarnedValue);
    return val > 0 ? val : null;
  });

  if (sCurveChart) {
    sCurveChart.destroy();
  }

  const plannedLegendLabel = isUnits ? 'Labor unit - Planned S curve' : 'Cost ($) - Planned S curve';
  const earnedLegendLabel = isUnits ? 'Labor unit - Earned S curve' : 'Cost ($) - Earned S curve';

  sCurveChart = new Chart(ctx, {
    data: {
      labels: labels,
      datasets: [
        {
          type: 'line',
          label: plannedLegendLabel,
          data: plannedCurve,
          borderColor: '#0284c7',
          backgroundColor: '#0284c7',
          borderWidth: 3,
          tension: 0.35,
          pointRadius: 3,
          pointHoverRadius: 6,
          yAxisID: 'yCumulative'
        },
        {
          type: 'line',
          label: earnedLegendLabel,
          data: earnedCurve,
          borderColor: '#dc2626',
          backgroundColor: '#dc2626',
          borderWidth: 3,
          tension: 0.35,
          pointRadius: 4,
          pointHoverRadius: 7,
          yAxisID: 'yCumulative'
        },
        {
          type: 'bar',
          label: isUnits ? 'Monthly Planned Units' : 'Monthly Planned Cost',
          data: plannedBars,
          backgroundColor: 'rgba(56, 189, 248, 0.45)',
          borderColor: '#0284c7',
          borderWidth: 1,
          borderRadius: 4,
          yAxisID: 'yIncremental'
        },
        {
          type: 'bar',
          label: isUnits ? 'Monthly Earned Units' : 'Monthly Earned Cost',
          data: earnedBars,
          backgroundColor: 'rgba(239, 68, 68, 0.45)',
          borderColor: '#dc2626',
          borderWidth: 1,
          borderRadius: 4,
          yAxisID: 'yIncremental'
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: {
        mode: 'index',
        intersect: false
      },
      plugins: {
        legend: {
          display: true,
          position: 'top',
          labels: {
            boxWidth: 14,
            usePointStyle: true,
            font: { size: 12, weight: '600' }
          }
        },
        tooltip: {
          callbacks: {
            label: function(context) {
              const val = context.raw;
              if (val === null || val === undefined) return null;
              const formatted = isUnits
                ? val.toLocaleString(undefined, { maximumFractionDigits: 1 }) + ' units'
                : '$' + val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
              return `${context.dataset.label}: ${formatted}`;
            }
          }
        }
      },
      scales: {
        x: {
          grid: { color: 'rgba(226, 232, 240, 0.6)' },
          ticks: { font: { size: 11, weight: '500' } }
        },
        yCumulative: {
          type: 'linear',
          position: 'right',
          title: {
            display: true,
            text: isUnits ? 'Cumulative Units' : 'Cumulative Cost ($)',
            font: { weight: '600', size: 12 }
          },
          grid: { drawOnChartArea: true, color: 'rgba(226, 232, 240, 0.6)' },
          ticks: {
            callback: (val) => isUnits ? val.toLocaleString() : '$' + (val >= 1000 ? (val / 1000).toFixed(0) + 'k' : val)
          }
        },
        yIncremental: {
          type: 'linear',
          position: 'left',
          title: {
            display: true,
            text: isUnits ? 'Monthly Units' : 'Monthly Cost ($)',
            font: { weight: '600', size: 12 }
          },
          grid: { drawOnChartArea: false },
          ticks: {
            callback: (val) => isUnits ? val.toLocaleString() : '$' + (val >= 1000 ? (val / 1000).toFixed(0) + 'k' : val)
          }
        }
      }
    }
  });
}

function renderPeriodSummarySection() {
  if (!scheduleData || !scheduleData.periodSummary) return;

  const ps = scheduleData.periodSummary;
  const isUnits = currentMetricMode === 'units';

  const lastPv = isUnits ? ps.lastPeriodPlannedUnits : ps.lastPeriodPlannedCost;
  const lastEv = isUnits ? ps.lastPeriodEarnedUnits : ps.lastPeriodEarnedCost;
  const lastSv = isUnits ? ps.lastPeriodUnitsVariance : ps.lastPeriodCostVariance;
  const lastSpi = isUnits ? ps.lastPeriodUnitsSpi : ps.lastPeriodCostSpi;
  const comingScope = isUnits ? ps.currentPeriodPlannedUnits : ps.currentPeriodPlannedCost;

  const fmtVal = (v) => isUnits
    ? (v >= 1000 ? Math.round(v).toLocaleString() : v.toFixed(1))
    : '$' + v.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 0 });

  document.getElementById('lblPeriodEv').textContent = fmtVal(lastEv);
  document.getElementById('lblPeriodPv').textContent = fmtVal(lastPv);
  document.getElementById('lblPeriodSub').textContent = `Last Period (${ps.lastPeriodLabel || 'Previous Month'}) EV vs PV`;

  const spiElem = document.getElementById('lblPeriodSpi');
  spiElem.textContent = lastSpi.toFixed(2);
  spiElem.style.color = lastSpi >= 1.0 ? 'var(--success)' : (lastSpi >= 0.85 ? '#d97706' : 'var(--danger)');

  document.getElementById('lblPeriodSv').textContent = `SV: ${fmtVal(lastSv)} (${lastSv >= 0 ? '+' : ''}${isUnits ? 'units' : '$'})`;

  document.getElementById('lblComingScope').textContent = fmtVal(comingScope);
  document.getElementById('lblComingLabel').textContent = `Scheduled in ${ps.currentPeriodLabel || 'Current Month'}`;

  document.getElementById('lblDelayedCount').textContent = ps.delayedTasksCount;
  document.getElementById('lblCriticalCount').textContent = ps.criticalTasksCount;

  // Update Tab Badges
  const allTasks = ps.tasks || [];
  document.getElementById('tabCountAll').textContent = allTasks.length;
  document.getElementById('tabCountDelayed').textContent = allTasks.filter(t => t.isDelayed).length;
  document.getElementById('tabCountCritical').textContent = allTasks.filter(t => t.isCritical).length;
  document.getElementById('tabCountLast').textContent = allTasks.filter(t => t.periodType === 'LastPeriod').length;
  document.getElementById('tabCountCurrent').textContent = allTasks.filter(t => t.periodType === 'CurrentPeriod').length;

  document.getElementById('thPeriodPlanned').textContent = isUnits ? 'Period Planned (Units)' : 'Period Planned ($)';
  document.getElementById('thPeriodEarned').textContent = isUnits ? 'Period Earned (Units)' : 'Period Earned ($)';

  renderPeriodSummaryTable();
}

function renderPeriodSummaryTable() {
  if (!scheduleData || !scheduleData.periodSummary) return;

  const tbody = document.getElementById('periodSummaryTableBody');
  tbody.innerHTML = '';

  const allTasks = scheduleData.periodSummary.tasks || [];
  const isUnits = currentMetricMode === 'units';

  let filtered = allTasks;
  if (currentPeriodFilter === 'DELAYED') {
    filtered = allTasks.filter(t => t.isDelayed);
  } else if (currentPeriodFilter === 'CRITICAL') {
    filtered = allTasks.filter(t => t.isCritical);
  } else if (currentPeriodFilter === 'LAST_PERIOD') {
    filtered = allTasks.filter(t => t.periodType === 'LastPeriod');
  } else if (currentPeriodFilter === 'CURRENT_PERIOD') {
    filtered = allTasks.filter(t => t.periodType === 'CurrentPeriod');
  }

  if (filtered.length === 0) {
    tbody.innerHTML = `<tr><td colspan="11" style="text-align:center; padding: 2rem; color: #94a3b8;">No tasks matching filter "${currentPeriodFilter}".</td></tr>`;
    return;
  }

  const fmtDate = (d) => d ? new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '-';

  filtered.forEach(task => {
    const tr = document.createElement('tr');
    if (task.isDelayed) tr.style.backgroundColor = '#fff5f5';

    const pBadge = task.periodType === 'LastPeriod'
      ? `<span class="period-badge-last">Last Period (${task.periodLabel})</span>`
      : `<span class="period-badge-current">Current Period (${task.periodLabel})</span>`;

    const critBadge = task.isCritical
      ? `<span class="badge badge-critical">Critical Path</span>`
      : `<span class="badge badge-standard">Standard</span>`;

    const vClass = (task.varianceDays || 0) < 0 ? 'var-pill delayed' : ((task.varianceDays || 0) > 0 ? 'var-pill ahead' : 'var-pill ontrack');
    const vText = task.varianceDays !== null && task.varianceDays !== undefined
      ? `${task.varianceDays}d`
      : '-';

    const pUnits = isUnits ? task.plannedUnits : task.plannedCost;
    const eUnits = isUnits ? task.earnedUnits : task.earnedCost;

    const fmtNum = (n) => isUnits
      ? (n > 0 ? (n >= 1000 ? Math.round(n).toLocaleString() : n.toFixed(1)) : '-')
      : (n > 0 ? '$' + n.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 0 }) : '-');

    tr.innerHTML = `
      <td>${pBadge}</td>
      <td><strong>${task.activityId}</strong></td>
      <td class="cell-activity-name">${task.activityName}</td>
      <td style="text-align: center;">${critBadge}</td>
      <td style="font-size: 0.76rem;">${fmtDate(task.forecastStartDate)} &rarr; ${fmtDate(task.forecastFinishDate)}</td>
      <td style="text-align: center;"><span class="${vClass}">${vText}</span></td>
      <td style="text-align: right; font-weight: 600;">${fmtNum(pUnits)}</td>
      <td style="text-align: right; font-weight: 600; color: #dc2626;">${fmtNum(eUnits)}</td>
      <td>
        <div class="progress-bar-container">
          <div class="progress-track">
            <div class="progress-fill ${task.cumulativePercent >= 100 ? 'fill-complete' : (task.isDelayed ? 'fill-delayed' : '')}" style="width: ${Math.min(100, task.cumulativePercent)}%"></div>
          </div>
          <span style="font-size: 0.75rem; font-weight: 600; min-width: 38px;">${task.cumulativePercent.toFixed(1)}%</span>
        </div>
      </td>
      <td>${getStatusBadge(task.status)}</td>
      <td style="font-size: 0.76rem; color: #475569;">${task.actionRecommendation || '-'}</td>
    `;
    tbody.appendChild(tr);
  });
}

function renderLookaheadSection() {
  const items = scheduleData.lookaheadActivities || [];
  const isUnits = currentMetricMode === 'units';

  document.getElementById('lhCount').textContent = items.length;
  document.getElementById('lhStartingCount').textContent = items.filter(x => x.lookaheadPhase.includes('Starting')).length;
  document.getElementById('lhFinishingCount').textContent = items.filter(x => x.lookaheadPhase.includes('Finishing')).length;
  document.getElementById('lhDelayedCount').textContent = items.filter(x => x.isDelayed).length;

  const totalScopeInWindow = items.reduce((sum, item) => sum + (isUnits ? item.lookaheadPlannedUnits : item.lookaheadPlannedCost), 0);
  document.getElementById('lhScopeValue').textContent = isUnits
    ? totalScopeInWindow.toLocaleString(undefined, { maximumFractionDigits: 1 }) + ' units'
    : '$' + totalScopeInWindow.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

  const tbody = document.getElementById('lookaheadTableBody');
  tbody.innerHTML = '';

  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="12" style="text-align:center; padding: 2rem; color: #94a3b8;">No activities found within the ${selectedLookaheadDays}-day lookahead window.</td></tr>`;
    return;
  }

  const fmt = (d) => d ? new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '-';

  items.forEach(item => {
    const tr = document.createElement('tr');

    const phaseClass = item.lookaheadPhase.includes('Starting') ? 'phase-starting' :
                       item.lookaheadPhase.includes('Finishing') ? 'phase-finishing' : 'phase-entire';

    const vClass = (item.varianceDays < 0) ? 'var-pill delayed' : (item.varianceDays > 0 ? 'var-pill ahead' : 'var-pill ontrack');
    const vText = item.varianceDays !== null && item.varianceDays !== undefined ? `${item.varianceDays}d` : '-';

    const totalBac = isUnits ? item.totalPlannedUnits : item.totalPlannedCost;
    const rem = isUnits ? item.remainingUnits : item.remainingCost;
    const inWin = isUnits ? item.lookaheadPlannedUnits : item.lookaheadPlannedCost;

    const valFmt = (v) => isUnits ? v.toLocaleString(undefined, { maximumFractionDigits: 1 }) : '$' + v.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 0 });

    const statusBadge = getStatusBadge(item.status);

    tr.innerHTML = `
      <td><strong>${item.activityId}</strong></td>
      <td class="cell-activity-name">${item.activityName}</td>
      <td><span class="phase-tag ${phaseClass}">${item.lookaheadPhase}</span></td>
      <td>${fmt(item.forecastStartDate)}</td>
      <td>${fmt(item.forecastFinishDate)}</td>
      <td><strong>${item.daysUntilForecastStart >= 0 ? item.daysUntilForecastStart + 'd' : 'Started'}</strong></td>
      <td><span class="${vClass}">${vText}</span></td>
      <td>${valFmt(totalBac)}</td>
      <td>${valFmt(rem)}</td>
      <td><strong>${valFmt(inWin)}</strong></td>
      <td>${item.percentComplete.toFixed(1)}%</td>
      <td>${statusBadge}</td>
    `;
    tbody.appendChild(tr);
  });
}

function exportLookaheadToCsv() {
  const items = scheduleData.lookaheadActivities || [];
  if (items.length === 0) {
    alert('No lookahead activities to export.');
    return;
  }

  const isUnits = currentMetricMode === 'units';
  const headers = ['Activity ID', 'Activity Name', 'Phase', 'Forecast Start', 'Forecast Finish', 'Variance (days)', 'Total Scope', 'Remaining Scope', 'Scope in Window', 'Percent Complete', 'Status'];
  const rows = items.map(x => [
    `"${x.activityId}"`,
    `"${x.activityName.replace(/"/g, '""')}"`,
    `"${x.lookaheadPhase}"`,
    `"${x.forecastStartDate ? x.forecastStartDate.substring(0, 10) : ''}"`,
    `"${x.forecastFinishDate ? x.forecastFinishDate.substring(0, 10) : ''}"`,
    x.varianceDays || 0,
    isUnits ? x.totalPlannedUnits : x.totalPlannedCost,
    isUnits ? x.remainingUnits : x.remainingCost,
    isUnits ? x.lookaheadPlannedUnits : x.lookaheadPlannedCost,
    x.percentComplete,
    `"${getStatusText(x.status)}"`
  ]);

  const csvContent = 'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
  const encodedUri = encodeURI(csvContent);
  const link = document.createElement('a');
  link.setAttribute('href', encodedUri);
  link.setAttribute('download', `Lookahead_${selectedLookaheadDays}d_${new Date().toISOString().substring(0, 10)}.csv`);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
}

function renderMatrixTable() {
  const thead = document.getElementById('matrixTableHead');
  const tbody = document.getElementById('matrixTableBody');

  const activities = scheduleData.activities || [];
  const periods = scheduleData.timelinePeriodHeaders || [];
  const isUnits = currentMetricMode === 'units';

  const filterText = document.getElementById('searchInput').value.toLowerCase().trim();
  const filterStatus = document.getElementById('statusFilter').value;

  // Build Table Header matching the user image
  let headerHtml = '<tr>';
  headerHtml += '<th class="th-fixed" style="width: 280px;">Activity ID / Name</th>';
  headerHtml += '<th class="th-fixed" style="width: 80px; text-align: center;">Var (days)</th>';
  headerHtml += '<th class="th-fixed" style="width: 90px; text-align: center;">Schedule %</th>';
  headerHtml += '<th class="th-fixed" style="width: 90px; text-align: center;">Perform. %</th>';

  periods.forEach(p => {
    headerHtml += `<th class="th-timeline">${p}</th>`;
  });
  headerHtml += '</tr>';
  thead.innerHTML = headerHtml;

  // Filter activities
  const filtered = activities.filter(a => {
    const matchesSearch = !filterText || a.activityId.toLowerCase().includes(filterText) || a.activityName.toLowerCase().includes(filterText);
    const matchesStatus = filterStatus === 'ALL' || getStatusText(a.status) === filterStatus;
    return matchesSearch && matchesStatus;
  });

  tbody.innerHTML = '';
  if (filtered.length === 0) {
    tbody.innerHTML = `<tr><td colspan="${4 + periods.length}" style="text-align:center; padding: 2rem; color: #94a3b8;">No matching activities found.</td></tr>`;
    return;
  }

  filtered.forEach(act => {
    const tr = document.createElement('tr');
    if (act.isSummary) tr.className = 'row-summary';

    const indentClass = act.level > 0 ? `cell-indent-${Math.min(4, act.level)}` : '';

    const vVal = act.varianceDays !== null && act.varianceDays !== undefined ? act.varianceDays : (act.finishVarianceDays || 0);
    const vClass = vVal < 0 ? 'var-pill delayed' : (vVal > 0 ? 'var-pill ahead' : 'var-pill ontrack');

    const schedPct = act.schedulePercent !== null && act.schedulePercent !== undefined ? `${act.schedulePercent.toFixed(2)}%` : '-';
    const perfPct = act.performancePercent !== null && act.performancePercent !== undefined ? `${act.performancePercent.toFixed(2)}%` : `${act.percentComplete.toFixed(2)}%`;

    let rowHtml = `
      <td class="${indentClass}">
        <strong>${act.activityId}</strong> ${act.activityName && act.activityName !== act.activityId ? '- ' + act.activityName : ''}
      </td>
      <td style="text-align: center;"><span class="${vClass}">${vVal}</span></td>
      <td style="text-align: center;"><strong>${schedPct}</strong></td>
      <td style="text-align: center;"><strong>${perfPct}</strong></td>
    `;

    // Map period values for this row
    const periodMap = new Map();
    (act.periodValues || []).forEach(p => {
      periodMap.set(p.periodLabel, p);
    });

    // Determine continuous span indices for planned and earned values
    let firstPlannedIdx = -1;
    let lastPlannedIdx = -1;
    let firstEarnedIdx = -1;
    let lastEarnedIdx = -1;

    periods.forEach((pHeader, idx) => {
      const pItem = periodMap.get(pHeader);
      if (pItem) {
        const pVal = isUnits ? pItem.plannedUnits : pItem.plannedCost;
        const eVal = isUnits ? pItem.earnedUnits : pItem.earnedCost;

        if (pVal > 0) {
          if (firstPlannedIdx === -1) firstPlannedIdx = idx;
          lastPlannedIdx = idx;
        }

        if (eVal > 0) {
          if (firstEarnedIdx === -1) firstEarnedIdx = idx;
          lastEarnedIdx = idx;
        }
      }
    });

    periods.forEach((pHeader, idx) => {
      const pItem = periodMap.get(pHeader);
      let plannedVal = 0;
      let earnedVal = 0;

      if (pItem) {
        plannedVal = isUnits ? pItem.plannedUnits : pItem.plannedCost;
        earnedVal = isUnits ? pItem.earnedUnits : pItem.earnedCost;
      }

      const hasPlanned = idx >= firstPlannedIdx && idx <= lastPlannedIdx && firstPlannedIdx !== -1;
      const hasEarned = idx >= firstEarnedIdx && idx <= lastEarnedIdx && firstEarnedIdx !== -1;

      if (hasPlanned || hasEarned) {
        let earnedHtml = '<div class="matrix-empty-track"></div>';
        if (hasEarned) {
          const isStart = idx === firstEarnedIdx;
          const isEnd = idx === lastEarnedIdx;
          let edgeClass = '';
          if (isStart && isEnd) edgeClass = 'is-single';
          else if (isStart) edgeClass = 'is-start';
          else if (isEnd) edgeClass = 'is-end';

          const eText = earnedVal > 0 ? (earnedVal >= 1000 ? Math.round(earnedVal).toLocaleString() : earnedVal.toFixed(earnedVal % 1 === 0 ? 0 : 1)) : '';
          earnedHtml = `<div class="matrix-bar-track matrix-bar-earned ${edgeClass}" title="Earned: ${earnedVal.toLocaleString()}"><span class="bar-val-text">${eText}</span></div>`;
        }

        let plannedHtml = '<div class="matrix-empty-track"></div>';
        if (hasPlanned) {
          const isStart = idx === firstPlannedIdx;
          const isEnd = idx === lastPlannedIdx;
          let edgeClass = '';
          if (isStart && isEnd) edgeClass = 'is-single';
          else if (isStart) edgeClass = 'is-start';
          else if (isEnd) edgeClass = 'is-end';

          const pText = plannedVal > 0 ? (plannedVal >= 1000 ? Math.round(plannedVal).toLocaleString() : plannedVal.toFixed(plannedVal % 1 === 0 ? 0 : 1)) : '';
          plannedHtml = `<div class="matrix-bar-track matrix-bar-planned ${edgeClass}" title="Planned: ${plannedVal.toLocaleString()}"><span class="bar-val-text">${pText}</span></div>`;
        }

        rowHtml += `
          <td class="cell-matrix-val">
            <div class="matrix-bar-track-wrapper">
              ${earnedHtml}
              ${plannedHtml}
            </div>
          </td>`;
      } else {
        rowHtml += `<td class="cell-matrix-val"></td>`;
      }
    });

    tr.innerHTML = rowHtml;
    tbody.appendChild(tr);
  });
}

function getStatusBadge(status) {
  switch (status) {
    case 2: // Completed
      return '<span class="badge badge-completed">Completed</span>';
    case 1: // InProgress
      return '<span class="badge badge-in-progress">In Progress</span>';
    case 3: // Delayed
      return '<span class="badge badge-delayed">Delayed</span>';
    default:
      return '<span class="badge badge-not-started">Not Started</span>';
  }
}

function getStatusText(status) {
  switch (status) {
    case 2: return 'Completed';
    case 1: return 'InProgress';
    case 3: return 'Delayed';
    default: return 'NotStarted';
  }
}

// Matrix S-Curve Overlay Functionality
let showMatrixSCurve = true;

function toggleMatrixSCurve() {
  showMatrixSCurve = !showMatrixSCurve;
  const txt = document.getElementById('txtToggleMatrixSCurve');
  if (txt) {
    txt.textContent = showMatrixSCurve ? 'Hide S-Curve on Matrix' : 'Show S-Curve on Matrix';
  }
  drawMatrixSCurveOverlay();
}

function drawMatrixSCurveOverlay() {
  const canvas = document.getElementById('matrixSCurveCanvas');
  const scrollContainer = document.getElementById('matrixScrollContainer');
  const table = document.getElementById('matrixTable');
  if (!canvas || !scrollContainer || !table) return;

  const ctx = canvas.getContext('2d');
  ctx.clearRect(0, 0, canvas.width, canvas.height);

  if (!showMatrixSCurve || !scheduleData || !scheduleData.monthlySeries || scheduleData.monthlySeries.length === 0) {
    return;
  }

  // Adjust canvas size to match the scrollable table area
  canvas.width = table.scrollWidth;
  canvas.height = table.scrollHeight;
  canvas.style.width = table.scrollWidth + 'px';
  canvas.style.height = table.scrollHeight + 'px';

  // Find column header th positions for timeline
  const thHeaders = Array.from(table.querySelectorAll('thead th.th-timeline'));
  if (thHeaders.length === 0) return;

  const isUnits = currentMetricMode === 'units';
  const series = scheduleData.monthlySeries;
  const bac = isUnits ? (scheduleData.totalUnitsBudgetAtCompletion || 1) : (scheduleData.totalCostBudgetAtCompletion || 1);

  const headerRow = table.querySelector('thead tr');
  const tbodyRows = table.querySelectorAll('tbody tr');
  if (tbodyRows.length === 0) return;

  const headerBottom = headerRow.offsetTop + headerRow.offsetHeight;
  const tableBottom = table.scrollHeight - 30;
  const availableHeight = Math.max(100, tableBottom - headerBottom);

  const plannedCoords = [];
  const earnedCoords = [];

  series.forEach((pt, idx) => {
    if (idx < thHeaders.length) {
      const th = thHeaders[idx];
      const x = th.offsetLeft + (th.offsetWidth / 2);

      const cumPv = isUnits ? pt.cumulativePlannedUnits : pt.cumulativePlannedCost;
      const cumEv = isUnits ? pt.cumulativeEarnedUnits : pt.cumulativeEarnedCost;

      const pvRatio = Math.min(1.0, Math.max(0, cumPv / bac));
      const evRatio = Math.min(1.0, Math.max(0, cumEv / bac));

      // 0% at tableBottom, 100% near headerBottom
      const pvY = tableBottom - (pvRatio * availableHeight);
      plannedCoords.push({ x, y: pvY, label: pt.periodLabel, val: cumPv, pct: (pvRatio * 100).toFixed(1) });

      if (cumEv > 0 || idx <= 1) { // Up to data date
        const evY = tableBottom - (evRatio * availableHeight);
        earnedCoords.push({ x, y: evY, label: pt.periodLabel, val: cumEv, pct: (evRatio * 100).toFixed(1) });
      }
    }
  });

  // Draw Planned S-Curve line (Blue, as in the user's attachment)
  if (plannedCoords.length > 1) {
    ctx.save();
    ctx.beginPath();
    ctx.strokeStyle = '#0284c7';
    ctx.lineWidth = 3.5;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';

    ctx.moveTo(plannedCoords[0].x, plannedCoords[0].y);
    for (let i = 1; i < plannedCoords.length; i++) {
      const xc = (plannedCoords[i - 1].x + plannedCoords[i].x) / 2;
      const yc = (plannedCoords[i - 1].y + plannedCoords[i].y) / 2;
      ctx.quadraticCurveTo(plannedCoords[i - 1].x, plannedCoords[i - 1].y, xc, yc);
    }
    const last = plannedCoords[plannedCoords.length - 1];
    ctx.lineTo(last.x, last.y);
    ctx.stroke();

    // Planned S curve label directly along the curve within visible chart area
    const midIdx = Math.min(plannedCoords.length - 1, Math.floor(plannedCoords.length * 0.45));
    if (plannedCoords[midIdx]) {
      ctx.save();
      ctx.translate(plannedCoords[midIdx].x + 10, plannedCoords[midIdx].y - 12);
      ctx.rotate(-0.35);
      ctx.fillStyle = '#0369a1';
      ctx.font = 'bold 12px "Segoe UI", sans-serif';
      ctx.fillText(isUnits ? 'Labor unit - Planned S curve' : 'Cost ($) - Planned S curve', 0, 0);
      ctx.restore();
    }
    ctx.restore();
  }

  // Draw Earned S-Curve line (Red, matching user's attachment)
  if (earnedCoords.length > 0) {
    ctx.save();
    ctx.beginPath();
    ctx.strokeStyle = '#dc2626';
    ctx.lineWidth = 3.5;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';

    ctx.moveTo(earnedCoords[0].x, earnedCoords[0].y);
    for (let i = 1; i < earnedCoords.length; i++) {
      ctx.lineTo(earnedCoords[i].x, earnedCoords[i].y);
    }
    ctx.stroke();

    // Points on Earned curve
    earnedCoords.forEach(pt => {
      ctx.beginPath();
      ctx.arc(pt.x, pt.y, 4, 0, Math.PI * 2);
      ctx.fillStyle = '#dc2626';
      ctx.fill();
    });

    // Label for Earned S curve
    const lastE = earnedCoords[earnedCoords.length - 1];
    ctx.save();
    ctx.translate(lastE.x - 70, lastE.y + 24);
    ctx.rotate(-0.2);
    ctx.fillStyle = '#dc2626';
    ctx.font = 'bold 12px "Segoe UI", sans-serif';
    ctx.fillText(isUnits ? 'Labor unit - Earned S curve' : 'Cost ($) - Earned S curve', 0, 0);
    ctx.restore();

    ctx.restore();
  }
}
