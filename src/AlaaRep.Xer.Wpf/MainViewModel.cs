using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AlaaRep.Xer.Entities;
using AlaaRep.Xer.Models;
using AlaaRep.Xer.Services;
using Microsoft.Win32;

namespace AlaaRep.Xer.Wpf;

public class MainViewModel : INotifyPropertyChanged
{
    private XerDocument? _document;
    private XerTable? _selectedTable;
    private TaskActivity? _selectedActivity;
    private XerRow? _selectedRawRow;
    private string _searchFilter = string.Empty;
    private string _statusMessage = "Ready. Open an XER file or load sample data to begin.";
    private bool _hasUnsavedChanges;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<XerTable> Tables { get; } = new();
    public ObservableCollection<TaskActivity> Activities { get; } = new();

    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand LoadSampleCommand { get; }
    public ICommand AddActivityCommand { get; }
    public ICommand DeleteActivityCommand { get; }
    public ICommand AddTableRowCommand { get; }
    public ICommand DeleteTableRowCommand { get; }

    public MainViewModel()
    {
        OpenCommand = new RelayCommand(OpenFile);
        SaveCommand = new RelayCommand(SaveFile, () => _document != null);
        SaveAsCommand = new RelayCommand(SaveFileAs, () => _document != null);
        LoadSampleCommand = new RelayCommand(LoadSample);
        AddActivityCommand = new RelayCommand(AddActivity, () => _document != null && _document.TaskTable != null);
        DeleteActivityCommand = new RelayCommand(DeleteActivity, () => SelectedActivity != null);
        AddTableRowCommand = new RelayCommand(AddTableRow, () => SelectedTable != null);
        DeleteTableRowCommand = new RelayCommand(DeleteTableRow, () => SelectedTable != null && SelectedRawRow != null);

        // Load sample project by default so the user sees a functioning schedule immediately
        LoadSample();
    }

    public XerDocument? Document
    {
        get => _document;
        set
        {
            _document = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(WindowTitle));
            RefreshCollections();
        }
    }

    public string ProjectName
    {
        get
        {
            var proj = _document?.PrimaryProject;
            return proj?.GetValue("proj_short_name", "Untitled Project") ?? "Untitled Project";
        }
        set
        {
            var proj = _document?.PrimaryProject;
            if (proj != null)
            {
                proj.SetValue("proj_short_name", value);
                MarkDirty();
                OnPropertyChanged();
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public string WindowTitle
    {
        get
        {
            var fileName = !string.IsNullOrEmpty(_document?.FilePath)
                ? Path.GetFileName(_document.FilePath)
                : "Untitled.xer";
            var dirty = HasUnsavedChanges ? "*" : "";
            return $"AlaaRep XER Editor & Rewriter - {fileName}{dirty} ({ProjectName})";
        }
    }

    public XerTable? SelectedTable
    {
        get => _selectedTable;
        set
        {
            _selectedTable = value;
            OnPropertyChanged();
        }
    }

    public TaskActivity? SelectedActivity
    {
        get => _selectedActivity;
        set
        {
            _selectedActivity = value;
            OnPropertyChanged();
        }
    }

    public XerRow? SelectedRawRow
    {
        get => _selectedRawRow;
        set
        {
            _selectedRawRow = value;
            OnPropertyChanged();
        }
    }

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            _searchFilter = value;
            OnPropertyChanged();
            ApplyActivityFilter();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set
        {
            _hasUnsavedChanges = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public void MarkDirty()
    {
        HasUnsavedChanges = true;
    }

    public void OpenFile()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Primavera P6 XER files (*.xer)|*.xer|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Open Primavera P6 XER File"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                var doc = XerReader.ReadFile(ofd.FileName);
                Document = doc;
                HasUnsavedChanges = false;
                StatusMessage = $"Loaded {Path.GetFileName(ofd.FileName)} successfully ({doc.Tables.Count} tables, {Activities.Count} activities).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening file: {ex.Message}";
            }
        }
    }

    public void SaveFile()
    {
        if (_document == null) return;
        if (string.IsNullOrEmpty(_document.FilePath))
        {
            SaveFileAs();
            return;
        }

        try
        {
            XerWriter.WriteFile(_document, _document.FilePath);
            HasUnsavedChanges = false;
            StatusMessage = $"Saved successfully to {_document.FilePath} at {DateTime.Now:HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving file: {ex.Message}";
        }
    }

    public void SaveFileAs()
    {
        if (_document == null) return;

        var sfd = new SaveFileDialog
        {
            Filter = "Primavera P6 XER files (*.xer)|*.xer|All files (*.*)|*.*",
            Title = "Save Modified XER File As",
            FileName = !string.IsNullOrEmpty(_document.FilePath) ? Path.GetFileName(_document.FilePath) : "modified_project.xer"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                XerWriter.WriteFile(_document, sfd.FileName);
                _document.FilePath = sfd.FileName;
                HasUnsavedChanges = false;
                StatusMessage = $"Saved successfully to {sfd.FileName}.";
                OnPropertyChanged(nameof(WindowTitle));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving file: {ex.Message}";
            }
        }
    }

    public void LoadSample()
    {
        var doc = SampleXerGenerator.CreateSampleProject("Commercial Tower Construction");
        Document = doc;
        HasUnsavedChanges = false;
        StatusMessage = "Loaded built-in sample project schedule.";
    }

    public void AddActivity()
    {
        if (_document == null) return;
        var taskTable = _document.GetOrCreateTable("TASK");

        // Ensure key fields exist
        taskTable.EnsureField("task_id");
        taskTable.EnsureField("proj_id");
        taskTable.EnsureField("wbs_id");
        taskTable.EnsureField("task_code");
        taskTable.EnsureField("task_name");
        taskTable.EnsureField("status_code");
        taskTable.EnsureField("target_durn_hr_cnt");
        taskTable.EnsureField("target_start_date");
        taskTable.EnsureField("target_end_date");
        taskTable.EnsureField("phys_complete_pct");

        int nextId = 1000 + taskTable.Rows.Count + 1;
        var row = taskTable.CreateRow();
        row.SetValue("task_id", nextId.ToString());
        row.SetValue("proj_id", _document.PrimaryProject?.GetValue("proj_id", "1001") ?? "1001");
        row.SetValue("wbs_id", "1");
        row.SetValue("task_code", $"A{nextId}");
        row.SetValue("task_name", "New Schedule Activity");
        row.SetValue("status_code", "TK_NotStart");
        row.SetValue("target_durn_hr_cnt", "80.0");
        row.SetValue("phys_complete_pct", "0.0");
        row.SetValue("target_start_date", DateTime.Now.ToString("yyyy-MM-dd 08:00"));
        row.SetValue("target_end_date", DateTime.Now.AddDays(10).ToString("yyyy-MM-dd 17:00"));

        var activity = new TaskActivity(row);
        activity.PropertyChanged += (s, e) => MarkDirty();
        Activities.Add(activity);
        SelectedActivity = activity;
        MarkDirty();
        StatusMessage = $"Added new activity {activity.TaskCode}.";
    }

    public void DeleteActivity()
    {
        if (_document == null || SelectedActivity == null) return;
        var activity = SelectedActivity;
        var taskTable = _document.TaskTable;
        if (taskTable != null)
        {
            taskTable.RemoveRow(activity.UnderlyingRow);
            Activities.Remove(activity);
            SelectedActivity = Activities.FirstOrDefault();
            MarkDirty();
            StatusMessage = $"Removed activity {activity.TaskCode}.";
        }
    }

    public void AddTableRow()
    {
        if (SelectedTable == null) return;
        var row = SelectedTable.CreateRow();
        SelectedRawRow = row;
        MarkDirty();
        StatusMessage = $"Added new row to table {SelectedTable.Name}.";
    }

    public void DeleteTableRow()
    {
        if (SelectedTable == null || SelectedRawRow == null) return;
        var row = SelectedRawRow;
        SelectedTable.RemoveRow(row);
        SelectedRawRow = SelectedTable.Rows.FirstOrDefault();
        MarkDirty();
        StatusMessage = $"Removed row from table {SelectedTable.Name}.";
    }

    private void RefreshCollections()
    {
        Tables.Clear();
        if (_document != null)
        {
            foreach (var t in _document.Tables)
            {
                Tables.Add(t);
            }
            SelectedTable = _document.Tables.FirstOrDefault();
        }

        ApplyActivityFilter();
    }

    private void ApplyActivityFilter()
    {
        Activities.Clear();
        var taskTable = _document?.TaskTable;
        if (taskTable == null) return;

        var filter = _searchFilter.Trim().ToLowerInvariant();

        foreach (var row in taskTable.Rows)
        {
            var activity = new TaskActivity(row);
            activity.PropertyChanged += (s, e) => MarkDirty();

            if (string.IsNullOrEmpty(filter) ||
                activity.TaskCode.ToLowerInvariant().Contains(filter) ||
                activity.TaskName.ToLowerInvariant().Contains(filter) ||
                activity.StatusDisplayName.ToLowerInvariant().Contains(filter))
            {
                Activities.Add(activity);
            }
        }

        SelectedActivity = Activities.FirstOrDefault();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
