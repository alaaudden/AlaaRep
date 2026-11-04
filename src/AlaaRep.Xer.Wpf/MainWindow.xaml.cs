using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AlaaRep.Xer.Models;

namespace AlaaRep.Xer.Wpf;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

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
                // XerRow indexer binding: [field]
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
}
