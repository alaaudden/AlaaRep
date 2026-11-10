using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
    private ScheduleItemNode? _selectedScheduleNode;
    private XerRow? _selectedRawRow;
    private string _searchFilter = string.Empty;
    private string _statusMessage = "Ready. Open an XER file or load sample data to begin.";
    private bool _hasUnsavedChanges;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<XerTable> Tables { get; } = new();
    public ObservableCollection<ScheduleItemNode> VisibleScheduleItems { get; } = new();
    public List<ScheduleItemNode> RootNodes { get; private set; } = new();

    public ObservableCollection<TaskRelationshipItem> Predecessors { get; } = new();
    public ObservableCollection<TaskRelationshipItem> Successors { get; } = new();
    public ObservableCollection<KeyValuePair<string, string>> SelectedRowAttributes { get; } = new();

    public GanttTimelineManager TimelineManager { get; } = new();

    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand LoadSampleCommand { get; }
    public ICommand AddActivityCommand { get; }
    public ICommand DeleteActivityCommand { get; }
    public ICommand ExpandAllCommand { get; }
    public ICommand CollapseAllCommand { get; }
    public ICommand ToggleNodeCommand { get; }
    public ICommand AddTableRowCommand { get; }
    public ICommand DeleteTableRowCommand { get; }

    public MainViewModel()
    {
        OpenCommand = new RelayCommand(OpenFile);
        SaveCommand = new RelayCommand(SaveFile, () => _document != null);
        SaveAsCommand = new RelayCommand(SaveFileAs, () => _document != null);
        LoadSampleCommand = new RelayCommand(LoadSample);
        AddActivityCommand = new RelayCommand(AddActivity, () => _document != null);
        DeleteActivityCommand = new RelayCommand(DeleteActivity, () => SelectedScheduleNode != null);
        ExpandAllCommand = new RelayCommand(ExpandAll);
        CollapseAllCommand = new RelayCommand(CollapseAll);
        ToggleNodeCommand = new RelayCommand(node => ToggleNode(node as ScheduleItemNode));
        AddTableRowCommand = new RelayCommand(AddTableRow, () => SelectedTable != null);
        DeleteTableRowCommand = new RelayCommand(DeleteTableRow, () => SelectedTable != null && SelectedRawRow != null);

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
            return $"AlaaRep Primavera P6 XER Gantt & Schedule Editor - {fileName}{dirty} ({ProjectName})";
        }
    }

    public ScheduleItemNode? SelectedScheduleNode
    {
        get => _selectedScheduleNode;
        set
        {
            _selectedScheduleNode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedActivity));
            OnPropertyChanged(nameof(HasSelectedActivity));
            OnPropertyChanged(nameof(HasSelectedNode));
            RefreshDetailTabs();
        }
    }

    public TaskActivity? SelectedActivity => SelectedScheduleNode?.Activity;
    public bool HasSelectedActivity => SelectedScheduleNode?.Activity != null;
    public bool HasSelectedNode => SelectedScheduleNode != null;

    public XerTable? SelectedTable
    {
        get => _selectedTable;
        set
        {
            _selectedTable = value;
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
            RefreshTreeDisplay();
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
                StatusMessage = $"Loaded {Path.GetFileName(ofd.FileName)} successfully ({doc.Tables.Count} tables, {VisibleScheduleItems.Count} schedule items).";
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
        var doc = SampleXerGenerator.CreateSampleProject("Marina Commercial Complex");
        Document = doc;
        HasUnsavedChanges = false;
        StatusMessage = "Loaded built-in sample schedule with WBS hierarchy and Gantt timeline.";
    }

    public void AddActivity()
    {
        if (_document == null) return;
        var taskTable = _document.GetOrCreateTable("TASK");

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
        taskTable.EnsureField("total_float_hr_cnt");

        string parentWbsId = "1";
        if (SelectedScheduleNode != null)
        {
            parentWbsId = SelectedScheduleNode.IsWbs ? SelectedScheduleNode.Id : SelectedScheduleNode.WbsId;
        }

        int nextId = 2000 + taskTable.Rows.Count + 1;
        var startDate = DateTime.Today.AddDays(7);
        var endDate = startDate.AddDays(14);

        var row = taskTable.CreateRow();
        row.SetValue("task_id", nextId.ToString());
        row.SetValue("proj_id", _document.PrimaryProject?.GetValue("proj_id", "1001") ?? "1001");
        row.SetValue("wbs_id", parentWbsId);
        row.SetValue("task_code", $"A{nextId}");
        row.SetValue("task_name", "New Project Activity");
        row.SetValue("status_code", "TK_NotStart");
        row.SetValue("target_durn_hr_cnt", "80.0");
        row.SetValue("phys_complete_pct", "0.0");
        row.SetValue("total_float_hr_cnt", "0.0");
        row.SetValue("target_start_date", startDate.ToString("yyyy-MM-dd 08:00"));
        row.SetValue("target_end_date", endDate.ToString("yyyy-MM-dd 17:00"));
        row.SetValue("early_start_date", startDate.ToString("yyyy-MM-dd 08:00"));
        row.SetValue("early_end_date", endDate.ToString("yyyy-MM-dd 17:00"));

        RefreshScheduleHierarchy();
        SelectedScheduleNode = VisibleScheduleItems.FirstOrDefault(n => n.Code == $"A{nextId}");
        MarkDirty();
        StatusMessage = $"Added new activity A{nextId}.";
    }

    public void DeleteActivity()
    {
        if (_document == null || SelectedScheduleNode == null) return;
        var node = SelectedScheduleNode;

        if (node.Activity != null && _document.TaskTable != null)
        {
            _document.TaskTable.RemoveRow(node.Activity.UnderlyingRow);
            RefreshScheduleHierarchy();
            SelectedScheduleNode = VisibleScheduleItems.FirstOrDefault();
            MarkDirty();
            StatusMessage = $"Deleted activity {node.Code}.";
        }
        else if (node.IsWbs && _document.WbsTable != null && node.RawRow != null)
        {
            _document.WbsTable.RemoveRow(node.RawRow);
            RefreshScheduleHierarchy();
            SelectedScheduleNode = VisibleScheduleItems.FirstOrDefault();
            MarkDirty();
            StatusMessage = $"Deleted WBS node {node.Code}.";
        }
    }

    public void ExpandAll()
    {
        SetExpansionRecursive(RootNodes, true);
        RefreshTreeDisplay();
    }

    public void CollapseAll()
    {
        SetExpansionRecursive(RootNodes, false);
        // Keep root level expanded
        foreach (var r in RootNodes) r.IsExpanded = true;
        RefreshTreeDisplay();
    }

    private void SetExpansionRecursive(IEnumerable<ScheduleItemNode> nodes, bool isExpanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = isExpanded;
            SetExpansionRecursive(node.Children, isExpanded);
        }
    }

    public void ToggleNode(ScheduleItemNode? node)
    {
        if (node == null || !node.HasChildren) return;
        node.IsExpanded = !node.IsExpanded;
        RefreshTreeDisplay();
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

    public void OnDetailPropertyChanged()
    {
        if (SelectedScheduleNode == null) return;

        var act = SelectedScheduleNode.Activity;
        if (act != null)
        {
            SelectedScheduleNode.Code = act.TaskCode;
            SelectedScheduleNode.Name = act.TaskName;
            SelectedScheduleNode.StatusDisplayName = act.StatusDisplayName;
            SelectedScheduleNode.PercentComplete = act.PhysicalPercentComplete;
            SelectedScheduleNode.DurationDays = act.TargetDurationDays;
            SelectedScheduleNode.TotalFloatHours = act.TotalFloatHours;
            SelectedScheduleNode.IsCritical = act.TotalFloatHours <= 0 && act.StatusCode != "TK_Complete";
            SelectedScheduleNode.StartDate = ScheduleHierarchyBuilder.ParseDate(act.TargetStartDate) ?? ScheduleHierarchyBuilder.ParseDate(act.EarlyStartDate);
            SelectedScheduleNode.EndDate = ScheduleHierarchyBuilder.ParseDate(act.TargetEndDate) ?? ScheduleHierarchyBuilder.ParseDate(act.EarlyEndDate);

            TimelineManager.UpdateItemGanttMetrics(SelectedScheduleNode);
            SelectedScheduleNode.NotifyVisualsChanged();
        }

        MarkDirty();
        RefreshDetailTabs();
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

        RefreshScheduleHierarchy();
    }

    public void RefreshScheduleHierarchy()
    {
        if (_document == null)
        {
            VisibleScheduleItems.Clear();
            RootNodes.Clear();
            return;
        }

        RootNodes = ScheduleHierarchyBuilder.BuildHierarchy(_document);
        RefreshTreeDisplay();
    }

    private void RefreshTreeDisplay()
    {
        VisibleScheduleItems.Clear();
        var flattened = ScheduleHierarchyBuilder.FlattenVisibleTree(RootNodes);

        var filter = _searchFilter.Trim().ToLowerInvariant();
        var filteredList = string.IsNullOrEmpty(filter)
            ? flattened
            : flattened.Where(n => n.Code.ToLowerInvariant().Contains(filter) ||
                                   n.Name.ToLowerInvariant().Contains(filter) ||
                                   n.WbsCode.ToLowerInvariant().Contains(filter)).ToList();

        TimelineManager.CalculateTimeline(filteredList);

        foreach (var item in filteredList)
        {
            VisibleScheduleItems.Add(item);
        }

        if (SelectedScheduleNode == null || !VisibleScheduleItems.Contains(SelectedScheduleNode))
        {
            SelectedScheduleNode = VisibleScheduleItems.FirstOrDefault();
        }
    }

    private void RefreshDetailTabs()
    {
        Predecessors.Clear();
        Successors.Clear();
        SelectedRowAttributes.Clear();

        if (SelectedScheduleNode == null || _document == null) return;

        // Populate raw attributes
        var raw = SelectedScheduleNode.RawRow;
        if (raw != null)
        {
            foreach (var kvp in raw.GetAllValues())
            {
                SelectedRowAttributes.Add(kvp);
            }
        }

        // If activity, lookup relationships from TASKPRED
        var taskId = SelectedScheduleNode.Id;
        var relTable = _document.RelationshipTable;
        var taskTable = _document.TaskTable;

        if (relTable != null && !string.IsNullOrEmpty(taskId))
        {
            var taskNameMap = new Dictionary<string, (string code, string name)>();
            if (taskTable != null)
            {
                foreach (var r in taskTable.Rows)
                {
                    taskNameMap[r.GetValue("task_id")] = (r.GetValue("task_code"), r.GetValue("task_name"));
                }
            }

            foreach (var row in relTable.Rows)
            {
                var rTaskId = row.GetValue("task_id");
                var rPredTaskId = row.GetValue("pred_task_id");
                var relType = row.GetValue("pred_type", "PR_FS");
                var lagHr = double.TryParse(row.GetValue("lag_hr_cnt"), NumberStyles.Any, CultureInfo.InvariantCulture, out var lh) ? lh : 0.0;

                if (string.Equals(rTaskId, taskId, StringComparison.OrdinalIgnoreCase))
                {
                    // This is a predecessor to our selected task
                    taskNameMap.TryGetValue(rPredTaskId, out var predInfo);
                    Predecessors.Add(new TaskRelationshipItem
                    {
                        Direction = "Predecessor",
                        RelationshipId = row.GetValue("task_pred_id"),
                        RelatedTaskId = rPredTaskId,
                        RelatedTaskCode = predInfo.code ?? rPredTaskId,
                        RelatedTaskName = predInfo.name ?? "",
                        Type = relType,
                        LagDays = Math.Round(lagHr / 8.0, 1)
                    });
                }
                else if (string.Equals(rPredTaskId, taskId, StringComparison.OrdinalIgnoreCase))
                {
                    // This is a successor to our selected task
                    taskNameMap.TryGetValue(rTaskId, out var succInfo);
                    Successors.Add(new TaskRelationshipItem
                    {
                        Direction = "Successor",
                        RelationshipId = row.GetValue("task_pred_id"),
                        RelatedTaskId = rTaskId,
                        RelatedTaskCode = succInfo.code ?? rTaskId,
                        RelatedTaskName = succInfo.name ?? "",
                        Type = relType,
                        LagDays = Math.Round(lagHr / 8.0, 1)
                    });
                }
            }
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
