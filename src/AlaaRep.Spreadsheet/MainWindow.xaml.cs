using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Win32;
using AlaaRep.Spreadsheet.Core.Models;
using AlaaRep.Spreadsheet.Core.Services;

namespace AlaaRep.Spreadsheet
{
    public partial class MainWindow : Window
    {
        private SpreadsheetDocument _document = new();
        private readonly ExcelImportService _importService = new();
        private bool _isUpdatingFormulaBar;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BindDocument();
        }

        private void BindDocument()
        {
            SpreadsheetGrid.Sheet = _document.ActiveSheet;
            SheetTabsList.ItemsSource = _document.Sheets;

            SpreadsheetGrid.ActiveCellChanged += SpreadsheetGrid_ActiveCellChanged;
            SpreadsheetGrid.EditCommitted += SpreadsheetGrid_EditCommitted;
            _document.ActiveSheetChanged += (s, e) =>
            {
                SpreadsheetGrid.Sheet = _document.ActiveSheet;
                UpdateActiveCellInfo();
                UpdateStatus();
            };

            UpdateActiveCellInfo();
            UpdateStatus();
        }

        private void SpreadsheetGrid_ActiveCellChanged(object? sender, EventArgs e)
        {
            UpdateActiveCellInfo();
            UpdateSelectionSummary();
        }

        private void SpreadsheetGrid_EditCommitted(object? sender, EventArgs e)
        {
            UpdateActiveCellInfo();
            UpdateStatus();
            UpdateSelectionSummary();
        }

        private void UpdateActiveCellInfo()
        {
            if (_document.ActiveSheet == null) return;

            var active = SpreadsheetGrid.Selection.ActiveCell;
            TxtActiveCellCoord.Text = active.ToString();

            _isUpdatingFormulaBar = true;
            try
            {
                var val = _document.ActiveSheet.GetCellValue(active.Row, active.Column);
                TxtActiveCellValue.Text = val?.ToString() ?? string.Empty;

                var format = _document.ActiveSheet.GetCellFormat(active.Row, active.Column);
                BtnBold.IsChecked = format.IsBold;
                BtnItalic.IsChecked = format.IsItalic;
                BtnUnderline.IsChecked = format.IsUnderline;
            }
            finally
            {
                _isUpdatingFormulaBar = false;
            }
        }

        private void UpdateStatus()
        {
            if (_document.ActiveSheet == null) return;
            int maxRow = _document.ActiveSheet.MaxOccupiedRow;
            int maxCol = _document.ActiveSheet.MaxOccupiedColumn;
            StatusOccupied.Text = $"Rows: {(maxRow >= 0 ? maxRow + 1 : 0)} | Columns: {(maxCol >= 0 ? maxCol + 1 : 0)}";
        }

        private void UpdateSelectionSummary()
        {
            if (_document.ActiveSheet == null) return;

            var sel = SpreadsheetGrid.Selection.SelectionRange;
            if (sel.TotalCells <= 1)
            {
                StatusSelectionStats.Text = string.Empty;
                return;
            }

            double sum = 0;
            int numCount = 0;
            int totalOccupied = 0;

            // Only check up to a reasonable cap to avoid lag on selecting 1M rows
            int rLimit = Math.Min(sel.Bottom, sel.Top + 1000);
            int cLimit = Math.Min(sel.Right, sel.Left + 500);

            for (int r = sel.Top; r <= rLimit; r++)
            {
                for (int c = sel.Left; c <= cLimit; c++)
                {
                    var val = _document.ActiveSheet.GetCellValue(r, c);
                    if (val != null)
                    {
                        totalOccupied++;
                        if (double.TryParse(val.ToString(), out double d))
                        {
                            sum += d;
                            numCount++;
                        }
                    }
                }
            }

            if (numCount > 0)
            {
                double avg = sum / numCount;
                StatusSelectionStats.Text = $"Average: {avg:N2}  Count: {totalOccupied}  Sum: {sum:N2}";
            }
            else if (totalOccupied > 0)
            {
                StatusSelectionStats.Text = $"Count: {totalOccupied}";
            }
            else
            {
                StatusSelectionStats.Text = string.Empty;
            }
        }

        // Formula Bar
        private void TxtActiveCellValue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitFormulaBar();
                SpreadsheetGrid.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                UpdateActiveCellInfo();
                SpreadsheetGrid.Focus();
                e.Handled = true;
            }
        }

        private void TxtActiveCellValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFormulaBar) return;
        }

        private void CommitFormulaBar()
        {
            if (_document.ActiveSheet == null) return;
            var active = SpreadsheetGrid.Selection.ActiveCell;
            string text = TxtActiveCellValue.Text;

            if (double.TryParse(text, out double d))
            {
                _document.ActiveSheet.SetCellValue(active.Row, active.Column, d);
            }
            else
            {
                _document.ActiveSheet.SetCellValue(active.Row, active.Column, string.IsNullOrEmpty(text) ? null : text);
            }

            SpreadsheetGrid.InvalidateVisual();
            UpdateStatus();
        }

        // Import Excel / CSV
        private void BtnImportExcel_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Excel & CSV Files (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|Excel Workbooks (*.xlsx)|*.xlsx|Excel 97-2003 (*.xls)|*.xls|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Import Data into Spreadsheet"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    StatusReady.Text = "Importing file...";
                    var doc = _importService.LoadFromFile(ofd.FileName);
                    _document = doc;
                    BindDocument();
                    StatusReady.Text = $"Imported {Path.GetFileName(ofd.FileName)} successfully.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import file:\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusReady.Text = "Import failed.";
                }
            }
        }

        // Export CSV
        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_document.ActiveSheet == null) return;

            var sfd = new SaveFileDialog
            {
                Filter = "CSV (Comma delimited) (*.csv)|*.csv|All Files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = $"{_document.ActiveSheet.Name}.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    _importService.ExportToCsv(_document.ActiveSheet, sfd.FileName);
                    StatusReady.Text = $"Exported to {Path.GetFileName(sfd.FileName)}.";
                    MessageBox.Show("Exported successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Clipboard
        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            SpreadsheetGrid.CopySelectionToClipboard();
            StatusReady.Text = "Copied to clipboard.";
        }

        private void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            SpreadsheetGrid.PasteFromClipboard();
            StatusReady.Text = "Pasted from clipboard.";
            UpdateStatus();
        }

        // Formatting Handlers
        private void ApplyFormat(Func<CellFormat, CellFormat> transform)
        {
            if (_document.ActiveSheet == null) return;
            _document.ActiveSheet.ApplyFormatToRange(SpreadsheetGrid.Selection.SelectionRange, transform);
            SpreadsheetGrid.InvalidateVisual();
        }

        private void BtnBold_Click(object sender, RoutedEventArgs e)
        {
            bool isBold = BtnBold.IsChecked == true;
            ApplyFormat(f => f with { IsBold = isBold });
        }

        private void BtnItalic_Click(object sender, RoutedEventArgs e)
        {
            bool isItalic = BtnItalic.IsChecked == true;
            ApplyFormat(f => f with { IsItalic = isItalic });
        }

        private void BtnUnderline_Click(object sender, RoutedEventArgs e)
        {
            bool isUnderline = BtnUnderline.IsChecked == true;
            ApplyFormat(f => f with { IsUnderline = isUnderline });
        }

        private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontFamilyCombo.SelectedItem is ComboBoxItem item && item.Content is string fontName)
            {
                ApplyFormat(f => f with { FontFamily = fontName });
            }
        }

        private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontSizeCombo.SelectedItem is ComboBoxItem item && double.TryParse(item.Content?.ToString(), out double size))
            {
                ApplyFormat(f => f with { FontSize = size });
            }
        }

        private void ColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorCombo.SelectedItem is ComboBoxItem item && item.Tag is string hex)
            {
                ApplyFormat(f => f with { ForegroundColorHex = hex });
            }
        }

        private void BgColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BgColorCombo.SelectedItem is ComboBoxItem item)
            {
                string? hex = item.Tag as string;
                ApplyFormat(f => f with { BackgroundColorHex = string.IsNullOrEmpty(hex) ? null : hex });
            }
        }

        private void BtnAlignLeft_Click(object sender, RoutedEventArgs e)
        {
            ApplyFormat(f => f with { HorizontalAlignment = CellHorizontalAlignment.Left });
        }

        private void BtnAlignCenter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFormat(f => f with { HorizontalAlignment = CellHorizontalAlignment.Center });
        }

        private void BtnAlignRight_Click(object sender, RoutedEventArgs e)
        {
            ApplyFormat(f => f with { HorizontalAlignment = CellHorizontalAlignment.Right });
        }

        private void FormatTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FormatTypeCombo.SelectedIndex < 0) return;
            NumberFormatType formatType = FormatTypeCombo.SelectedIndex switch
            {
                1 => NumberFormatType.Number,
                2 => NumberFormatType.Currency,
                3 => NumberFormatType.Percentage,
                4 => NumberFormatType.ShortDate,
                _ => NumberFormatType.General
            };
            ApplyFormat(f => f with { FormatType = formatType });
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_document.ActiveSheet == null) return;
            _document.ActiveSheet.ClearRange(SpreadsheetGrid.Selection.SelectionRange, clearValues: true, clearFormats: false);
            SpreadsheetGrid.InvalidateVisual();
            UpdateStatus();
        }

        private void BtnAutoFitCol_Click(object sender, RoutedEventArgs e)
        {
            if (_document.ActiveSheet == null) return;
            var sel = SpreadsheetGrid.Selection.SelectionRange;

            for (int col = sel.Left; col <= sel.Right; col++)
            {
                double maxWidth = 60;
                // Measure cells in col
                for (int row = 0; row <= Math.Min(_document.ActiveSheet.MaxOccupiedRow, 1000); row++)
                {
                    var cell = _document.ActiveSheet.GetCell(row, col);
                    if (cell != null && cell.Value != null)
                    {
                        int len = cell.GetFormattedText().Length;
                        double estimatedWidth = len * 8.5 + 16;
                        if (estimatedWidth > maxWidth) maxWidth = estimatedWidth;
                    }
                }
                _document.ActiveSheet.SetColumnWidth(col, Math.Min(350, maxWidth));
            }
            SpreadsheetGrid.InvalidateVisual();
        }

        // Sheet Tab Buttons
        private void SheetTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SheetDataModel sheet)
            {
                _document.ActiveSheet = sheet;
            }
        }

        private void BtnAddSheet_Click(object sender, RoutedEventArgs e)
        {
            _document.AddSheet();
        }

        // Huge Data Demo Generator (100,000 Rows)
        private void BtnLoadDemo_Click(object sender, RoutedEventArgs e)
        {
            StatusReady.Text = "Generating 100,000 rows...";
            var sheet = _document.ActiveSheet;
            sheet.ClearAll();

            // Set column headers
            string[] headers = { "ID", "Customer", "Region", "Category", "Sales ($)", "Quantity", "Profit ($)", "Date" };
            for (int c = 0; c < headers.Length; c++)
            {
                sheet.SetCellValue(0, c, headers[c], notify: false);
                sheet.SetCellFormat(0, c, new CellFormat { IsBold = true, BackgroundColorHex = "#FFE0E0E0" }, notify: false);
            }

            var rnd = new Random(42);
            string[] customers = { "Acme Corp", "Globex", "Initech", "Umbrella Corp", "Wayne Enterprises", "Stark Ind" };
            string[] regions = { "North", "South", "East", "West", "Central" };
            string[] categories = { "Hardware", "Software", "Services", "Cloud", "Consulting" };

            var currencyFormat = new CellFormat { FormatType = NumberFormatType.Currency };
            var numberFormat = new CellFormat { FormatType = NumberFormatType.Number };

            int rowCount = 100_000;
            var items = new System.Collections.Generic.List<(int Row, int Col, object? Value, CellFormat? Format)>(rowCount * 8);

            for (int r = 1; r <= rowCount; r++)
            {
                items.Add((r, 0, r, numberFormat));
                items.Add((r, 1, customers[rnd.Next(customers.Length)], null));
                items.Add((r, 2, regions[rnd.Next(regions.Length)], null));
                items.Add((r, 3, categories[rnd.Next(categories.Length)], null));
                items.Add((r, 4, Math.Round(rnd.NextDouble() * 5000 + 50, 2), currencyFormat));
                items.Add((r, 5, rnd.Next(1, 100), numberFormat));
                items.Add((r, 6, Math.Round(rnd.NextDouble() * 1500 - 200, 2), currencyFormat));
                items.Add((r, 7, DateTime.Today.AddDays(-rnd.Next(0, 365)).ToString("yyyy-MM-dd"), null));
            }

            sheet.BatchLoad(items);
            SpreadsheetGrid.InvalidateVisual();
            UpdateStatus();
            StatusReady.Text = $"Loaded 100,000 rows demo successfully. Scroll smoothly!";
        }
    }
}
