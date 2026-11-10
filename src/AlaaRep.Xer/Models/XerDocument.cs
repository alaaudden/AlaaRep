using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AlaaRep.Xer.Models;

/// <summary>
/// Represents a complete Primavera P6 XER export document.
/// </summary>
public class XerDocument
{
    /// <summary>
    /// File header line (typically: ERMHDR\t8.3\t2024-03-29\tPROJECT or similar).
    /// </summary>
    public string HeaderLine { get; set; } = "ERMHDR\t8.3\t" + DateTime.Now.ToString("yyyy-MM-dd") + "\tPROJECT";

    /// <summary>
    /// File path this document was loaded from or saved to.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Ordered list of tables present in the XER file.
    /// </summary>
    public ObservableCollection<XerTable> Tables { get; } = new();

    /// <summary>
    /// Gets a table by its name (case-insensitive), or null if not found.
    /// </summary>
    public XerTable? GetTable(string tableName)
    {
        return Tables.FirstOrDefault(t => string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets or creates a table with the given name.
    /// </summary>
    public XerTable GetOrCreateTable(string tableName)
    {
        var table = GetTable(tableName);
        if (table == null)
        {
            table = new XerTable(tableName);
            Tables.Add(table);
        }
        return table;
    }

    /// <summary>
    /// Common Primavera P6 tables shortcuts:
    /// </summary>
    public XerTable? ProjectTable => GetTable("PROJECT");
    public XerTable? WbsTable => GetTable("PROJWBS");
    public XerTable? TaskTable => GetTable("TASK");
    public XerTable? RelationshipTable => GetTable("TASKPRED");
    public XerTable? CalendarTable => GetTable("CALENDAR");
    public XerTable? ResourceTable => GetTable("RSRC");

    /// <summary>
    /// Gets the primary project row from PROJECT table, or null if empty.
    /// </summary>
    public XerRow? PrimaryProject => ProjectTable?.Rows.FirstOrDefault();
}
