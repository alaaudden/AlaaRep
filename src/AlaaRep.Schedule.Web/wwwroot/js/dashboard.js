document.addEventListener('DOMContentLoaded', () => {
  let scheduleData = null;
  let sCurveChart = null;
  let weeklyBarChart = null;

  const fileInput = document.getElementById('excelFileInput');
  const dropZone = document.getElementById('dropZone');
  const uploadStatus = document.getElementById('uploadStatus');
  const btnDownloadSample = document.getElementById('btnDownloadSample');
  const searchInput = document.getElementById('searchInput');
  const statusFilter = document.getElementById('statusFilter');

  // Load initial sample data
  loadDefaultData();

  // Event Listeners
  btnDownloadSample.addEventListener('click', () => {
    window.location.href = '/api/schedule/sample';
  });

  fileInput.addEventListener('change', (e) => {
    if (e.target.files.length > 0) {
      uploadFile(e.target.files[0]);
    }
  });

  // Drag and Drop
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

  dropZone.addEventListener('click', (e) => {
    if (e.target.tagName !== 'INPUT' && e.target !== btnDownloadSample) {
      fileInput.click();
    }
  });

  searchInput.addEventListener('input', () => renderTable());
  statusFilter.addEventListener('change', () => renderTable());

  async function loadDefaultData() {
    try {
      const res = await fetch('/api/schedule/default-data');
      if (!res.ok) throw new Error('Failed to load sample project');
      scheduleData = await res.json();
      renderDashboard();
    } catch (err) {
      console.error(err);
      showStatus('Failed to load default schedule: ' + err.message, true);
    }
  }

  async function uploadFile(file) {
    if (!file.name.endsWith('.xlsx') && !file.name.endsWith('.xls')) {
      showStatus('Please upload an Excel file (.xlsx or .xls)', true);
      return;
    }

    showStatus(`Uploading and parsing ${file.name}...`, false);
    const formData = new FormData();
    formData.append('file', file);

    try {
      const res = await fetch('/api/schedule/upload', {
        method: 'POST',
        body: formData
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.error || 'Failed to upload schedule.');
      }

      scheduleData = data;
      showStatus(`Successfully loaded schedule: ${data.projectTitle || file.name} (${data.totalActivities} activities)`, false);
      renderDashboard();
    } catch (err) {
      showStatus(err.message, true);
    }
  }

  function showStatus(msg, isError) {
    uploadStatus.style.display = 'block';
    uploadStatus.textContent = msg;
    uploadStatus.style.color = isError ? '#ef4444' : '#10b981';
  }

  function formatCurrency(val) {
    if (val === null || val === undefined) return '$0';
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(val);
  }

  function formatDate(dStr) {
    if (!dStr) return '-';
    const d = new Date(dStr);
    return isNaN(d) ? '-' : d.toISOString().split('T')[0];
  }

  function getStatusBadge(status) {
    switch (status) {
      case 2:
      case 'Completed':
        return '<span class="badge badge-completed">Completed</span>';
      case 1:
      case 'InProgress':
        return '<span class="badge badge-inprogress">In Progress</span>';
      case 3:
      case 'Delayed':
        return '<span class="badge badge-delayed">Delayed</span>';
      default:
        return '<span class="badge badge-notstarted">Not Started</span>';
    }
  }

  function getStatusName(status) {
    if (status === 2 || status === 'Completed') return 'Completed';
    if (status === 1 || status === 'InProgress') return 'InProgress';
    if (status === 3 || status === 'Delayed') return 'Delayed';
    return 'NotStarted';
  }

  function renderDashboard() {
    if (!scheduleData) return;

    // Project header
    document.getElementById('projectTitle').textContent = scheduleData.projectTitle || 'Schedule & EVM Analytics';
    document.getElementById('projPlannedSpan').textContent = `${formatDate(scheduleData.projectPlannedStart)} to ${formatDate(scheduleData.projectPlannedFinish)}`;
    document.getElementById('projForecastSpan').textContent = `${formatDate(scheduleData.projectForecastStart)} to ${formatDate(scheduleData.projectForecastFinish)}`;

    const vDays = scheduleData.projectVarianceDays;
    const vSpan = document.getElementById('projVarianceSpan');
    if (vDays < 0) {
      vSpan.textContent = `${Math.abs(vDays)} days delayed`;
      vSpan.style.color = '#f87171';
    } else if (vDays > 0) {
      vSpan.textContent = `${vDays} days ahead`;
      vSpan.style.color = '#34d399';
    } else {
      vSpan.textContent = 'On Schedule';
      vSpan.style.color = '#94a3b8';
    }

    // Status pills
    document.getElementById('cntTotal').textContent = scheduleData.totalActivities;
    document.getElementById('cntCompleted').textContent = scheduleData.completedCount;
    document.getElementById('cntInProgress').textContent = scheduleData.inProgressCount;
    document.getElementById('cntDelayed').textContent = scheduleData.delayedCount;
    document.getElementById('cntNotStarted').textContent = scheduleData.notStartedCount;

    // KPI Cards
    document.getElementById('kpiBac').textContent = formatCurrency(scheduleData.totalBudgetAtCompletion);
    document.getElementById('kpiPv').textContent = formatCurrency(scheduleData.totalPlannedValue);
    document.getElementById('kpiEv').textContent = formatCurrency(scheduleData.totalEarnedValue);

    const sv = scheduleData.scheduleVariance;
    const svElem = document.getElementById('kpiSv');
    svElem.textContent = (sv >= 0 ? '+' : '') + formatCurrency(sv);
    svElem.style.color = sv >= 0 ? '#34d399' : '#f87171';
    document.getElementById('kpiSvSub').textContent = `${scheduleData.scheduleVariancePercent}% vs planned to date`;

    const spi = scheduleData.schedulePerformanceIndex;
    const spiElem = document.getElementById('kpiSpi');
    spiElem.textContent = spi.toFixed(2);
    spiElem.style.color = spi >= 1.0 ? '#34d399' : '#f87171';
    document.getElementById('kpiSpiSub').textContent = spi >= 1.0 ? 'Schedule Efficient (Ahead/On Track)' : 'Schedule Inefficient (Behind)';

    document.getElementById('kpiProgress').textContent = `${scheduleData.overallProgressPercent}%`;

    // Render Charts & Table
    renderCharts();
    renderTable();
  }

  function renderCharts() {
    const series = scheduleData.weeklySeries || [];
    const labels = series.map(s => s.weekLabel || `W${s.weekNumber}`);
    const plannedCum = series.map(s => s.cumulativePlannedValue);
    const earnedCum = series.map(s => s.cumulativeEarnedValue);
    const plannedWk = series.map(s => s.weeklyPlannedValue);
    const earnedWk = series.map(s => s.weeklyEarnedValue);

    // Destroy existing charts
    if (sCurveChart) sCurveChart.destroy();
    if (weeklyBarChart) weeklyBarChart.destroy();

    // 1. S-Curve
    const ctxSCurve = document.getElementById('sCurveChart').getContext('2d');
    sCurveChart = new Chart(ctxSCurve, {
      type: 'line',
      data: {
        labels: labels,
        datasets: [
          {
            label: 'Cumulative Planned Value (PV)',
            data: plannedCum,
            borderColor: '#3b82f6',
            backgroundColor: 'rgba(59, 130, 246, 0.1)',
            fill: true,
            tension: 0.35,
            borderWidth: 2.5,
            pointRadius: 4,
            pointBackgroundColor: '#3b82f6'
          },
          {
            label: 'Cumulative Earned Value (EV)',
            data: earnedCum,
            borderColor: '#10b981',
            backgroundColor: 'rgba(16, 185, 129, 0.1)',
            fill: true,
            tension: 0.35,
            borderWidth: 2.5,
            pointRadius: 4,
            pointBackgroundColor: '#10b981'
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { labels: { color: '#cbd5e1' } },
          tooltip: {
            callbacks: {
              label: (c) => `${c.dataset.label}: ${formatCurrency(c.parsed.y)}`
            }
          }
        },
        scales: {
          x: {
            grid: { color: '#334155' },
            ticks: { color: '#94a3b8' }
          },
          y: {
            grid: { color: '#334155' },
            ticks: {
              color: '#94a3b8',
              callback: (v) => formatCurrency(v)
            }
          }
        }
      }
    });

    // 2. Weekly Bar Chart
    const ctxWeekly = document.getElementById('weeklyBarChart').getContext('2d');
    weeklyBarChart = new Chart(ctxWeekly, {
      type: 'bar',
      data: {
        labels: labels,
        datasets: [
          {
            label: 'Weekly Planned Value (PV)',
            data: plannedWk,
            backgroundColor: '#3b82f6',
            borderRadius: 4
          },
          {
            label: 'Weekly Earned Value (EV)',
            data: earnedWk,
            backgroundColor: '#10b981',
            borderRadius: 4
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { labels: { color: '#cbd5e1' } },
          tooltip: {
            callbacks: {
              label: (c) => `${c.dataset.label}: ${formatCurrency(c.parsed.y)}`
            }
          }
        },
        scales: {
          x: {
            grid: { color: '#334155' },
            ticks: { color: '#94a3b8' }
          },
          y: {
            grid: { color: '#334155' },
            ticks: {
              color: '#94a3b8',
              callback: (v) => formatCurrency(v)
            }
          }
        }
      }
    });
  }

  function renderTable() {
    const tbody = document.getElementById('activityTableBody');
    tbody.innerHTML = '';

    if (!scheduleData || !scheduleData.activities) return;

    const query = searchInput.value.toLowerCase().trim();
    const filter = statusFilter.value;

    const filtered = scheduleData.activities.filter(a => {
      const matchQuery = !query ||
        (a.activityId && a.activityId.toLowerCase().includes(query)) ||
        (a.activityName && a.activityName.toLowerCase().includes(query));

      const statusName = getStatusName(a.status);
      const matchStatus = filter === 'ALL' || statusName === filter;

      return matchQuery && matchStatus;
    });

    if (filtered.length === 0) {
      tbody.innerHTML = '<tr><td colspan="11" style="text-align: center; color: #94a3b8; padding: 2rem;">No matching activities found.</td></tr>';
      return;
    }

    filtered.forEach(a => {
      const tr = document.createElement('tr');
      const vDays = a.finishVarianceDays;
      let slippageText = '-';
      let slippageClass = 'slippage-neutral';

      if (vDays !== null && vDays !== undefined) {
        if (vDays < 0) {
          slippageText = `${Math.abs(vDays)}d slip`;
          slippageClass = 'slippage-negative';
        } else if (vDays > 0) {
          slippageText = `+${vDays}d ahead`;
          slippageClass = 'slippage-positive';
        } else {
          slippageText = '0d on track';
        }
      }

      tr.innerHTML = `
        <td><strong>${a.activityId}</strong></td>
        <td>${a.activityName}</td>
        <td>${formatDate(a.plannedStartDate)}</td>
        <td>${formatDate(a.plannedFinishDate)}</td>
        <td>${formatDate(a.forecastStartDate)}</td>
        <td>${formatDate(a.forecastFinishDate)}</td>
        <td class="${slippageClass}">${slippageText}</td>
        <td>${formatCurrency(a.budgetAtCompletion)}</td>
        <td>${formatCurrency(a.earnedValueToDate)}</td>
        <td>
          <div>${a.percentComplete}%</div>
          <div class="progress-bar-container">
            <div class="progress-bar-fill" style="width: ${Math.min(100, Math.max(0, a.percentComplete))}%;"></div>
          </div>
        </td>
        <td>${getStatusBadge(a.status)}</td>
      `;
      tbody.appendChild(tr);
    });
  }
});
