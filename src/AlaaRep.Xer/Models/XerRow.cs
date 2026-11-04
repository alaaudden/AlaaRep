using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AlaaRep.Xer.Models;

/// <summary>
/// Represents a single record row (%R) in an XER table with dynamic field access and mutation support.
/// Implements INotifyPropertyChanged so WPF UI data binding automatically updates upon changes.
/// </summary>
public class XerRow : INotifyPropertyChanged
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;

    public XerRow()
    {
    }

    public XerRow(IDictionary<string, string> initialValues)
    {
        if (initialValues != null)
        {
            foreach (var kvp in initialValues)
            {
                _values[kvp.Key] = kvp.Value ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Gets or sets a field value by field name (case-insensitive).
    /// </summary>
    public string this[string fieldName]
    {
        get => GetValue(fieldName);
        set => SetValue(fieldName, value);
    }

    public string GetValue(string fieldName, string defaultValue = "")
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return defaultValue;
        return _values.TryGetValue(fieldName, out var val) ? (val ?? defaultValue) : defaultValue;
    }

    public void SetValue(string fieldName, string? value, [CallerMemberName] string? callerProperty = null)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return;
        var newVal = value ?? string.Empty;
        if (_values.TryGetValue(fieldName, out var existing) && existing == newVal)
        {
            return;
        }

        _values[fieldName] = newVal;
        OnPropertyChanged(fieldName);
        if (!string.IsNullOrEmpty(callerProperty) && callerProperty != fieldName)
        {
            OnPropertyChanged(callerProperty);
        }
    }

    public bool HasField(string fieldName)
    {
        return !string.IsNullOrWhiteSpace(fieldName) && _values.ContainsKey(fieldName);
    }

    public IReadOnlyDictionary<string, string> GetAllValues() => _values;

    public XerRow Clone()
    {
        var clone = new XerRow();
        foreach (var kvp in _values)
        {
            clone._values[kvp.Key] = kvp.Value;
        }
        return clone;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
