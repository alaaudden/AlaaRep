# AlaaRep Schedule & Earned Value Dashboard

A C# .NET solution for ingesting project time schedule updates from Excel sheets and generating interactive analytics dashboards with Earned Value Management (EVM) metrics, S-curves, weekly increments, and schedule variance tracking.

## Features

- **Excel Time Schedule Ingestion**: Parses project schedule Excel sheets with flexible column detection:
  - Activity ID & Activity Name / Description
  - Planned Start & Planned Finish Dates
  - Forecast Start & Forecast Finish Dates
  - Weekly Planned Values (PV) & Earned Values (EV) across any number of weeks (e.g. `W1 PV`, `W1 EV` or date-based week headers)
  - Budget at Completion (BAC)
- **EVM Analytics & Calculation Engine**:
  - Total BAC, Cumulative Planned Value (PV), Cumulative Earned Value (EV)
  - Schedule Variance (SV = EV - PV) in dollar and percentage terms
  - Schedule Performance Index (SPI = EV / PV)
  - Activity Finish Variance & Slippage in days
  - Activity status tracking (`Completed`, `InProgress`, `Delayed`, `NotStarted`)
- **Interactive Web Dashboard**:
  - Executive KPI summary cards (BAC, PV, EV, SV, SPI, Overall Progress %)
  - Cumulative S-Curve Chart (Planned vs Earned cumulative progression over time)
  - Weekly Incremental Performance Bar Chart (Planned vs Earned weekly values)
  - Dynamic Activity Schedule Table with slippage days and progress indicators
  - Search by activity ID/name and filtering by status
  - Excel file drag-and-drop upload and instant sample template download
- **Automated Tests**: Comprehensive unit tests for Excel parsing and EVM calculations.

## Structure

```
├── AlaaRep.Schedule.slnx
├── src/
│   ├── AlaaRep.Schedule.Core/    # Models, EVM calculation engine, Excel parser, sample generator
│   └── AlaaRep.Schedule.Web/     # ASP.NET Core web server and dashboard UI (HTML/CSS/JS)
└── tests/
    └── AlaaRep.Schedule.Tests/   # Unit tests with xUnit
```

## Running the Application

To run the application locally:

```bash
dotnet run --project src/AlaaRep.Schedule.Web/AlaaRep.Schedule.Web.csproj
```

Then navigate to `http://localhost:5000` in your web browser.

## Running Tests

To run the test suite:

```bash
dotnet test
```
