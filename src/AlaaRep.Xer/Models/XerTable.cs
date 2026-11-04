using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AlaaRep.Xer.Models;

/// <summary>
/// Represents an XER table (%T) consisting of field header definitions (%F) and data records (%R).
/// </summary>
public class XerTable
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Ordered list of column / field names defined in %F.
    /// </summary>
    public List<string> Fields { get; } = new();

    /// <summary>
    /// Collection of rows in this table. ObservableCollection enables WPF DataGrid binding.
    /// </summary>
    public ObservableCollection<XerRow> Rows { get; } = new();

    public XerTable()
    {
    }

    public XerTable(string name, IEnumerable<string>? fields = null)
    {
        Name = name ?? string.Empty;
        if (fields != null)
        {
            Fields.AddRange(fields);
        }
    }

    /// <summary>
    /// Ensures that a field exists in the table header schema. If not, appends it.
    /// </summary>
    public void EnsureField(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return;
        if (!Fields.Any(f => string.Equals(f, fieldName, StringComparison.OrdinalIgnoreCase)))
        {
            Fields.Add(fieldName);
        }
    }

    /// <summary>
    /// Adds a new row with the given field-value dictionary.
    /// Automatically ensures any new keys are registered in Fields.
    /// </summary>
    public XerRow AddRow(IDictionary<string, string> values)
    {
        foreach (var key in values.Keys)
        {
            EnsureField(key);
        }
        var row = new XerRow(values);
        Rows.Add(row);
        return row;
    }

    public XerRow CreateRow()
    {
        var row = new XerRow();
        Rows.Add(row);
        return row;
    }

    public bool RemoveRow(XerRow row)
    {
        return Rows.Remove(row);
    }

    public override string ToString() => $"{Name} ({Rows.Count} rows, {Fields.Count} fields)";
}
