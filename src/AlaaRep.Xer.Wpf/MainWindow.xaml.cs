using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace AlaaRep.Xer.Wpf;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private bool _isSyncingScroll;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            _viewModel = vm;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            UpdateRawTableColumns();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTable) ||
            e.PropertyName == nameof(MainViewModel.Document))
        {
            UpdateRawTableColumns();
        }
    }

    /// <summary>
    /// Dynamically constructs columns for RawTableDataGrid based on the selected table's %F fields.
    /// </summary>
    private void UpdateRawTableColumns()
    {
        RawTableDataGrid.Columns.Clear();

        if (_viewModel?.SelectedTable == null)
        {
            RawTableDataGrid.ItemsSource = null;
            return;
        }

        var table = _viewModel.SelectedTable;
        foreach (var field in table.Fields)
        {
            var col = new DataGridTextColumn
            {
                Header = field,
                Binding = new Binding($"[{field}]")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                },
                Width = new DataGridLength(140)
            };
            RawTableDataGrid.Columns.Add(col);
        }

        RawTableDataGrid.ItemsSource = table.Rows;
    }

    /// <summary>
    /// Synchronizes vertical scrolling from TreeDataGrid to GanttScrollViewer.
    /// </summary>
    private void TreeDataGrid_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll) return;
        try
        {
            _isSyncingScroll = true;
            if (e.VerticalChange != 0)
            {
                GanttScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            }
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    /// <summary>
    /// Synchronizes vertical scrolling from GanttScrollViewer to TreeDataGrid,
    /// and horizontal scrolling to TimelineHeaderScrollViewer.
    /// </summary>
    private void GanttScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.HorizontalChange != 0)
        {
            TimelineHeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        }

        if (_isSyncingScroll) return;
        try
        {
            _isSyncingScroll = true;
            if (e.VerticalChange != 0)
            {
                var scrollViewer = GetScrollViewer(TreeDataGrid);
                scrollViewer?.ScrollToVerticalOffset(e.VerticalOffset);
            }
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private static ScrollViewer? GetScrollViewer(DependencyObject dep)
    {
        if (dep is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
        {
            var child = VisualTreeHelper.GetChild(dep, i);
            var result = GetScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }
}
