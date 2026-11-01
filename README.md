# Construction Project Progress Dashboard (Primavera P6 XER)

A construction project progress reporting and dashboard application tailored for planning engineers. It parses Oracle Primavera P6 `.xer` files (Baseline and Weekly Updates) and generates an executive dashboard report with cumulative S-curves, WBS progress rollups, milestone variance tracking, and critical path analysis.

---

## Key Features

1. **Native Primavera P6 XER Parsing**:
   - Parses `%T` (tables), `%F` (fields), and `%R` (records) with multi-encoding fallback (`utf-8`, `latin-1`, `windows-1252`).
   - Extracts `PROJECT`, `PROJWBS`, `TASK`, and `TASKPRED` tables.
2. **Progress Analysis & Earned Value KPIs**:
   - **Data Date** cut-off handling.
   - **Planned % vs Actual Progress %** calculation weighted by baseline activity durations.
   - **Schedule Variance (SV)** & **Schedule Performance Index (SPI)**.
   - **Milestone Variance**: Baseline finish vs forecast/actual completion date with day slippage.
   - **Critical Path & Float Analysis**: Flags critical activities ($\text{TF} \le 0$) and near-critical items.
   - **Delay Status Classification**: On Track, Delayed, or Ahead.
   - **WBS Hierarchy Rollup**: Weighted progress aggregation by WBS work packages.
   - **Cumulative S-Curve Generation**: Weekly timeline of Planned vs Actual to Date and Forecast progression.
3. **Interactive & Executive Dashboard**:
   - Modern Tailwind CSS & Chart.js responsive design.
   - KPI metric scorecards.
   - Interactive S-Curve line chart.
   - WBS progress comparison bars.
   - Searchable activity watchlist for critical and delayed tasks.
   - One-click PDF / Print layout.
4. **Flexible Interfaces**:
   - **CLI**: Generate standalone HTML reports straight from command-line.
   - **Web UI**: Interactive FastAPI browser interface with file upload and live preview.

---

## Installation

```bash
pip install -r requirements.txt
```

---

## Quick Start

### 1. Generate Dashboard via CLI

Run the program with your Baseline and Weekly Update XER files:

```bash
python main.py --baseline path/to/baseline.xer --update path/to/update_week.xer --output dist/progress_dashboard.html
```

If no arguments are provided, it automatically runs with the realistic sample project provided:

```bash
python main.py
```

This generates `dist/progress_dashboard.html`, which can be opened in any web browser or emailed as a standalone report.

### 2. Launch Interactive Web App

To start the local web application:

```bash
python main.py --serve --port 8000
```

Open `http://127.0.0.1:8000` in your web browser:
- Upload your Baseline and Weekly Update `.xer` files.
- Click **Generate Progress Dashboard** to view your report in real-time.
- Or click **Load Sample Commercial Tower (Week 12)** for immediate demonstration.

---

## Project Structure

```
.
├── main.py                     # Unified CLI and application runner
├── requirements.txt            # Python dependencies
├── src/
│   ├── xer_parser.py           # Primavera P6 XER format parser
│   ├── progress_analyzer.py    # Progress & variance comparison engine
│   ├── dashboard.py            # HTML report template & dashboard generator
│   └── app.py                  # FastAPI web server and REST endpoints
├── samples/
│   ├── baseline_project.xer    # Sample commercial tower baseline schedule
│   ├── update_week12.xer       # Sample weekly update (Week 12 progress & delays)
│   └── generate_samples.py     # Script to generate realistic XER files
├── tests/
│   ├── test_xer_parser.py      # Tests for parser and entity extraction
│   ├── test_analyzer.py        # Tests for comparison engine & CLI
│   └── test_api.py             # Tests for web endpoints
└── dist/
    └── progress_dashboard.html # Generated dashboard report output
```

---

## Running Tests

Run the comprehensive unit test suite:

```bash
python3 -m unittest discover -s tests -p "test_*.py" -v
```
