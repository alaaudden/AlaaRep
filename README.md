# AlaaRep XER Suite

A complete C# .NET 8 and WPF framework solution for parsing, modifying, and rewriting Primavera P6 `.xer` files.

## Projects

- **`src/AlaaRep.Xer`**: Core class library providing:
  - `XerDocument`: In-memory representation of an XER document, its header (`ERMHDR`), and its tables (`%T`).
  - `XerTable`: Dynamic table structure containing `%F` field names and `%R` record rows.
  - `XerRow`: Observable row supporting dictionary-like access (`row["task_code"] = "A1010"`), INotifyPropertyChanged for WPF binding, and row creation/deletion.
  - `XerReader`: High performance, encoding-resilient parser (Windows-1252, UTF-8, Latin-1).
  - `XerWriter`: Faithful Primavera P6 compliant serializer preserving `%T`, `%F`, `%R`, and `%E` tab-delimited formatting.
  - `TaskActivity`: Strongly-typed entity wrapper around P6 `TASK` records with date, float, and progress calculation helpers.
  - `SampleXerGenerator`: Built-in generator creating complete Primavera P6 schedules.
- **`src/AlaaRep.Xer.Wpf`**: WPF desktop application featuring:
  - **Activity Schedule Editor**: Curated grid with Activity ID, Name, Status, % Complete, Start & Finish dates, and Total Float.
  - **All Tables & Raw XER Inspector**: Dynamic explorer allowing inspection and modification of any Primavera P6 table (e.g. `PROJECT`, `PROJWBS`, `TASK`, `TASKPRED`, `CALENDAR`).
  - **Ribbon & File Operations**: Open XER, Save, Save As, Load Sample, Add Activity, Delete Activity, and real-time Search / Filter.
- **`tests/AlaaRep.Xer.Tests`**: Unit and integration test suite covering parsing, programmatic modifications, CRUD operations, file I/O, and round-trip re-parsing verification.

---

## C# Code Examples

### 1. Reading an XER File

```csharp
using AlaaRep.Xer.Models;
using AlaaRep.Xer.Services;

// Read and parse an XER file
XerDocument doc = XerReader.ReadFile("path/to/project.xer");

Console.WriteLine($"Header: {doc.HeaderLine}");
Console.WriteLine($"Project: {doc.PrimaryProject?["proj_short_name"]}");
Console.WriteLine($"Total Tables: {doc.Tables.Count}");
```

### 2. Modifying Activities and Tables

```csharp
// Access the TASK table
XerTable? taskTable = doc.TaskTable;
if (taskTable != null)
{
    // Find an activity by code
    XerRow? activity = taskTable.Rows.FirstOrDefault(r => r["task_code"] == "ENG1010");
    if (activity != null)
    {
        activity["task_name"] = "Updated Engineering Survey";
        activity["phys_complete_pct"] = "50.0";
        activity["status_code"] = "TK_Active";
    }

    // Add a new activity
    XerRow newActivity = taskTable.CreateRow();
    newActivity["task_id"] = "1099";
    newActivity["proj_id"] = "1001";
    newActivity["wbs_id"] = "2";
    newActivity["task_code"] = "ENG1090";
    newActivity["task_name"] = "Permit Expediting";
    newActivity["target_durn_hr_cnt"] = "40.0";
    newActivity["status_code"] = "TK_NotStart";
}
```

### 3. Rewriting / Saving Back to XER

```csharp
// Write back to disk preserving Primavera P6 format and encoding
XerWriter.WriteFile(doc, "path/to/project_modified.xer");
```

### 4. Strongly-Typed Modification with `TaskActivity`

```csharp
using AlaaRep.Xer.Entities;

var row = doc.TaskTable!.Rows[0];
var activity = new TaskActivity(row);

// Properties update the underlying XerRow in real-time
activity.TaskName = "Notice to Proceed - Finalized";
activity.PhysicalPercentComplete = 100.0;
activity.StatusDisplayName = "Completed";
activity.TargetDurationDays = 15.0; // Converts to 120.0 hours
```

---

## Building and Running Tests

```bash
# Build solution
dotnet build AlaaRep.Xer.sln

# Run all tests
dotnet test AlaaRep.Xer.sln
```
