# AlaaRep Spreadsheet

A high-performance Excel-like spreadsheet application built with **C#** and **WPF (.NET 10 / .NET 8)**.

Designed to handle massive volumes of data (1,000,000+ rows and 16,384+ columns) smoothly with zero formulas applied, pure virtualized rendering, interactive column and row resizing, rich formatting, Excel/CSV import and export, and full Excel-compatible editing and clipboard behaviors.

---

## Features

- **High-Performance Sparse Matrix Engine**:
  - Uses sparse dictionary row storage so empty rows and cells take zero overhead.
  - Effortlessly scales to 1,000,000 rows without high memory consumption.
  - Built-in "Load 100k Rows Demo" to test and demonstrate instant 60 FPS viewport scrolling and batch loading.

- **Excel-Like Grid Behavior**:
  - Direct `DrawingVisual` / `DrawingContext` viewport virtualization: only visible rows and columns are measured and rendered.
  - Resizable column widths and row heights with mouse drag handles and interactive double-headed cursors (`<->` and `^v`).
  - Column auto-fit (`Auto-Fit Col`) based on cell contents.
  - Excel-style cell selection (single cell, rectangle drag selection, full row selection, full column selection, top-left corner "select all").
  - In-place cell editing overlay: double-click cell, press `F2`, `Enter`, or start typing alphanumeric characters. Supports `Esc` to cancel, `Enter` / `Tab` / Arrow keys to commit and navigate.
  - Formula/Cell Coordinate bar (`A1`, `B2`, etc.) synchronized bi-directionally with the selected cell and editor.
  - Tab-delimited clipboard copy & paste (Ctrl+C / Ctrl+V) compatible with Microsoft Excel.

- **Data Formatting**:
  - Font styling: Font Family, Font Size, Bold, Italic, Underline.
  - Text & Background colors: Font foreground colors and background fill colors.
  - Text Alignment: Left, Center, Right, Top, Middle, Bottom.
  - Number Formats: General, Number (comma separated decimals), Currency (`$`), Percentage (`%`), Short Date (`yyyy-MM-dd`).

- **Excel & CSV Import & Export**:
  - Stream-reads `.xlsx`, `.xls` (via `ExcelDataReader`), and `.csv` files.
  - Multi-sheet workbook support with sheet tabs at the bottom (Add, Switch, Remove sheets).
  - Clean CSV export with proper escaping and formatting.

---

## Project Structure

```
├── AlaaRep.Spreadsheet.sln
├── Directory.Build.props
├── src/
│   ├── AlaaRep.Spreadsheet/             # WPF Application
│   │   ├── Controls/
│   │   │   └── SpreadsheetGridControl.cs # High-performance virtualized canvas control
│   │   ├── MainWindow.xaml               # Ribbon, Formula bar, Status bar, Grid UI
│   │   ├── MainWindow.xaml.cs
│   │   ├── App.xaml
│   │   └── App.xaml.cs
│   └── AlaaRep.Spreadsheet.Core/        # Core Data Engine & Services
│       ├── Models/
│       │   ├── CellCoordinate.cs         # Excel A1 coordinates parser/formatter
│       │   ├── CellData.cs               # Cell value & formatting model
│       │   ├── CellFormat.cs             # Immutable styling & formatting definitions
│       │   ├── CellRange.cs              # 2D rectangle range coordinates & logic
│       │   ├── SheetDataModel.cs         # Sparse high-capacity sheet data structure
│       │   ├── SpreadsheetDocument.cs    # Multi-sheet document manager
│       │   └── SelectionModel.cs         # Active cell & range selection tracker
│       └── Services/
│           ├── ExcelImportService.cs     # Excel (.xlsx, .xls) and CSV stream reader/exporter
│           └── ClipboardService.cs       # Tab-delimited clipboard copy/paste handler
└── tests/
    └── AlaaRep.Spreadsheet.Tests/        # Automated test suite (xUnit)
        ├── CoreModelTests.cs
        ├── ClipboardTests.cs
        └── SpreadsheetIntegrationTests.cs
```

---

## Build & Test

### Build the Solution
```bash
dotnet build AlaaRep.Spreadsheet.sln -p:EnableWindowsTargeting=true
```

### Run Unit Tests
```bash
dotnet test tests/AlaaRep.Spreadsheet.Tests/AlaaRep.Spreadsheet.Tests.csproj
```
