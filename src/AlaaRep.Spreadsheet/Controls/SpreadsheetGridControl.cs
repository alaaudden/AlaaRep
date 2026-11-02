using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AlaaRep.Spreadsheet.Core.Models;

namespace AlaaRep.Spreadsheet.Controls
{
    public class SpreadsheetGridControl : Control
    {
        private SheetDataModel? _sheet;
        private VirtualLayoutManager? _layoutManager;
        private readonly SelectionModel _selection = new();

        // Scrollbars
        private ScrollBar? _horizontalScrollBar;
        private ScrollBar? _verticalScrollBar;

        // In-place editing overlay
        private TextBox? _editorTextBox;
        private bool _isEditing;

        // Mouse interaction state
        private bool _isDraggingSelection;
        private bool _isResizingColumn;
        private bool _isResizingRow;
        private int _resizingColIndex = -1;
        private int _resizingRowIndex = -1;
        private double _resizeStartPos;
        private double _resizeOriginalSize;

        private readonly AlaaRep.Spreadsheet.Core.Services.ClipboardService _clipboardService = new();

        // Brushes and Pens
        private static readonly Brush HeaderBackgroundBrush = new SolidColorBrush(Color.FromRgb(244, 245, 247));
        private static readonly Brush HeaderHoverBrush = new SolidColorBrush(Color.FromRgb(228, 230, 235));
        private static readonly Brush HeaderSelectedBrush = new SolidColorBrush(Color.FromRgb(200, 220, 248));
        private static readonly Pen GridLinePen = new Pen(new SolidColorBrush(Color.FromRgb(218, 220, 224)), 1);
        private static readonly Pen HeaderBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(190, 193, 198)), 1);
        private static readonly Brush SelectionFillBrush = new SolidColorBrush(Color.FromArgb(40, 33, 115, 70)); // Excel green tint
        private static readonly Pen SelectionBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(33, 115, 70)), 2);
        private static readonly Brush ActiveCellFillBrush = Brushes.White;

        public static readonly DependencyProperty SheetProperty =
            DependencyProperty.Register(
                nameof(Sheet),
                typeof(SheetDataModel),
                typeof(SpreadsheetGridControl),
                new FrameworkPropertyMetadata(null, OnSheetChanged));

        public SheetDataModel? Sheet
        {
            get => (SheetDataModel?)GetValue(SheetProperty);
            set => SetValue(SheetProperty, value);
        }

        public SelectionModel Selection => _selection;
        public bool IsEditing => _isEditing;

        public event EventHandler? ActiveCellChanged;
        public event EventHandler? EditCommitted;

        static SpreadsheetGridControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(SpreadsheetGridControl),
                new FrameworkPropertyMetadata(typeof(SpreadsheetGridControl)));
        }

        public SpreadsheetGridControl()
        {
            Focusable = true;
            ClipToBounds = true;

            _selection.SelectionChanged += (s, e) =>
            {
                ActiveCellChanged?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
            };

            SetupEditor();
            SetupScrollBars();

            Loaded += (s, e) =>
            {
                UpdateScrollBars();
                InvalidateVisual();
            };
            SizeChanged += (s, e) =>
            {
                UpdateScrollBars();
                InvalidateVisual();
            };
        }

        private static void OnSheetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpreadsheetGridControl grid)
            {
                if (e.OldValue is SheetDataModel oldSheet)
                {
                    oldSheet.DataChanged -= grid.OnSheetDataChanged;
                    oldSheet.LayoutChanged -= grid.OnSheetLayoutChanged;
                }
                if (e.NewValue is SheetDataModel newSheet)
                {
                    grid._sheet = newSheet;
                    grid._layoutManager = new VirtualLayoutManager(newSheet);
                    newSheet.DataChanged += grid.OnSheetDataChanged;
                    newSheet.LayoutChanged += grid.OnSheetLayoutChanged;
                    grid.UpdateScrollBars();
                    grid.InvalidateVisual();
                }
            }
        }

        private void OnSheetDataChanged(object? sender, EventArgs e)
        {
            InvalidateVisual();
        }

        private void OnSheetLayoutChanged(object? sender, EventArgs e)
        {
            UpdateScrollBars();
            InvalidateVisual();
        }

        private void SetupEditor()
        {
            _editorTextBox = new TextBox
            {
                Visibility = Visibility.Collapsed,
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Color.FromRgb(33, 115, 70)),
                Padding = new Thickness(2, 1, 2, 1),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _editorTextBox.KeyDown += EditorTextBox_KeyDown;
            _editorTextBox.LostFocus += EditorTextBox_LostFocus;
            AddVisualChild(_editorTextBox);
        }

        private void SetupScrollBars()
        {
            _horizontalScrollBar = new ScrollBar
            {
                Orientation = Orientation.Horizontal,
                Minimum = 0,
                SmallChange = 1,
                LargeChange = 5
            };
            _horizontalScrollBar.Scroll += (s, e) =>
            {
                if (_isEditing) CommitEdit();
                InvalidateVisual();
            };
            AddVisualChild(_horizontalScrollBar);

            _verticalScrollBar = new ScrollBar
            {
                Orientation = Orientation.Vertical,
                Minimum = 0,
                SmallChange = 1,
                LargeChange = 15
            };
            _verticalScrollBar.Scroll += (s, e) =>
            {
                if (_isEditing) CommitEdit();
                InvalidateVisual();
            };
            AddVisualChild(_verticalScrollBar);
        }

        protected override int VisualChildrenCount => 3; // ScrollBars (2) + Editor (1)

        protected override Visual GetVisualChild(int index)
        {
            return index switch
            {
                0 => _horizontalScrollBar!,
                1 => _verticalScrollBar!,
                2 => _editorTextBox!,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            double sbSize = SystemParameters.VerticalScrollBarWidth;
            double headerW = _layoutManager?.HeaderWidth ?? 48;
            double headerH = _layoutManager?.HeaderHeight ?? 26;

            if (_horizontalScrollBar != null)
            {
                _horizontalScrollBar.Arrange(new Rect(
                    headerW,
                    Math.Max(0, arrangeBounds.Height - sbSize),
                    Math.Max(0, arrangeBounds.Width - headerW - sbSize),
                    sbSize));
            }

            if (_verticalScrollBar != null)
            {
                _verticalScrollBar.Arrange(new Rect(
                    Math.Max(0, arrangeBounds.Width - sbSize),
                    headerH,
                    sbSize,
                    Math.Max(0, arrangeBounds.Height - headerH - sbSize)));
            }

            if (_isEditing && _editorTextBox != null && _sheet != null && _layoutManager != null)
            {
                var rect = GetCellScreenRect(_selection.ActiveCell.Row, _selection.ActiveCell.Column);
                _editorTextBox.Arrange(rect);
            }

            return arrangeBounds;
        }

        private void UpdateScrollBars()
        {
            if (_sheet == null || _layoutManager == null || _horizontalScrollBar == null || _verticalScrollBar == null)
                return;

            double sbSize = SystemParameters.VerticalScrollBarWidth;
            double viewW = Math.Max(10, ActualWidth - _layoutManager.HeaderWidth - sbSize);
            double viewH = Math.Max(10, ActualHeight - _layoutManager.HeaderHeight - sbSize);

            // Columns
            int visibleColCount = (int)Math.Max(1, viewW / _sheet.DefaultColumnWidth);
            _horizontalScrollBar.Maximum = Math.Max(0, _sheet.TotalColumnCount - visibleColCount);
            _horizontalScrollBar.ViewportSize = visibleColCount;

            // Rows
            int visibleRowCount = (int)Math.Max(1, viewH / _sheet.DefaultRowHeight);
            _verticalScrollBar.Maximum = Math.Max(0, _sheet.TotalRowCount - visibleRowCount);
            _verticalScrollBar.ViewportSize = visibleRowCount;
        }

        public Rect GetCellScreenRect(int row, int col)
        {
            if (_sheet == null || _layoutManager == null) return Rect.Empty;

            double headerW = _layoutManager.HeaderWidth;
            double headerH = _layoutManager.HeaderHeight;

            int scrollRow = (int)(_verticalScrollBar?.Value ?? 0);
            int scrollCol = (int)(_horizontalScrollBar?.Value ?? 0);

            double x = headerW + (col - scrollCol) * _sheet.DefaultColumnWidth;
            double y = headerH + (row - scrollRow) * _sheet.DefaultRowHeight;
            double w = _sheet.GetColumnWidth(col);
            double h = _sheet.GetRowHeight(row);

            return new Rect(x, y, w, h);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_sheet == null || _layoutManager == null)
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));
                return;
            }

            double headerW = _layoutManager.HeaderWidth;
            double headerH = _layoutManager.HeaderHeight;
            double sbSize = SystemParameters.VerticalScrollBarWidth;

            double viewportWidth = Math.Max(0, ActualWidth - headerW - sbSize);
            double viewportHeight = Math.Max(0, ActualHeight - headerH - sbSize);

            int startRow = (int)(_verticalScrollBar?.Value ?? 0);
            int startCol = (int)(_horizontalScrollBar?.Value ?? 0);

            int visibleRowCount = (int)Math.Ceiling(viewportHeight / _sheet.DefaultRowHeight) + 1;
            int visibleColCount = (int)Math.Ceiling(viewportWidth / _sheet.DefaultColumnWidth) + 1;

            int endRow = Math.Min(_sheet.TotalRowCount - 1, startRow + visibleRowCount);
            int endCol = Math.Min(_sheet.TotalColumnCount - 1, startCol + visibleColCount);

            // 1. Draw Background
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));

            // Clip grid area
            dc.PushClip(new RectangleGeometry(new Rect(headerW, headerH, viewportWidth, viewportHeight)));

            // 2. Draw cell backgrounds and values
            for (int r = startRow; r <= endRow; r++)
            {
                double y = headerH + (r - startRow) * _sheet.DefaultRowHeight;
                double h = _sheet.GetRowHeight(r);

                for (int c = startCol; c <= endCol; c++)
                {
                    double x = headerW + (c - startCol) * _sheet.DefaultColumnWidth;
                    double w = _sheet.GetColumnWidth(c);
                    var cellRect = new Rect(x, y, w, h);

                    var cell = _sheet.GetCell(r, c);
                    var format = cell?.Format ?? CellFormat.Default;

                    // Custom background if specified
                    if (!string.IsNullOrEmpty(format.BackgroundColorHex))
                    {
                        try
                        {
                            var bg = (Brush)new BrushConverter().ConvertFromString(format.BackgroundColorHex)!;
                            dc.DrawRectangle(bg, null, cellRect);
                        }
                        catch { }
                    }

                    // Draw cell text
                    if (cell != null && cell.Value != null)
                    {
                        string displayText = cell.GetFormattedText();
                        if (!string.IsNullOrEmpty(displayText))
                        {
                            DrawCellText(dc, displayText, format, cellRect);
                        }
                    }
                }
            }

            // 3. Draw Grid Lines
            for (int r = startRow; r <= endRow + 1; r++)
            {
                double y = headerH + (r - startRow) * _sheet.DefaultRowHeight;
                dc.DrawLine(GridLinePen, new Point(headerW, y), new Point(headerW + viewportWidth, y));
            }
            for (int c = startCol; c <= endCol + 1; c++)
            {
                double x = headerW + (c - startCol) * _sheet.DefaultColumnWidth;
                dc.DrawLine(GridLinePen, new Point(x, headerH), new Point(x, headerH + viewportHeight));
            }

            // 4. Draw Selection Box
            DrawSelection(dc, startRow, startCol, headerW, headerH);

            dc.Pop(); // End grid clip

            // 5. Draw Headers
            DrawHeaders(dc, startRow, endRow, startCol, endCol, headerW, headerH, viewportWidth, viewportHeight);

            // 6. Draw Top-Left Corner Box
            dc.DrawRectangle(HeaderBackgroundBrush, HeaderBorderPen, new Rect(0, 0, headerW, headerH));
            // Triangle in corner
            var pathGeom = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(headerW - 3, headerH - 3), IsClosed = true };
            figure.Segments.Add(new LineSegment(new Point(headerW - 10, headerH - 3), false));
            figure.Segments.Add(new LineSegment(new Point(headerW - 3, headerH - 10), false));
            pathGeom.Figures.Add(figure);
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(170, 173, 178)), null, pathGeom);
        }

        private void DrawCellText(DrawingContext dc, string text, CellFormat format, Rect cellRect)
        {
            Brush brush = Brushes.Black;
            if (!string.IsNullOrEmpty(format.ForegroundColorHex))
            {
                try
                {
                    if (new BrushConverter().ConvertFromString(format.ForegroundColorHex) is Brush b)
                    {
                        brush = b;
                    }
                }
                catch { }
            }

            var typeFace = new Typeface(
                new FontFamily(format.FontFamily),
                format.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                format.IsBold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);

            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeFace,
                format.FontSize,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = Math.Max(1, cellRect.Width - 6),
                MaxTextHeight = Math.Max(1, cellRect.Height - 4)
            };

            double textX = cellRect.Left + 4;
            if (format.HorizontalAlignment == CellHorizontalAlignment.Center)
            {
                textX = cellRect.Left + (cellRect.Width - formattedText.Width) / 2;
            }
            else if (format.HorizontalAlignment == CellHorizontalAlignment.Right)
            {
                textX = cellRect.Right - formattedText.Width - 4;
            }

            double textY = cellRect.Top + (cellRect.Height - formattedText.Height) / 2;
            if (format.VerticalAlignment == CellVerticalAlignment.Top)
            {
                textY = cellRect.Top + 2;
            }
            else if (format.VerticalAlignment == CellVerticalAlignment.Bottom)
            {
                textY = cellRect.Bottom - formattedText.Height - 2;
            }

            dc.PushClip(new RectangleGeometry(new Rect(cellRect.Left + 2, cellRect.Top + 1, cellRect.Width - 4, cellRect.Height - 2)));
            dc.DrawText(formattedText, new Point(textX, textY));
            if (format.IsUnderline)
            {
                dc.DrawLine(new Pen(brush, 1), new Point(textX, textY + formattedText.Height), new Point(textX + formattedText.Width, textY + formattedText.Height));
            }
            if (format.IsStrikethrough)
            {
                dc.DrawLine(new Pen(brush, 1), new Point(textX, textY + formattedText.Height / 2), new Point(textX + formattedText.Width, textY + formattedText.Height / 2));
            }
            dc.Pop();
        }

        private void DrawSelection(DrawingContext dc, int startRow, int startCol, double headerW, double headerH)
        {
            if (_sheet == null) return;

            var sel = _selection.SelectionRange;
            double selX = headerW + (sel.Left - startCol) * _sheet.DefaultColumnWidth;
            double selY = headerH + (sel.Top - startRow) * _sheet.DefaultRowHeight;
            double selW = sel.ColumnCount * _sheet.DefaultColumnWidth;
            double selH = sel.RowCount * _sheet.DefaultRowHeight;

            var selRect = new Rect(selX, selY, selW, selH);

            // Shading for multi-cell selection
            if (sel.RowCount > 1 || sel.ColumnCount > 1)
            {
                dc.DrawRectangle(SelectionFillBrush, null, selRect);
            }

            // Green Border
            dc.DrawRectangle(null, SelectionBorderPen, selRect);

            // Active cell inside range (white fill when in range)
            var active = _selection.ActiveCell;
            double actX = headerW + (active.Column - startCol) * _sheet.DefaultColumnWidth;
            double actY = headerH + (active.Row - startRow) * _sheet.DefaultRowHeight;
            double actW = _sheet.GetColumnWidth(active.Column);
            double actH = _sheet.GetRowHeight(active.Row);
            var actRect = new Rect(actX, actY, actW, actH);

            if (sel.RowCount > 1 || sel.ColumnCount > 1)
            {
                // Active cell within range is shown unshaded
                dc.DrawRectangle(ActiveCellFillBrush, null, new Rect(actX + 1, actY + 1, actW - 2, actH - 2));
                var activeCellData = _sheet.GetCell(active.Row, active.Column);
                if (activeCellData?.Value != null)
                {
                    DrawCellText(dc, activeCellData.GetFormattedText(), activeCellData.Format, actRect);
                }
            }

            // Little square fill handle at bottom right
            double handleSize = 6;
            Rect handleRect = new Rect(selRect.Right - handleSize / 2, selRect.Bottom - handleSize / 2, handleSize, handleSize);
            dc.DrawRectangle(SelectionBorderPen.Brush as Brush ?? Brushes.Green, null, handleRect);
        }

        private void DrawHeaders(DrawingContext dc, int startRow, int endRow, int startCol, int endCol, double headerW, double headerH, double viewportW, double viewportH)
        {
            if (_sheet == null) return;

            var font = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var fontSelected = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

            // Column Headers
            for (int c = startCol; c <= endCol; c++)
            {
                double x = headerW + (c - startCol) * _sheet.DefaultColumnWidth;
                double w = _sheet.GetColumnWidth(c);
                var headerRect = new Rect(x, 0, w, headerH);

                bool isColSelected = _selection.SelectionRange.Left <= c && c <= _selection.SelectionRange.Right;
                Brush bgBrush = isColSelected ? HeaderSelectedBrush : HeaderBackgroundBrush;

                dc.DrawRectangle(bgBrush, HeaderBorderPen, headerRect);

                string colLetters = CellCoordinate.ColumnIndexToLetters(c);
                var text = new FormattedText(
                    colLetters,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    isColSelected ? fontSelected : font,
                    11.0,
                    isColSelected ? Brushes.Black : new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                double tx = headerRect.Left + (headerRect.Width - text.Width) / 2;
                double ty = headerRect.Top + (headerRect.Height - text.Height) / 2;
                dc.DrawText(text, new Point(tx, ty));
            }

            // Row Headers
            for (int r = startRow; r <= endRow; r++)
            {
                double y = headerH + (r - startRow) * _sheet.DefaultRowHeight;
                double h = _sheet.GetRowHeight(r);
                var headerRect = new Rect(0, y, headerW, h);

                bool isRowSelected = _selection.SelectionRange.Top <= r && r <= _selection.SelectionRange.Bottom;
                Brush bgBrush = isRowSelected ? HeaderSelectedBrush : HeaderBackgroundBrush;

                dc.DrawRectangle(bgBrush, HeaderBorderPen, headerRect);

                string rowNumber = (r + 1).ToString();
                var text = new FormattedText(
                    rowNumber,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    isRowSelected ? fontSelected : font,
                    11.0,
                    isRowSelected ? Brushes.Black : new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                double tx = headerRect.Right - text.Width - 6;
                double ty = headerRect.Top + (headerRect.Height - text.Height) / 2;
                dc.DrawText(text, new Point(tx, ty));
            }
        }

        // Keyboard & Mouse Navigation
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (_isEditing)
            {
                return; // Let editor handle it
            }

            int row = _selection.ActiveCell.Row;
            int col = _selection.ActiveCell.Column;

            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            switch (e.Key)
            {
                case Key.Left:
                    if (col > 0) NavigateTo(row, col - 1, shift);
                    e.Handled = true;
                    break;
                case Key.Right:
                    if (col < (_sheet?.TotalColumnCount ?? 16384) - 1) NavigateTo(row, col + 1, shift);
                    e.Handled = true;
                    break;
                case Key.Up:
                    if (row > 0) NavigateTo(row - 1, col, shift);
                    e.Handled = true;
                    break;
                case Key.Down:
                    if (row < (_sheet?.TotalRowCount ?? 1000000) - 1) NavigateTo(row + 1, col, shift);
                    e.Handled = true;
                    break;
                case Key.Tab:
                    NavigateTo(row, col + 1, false);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    NavigateTo(row + 1, col, false);
                    e.Handled = true;
                    break;
                case Key.F2:
                    BeginEdit();
                    e.Handled = true;
                    break;
                case Key.Delete:
                    _sheet?.ClearRange(_selection.SelectionRange, clearValues: true, clearFormats: false);
                    e.Handled = true;
                    break;
                case Key.A when ctrl:
                    _selection.SelectAll(_sheet?.MaxOccupiedRow ?? 100, _sheet?.MaxOccupiedColumn ?? 25);
                    e.Handled = true;
                    break;
                case Key.C when ctrl:
                    CopySelectionToClipboard();
                    e.Handled = true;
                    break;
                case Key.V when ctrl:
                    PasteFromClipboard();
                    e.Handled = true;
                    break;
                default:
                    // If user types a visible character, begin edit immediately with that char
                    if (!ctrl && e.Key >= Key.A && e.Key <= Key.Z)
                    {
                        BeginEdit(e.Key.ToString());
                        e.Handled = true;
                    }
                    else if (!ctrl && e.Key >= Key.D0 && e.Key <= Key.D9)
                    {
                        BeginEdit(((int)e.Key - (int)Key.D0).ToString());
                        e.Handled = true;
                    }
                    else if (!ctrl && e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                    {
                        BeginEdit(((int)e.Key - (int)Key.NumPad0).ToString());
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void NavigateTo(int row, int col, bool extend)
        {
            if (extend)
            {
                _selection.ExtendSelection(row, col);
            }
            else
            {
                _selection.SelectCell(row, col);
            }
            EnsureCellVisible(row, col);
        }

        public void EnsureCellVisible(int row, int col)
        {
            if (_sheet == null || _horizontalScrollBar == null || _verticalScrollBar == null) return;

            int scrollRow = (int)_verticalScrollBar.Value;
            int scrollCol = (int)_horizontalScrollBar.Value;
            int visibleRows = (int)_verticalScrollBar.ViewportSize;
            int visibleCols = (int)_horizontalScrollBar.ViewportSize;

            if (row < scrollRow)
            {
                _verticalScrollBar.Value = row;
            }
            else if (row >= scrollRow + visibleRows - 1)
            {
                _verticalScrollBar.Value = Math.Max(0, row - visibleRows + 2);
            }

            if (col < scrollCol)
            {
                _horizontalScrollBar.Value = col;
            }
            else if (col >= scrollCol + visibleCols - 1)
            {
                _horizontalScrollBar.Value = Math.Max(0, col - visibleCols + 2);
            }

            InvalidateVisual();
        }

        // Mouse Handling for Selecting & Resizing
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (_sheet == null || _layoutManager == null) return;

            Point pos = e.GetPosition(this);
            double headerW = _layoutManager.HeaderWidth;
            double headerH = _layoutManager.HeaderHeight;

            int scrollRow = (int)(_verticalScrollBar?.Value ?? 0);
            int scrollCol = (int)(_horizontalScrollBar?.Value ?? 0);

            // 1. Clicked on Column Header
            if (pos.Y <= headerH && pos.X > headerW)
            {
                if (CheckColumnResizeStart(pos, headerW, scrollCol))
                {
                    return;
                }

                // Header select column
                int col = scrollCol + (int)((pos.X - headerW) / _sheet.DefaultColumnWidth);
                _selection.SelectColumn(col, Math.Max(_sheet.MaxOccupiedRow, 100));
                return;
            }

            // 2. Clicked on Row Header
            if (pos.X <= headerW && pos.Y > headerH)
            {
                if (CheckRowResizeStart(pos, headerH, scrollRow))
                {
                    return;
                }

                // Header select row
                int row = scrollRow + (int)((pos.Y - headerH) / _sheet.DefaultRowHeight);
                _selection.SelectRow(row, Math.Max(_sheet.MaxOccupiedColumn, 25));
                return;
            }

            // 3. Clicked on Top-Left Corner
            if (pos.X <= headerW && pos.Y <= headerH)
            {
                _selection.SelectAll(Math.Max(_sheet.MaxOccupiedRow, 100), Math.Max(_sheet.MaxOccupiedColumn, 25));
                return;
            }

            // 4. Clicked inside cell area
            if (pos.X > headerW && pos.Y > headerH)
            {
                int col = scrollCol + (int)((pos.X - headerW) / _sheet.DefaultColumnWidth);
                int row = scrollRow + (int)((pos.Y - headerH) / _sheet.DefaultRowHeight);

                if (e.ClickCount == 2)
                {
                    BeginEdit();
                }
                else
                {
                    if (_isEditing) CommitEdit();

                    bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                    if (shift)
                    {
                        _selection.ExtendSelection(row, col);
                    }
                    else
                    {
                        _selection.SelectCell(row, col);
                    }
                    _isDraggingSelection = true;
                    CaptureMouse();
                }
            }
        }

        private bool CheckColumnResizeStart(Point pos, double headerW, int scrollCol)
        {
            if (_sheet == null) return false;
            double x = headerW;
            int c = scrollCol;
            while (x < ActualWidth)
            {
                double w = _sheet.GetColumnWidth(c);
                double edgeX = x + w;
                if (Math.Abs(pos.X - edgeX) <= 4)
                {
                    _isResizingColumn = true;
                    _resizingColIndex = c;
                    _resizeStartPos = pos.X;
                    _resizeOriginalSize = w;
                    CaptureMouse();
                    return true;
                }
                x += w;
                c++;
            }
            return false;
        }

        private bool CheckRowResizeStart(Point pos, double headerH, int scrollRow)
        {
            if (_sheet == null) return false;
            double y = headerH;
            int r = scrollRow;
            while (y < ActualHeight)
            {
                double h = _sheet.GetRowHeight(r);
                double edgeY = y + h;
                if (Math.Abs(pos.Y - edgeY) <= 4)
                {
                    _isResizingRow = true;
                    _resizingRowIndex = r;
                    _resizeStartPos = pos.Y;
                    _resizeOriginalSize = h;
                    CaptureMouse();
                    return true;
                }
                y += h;
                r++;
            }
            return false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_sheet == null || _layoutManager == null) return;

            Point pos = e.GetPosition(this);
            double headerW = _layoutManager.HeaderWidth;
            double headerH = _layoutManager.HeaderHeight;

            int scrollRow = (int)(_verticalScrollBar?.Value ?? 0);
            int scrollCol = (int)(_horizontalScrollBar?.Value ?? 0);

            // Handle column resize
            if (_isResizingColumn)
            {
                double delta = pos.X - _resizeStartPos;
                double newWidth = Math.Max(20, _resizeOriginalSize + delta);
                _sheet.SetColumnWidth(_resizingColIndex, newWidth);
                InvalidateVisual();
                return;
            }

            // Handle row resize
            if (_isResizingRow)
            {
                double delta = pos.Y - _resizeStartPos;
                double newHeight = Math.Max(15, _resizeOriginalSize + delta);
                _sheet.SetRowHeight(_resizingRowIndex, newHeight);
                InvalidateVisual();
                return;
            }

            // Handle drag selection
            if (_isDraggingSelection)
            {
                int col = scrollCol + (int)Math.Max(0, (pos.X - headerW) / _sheet.DefaultColumnWidth);
                int row = scrollRow + (int)Math.Max(0, (pos.Y - headerH) / _sheet.DefaultRowHeight);
                _selection.ExtendSelection(row, col);
                return;
            }

            // Update Cursors on Header edges
            if (pos.Y <= headerH && pos.X > headerW)
            {
                // Check if hovering column resize edge
                double x = headerW;
                int c = scrollCol;
                bool resizeCursor = false;
                while (x < ActualWidth)
                {
                    double w = _sheet.GetColumnWidth(c);
                    double edgeX = x + w;
                    if (Math.Abs(pos.X - edgeX) <= 4)
                    {
                        Cursor = Cursors.SizeWE;
                        resizeCursor = true;
                        break;
                    }
                    x += w;
                    c++;
                }
                if (!resizeCursor) Cursor = Cursors.Arrow;
            }
            else if (pos.X <= headerW && pos.Y > headerH)
            {
                // Check if hovering row resize edge
                double y = headerH;
                int r = scrollRow;
                bool resizeCursor = false;
                while (y < ActualHeight)
                {
                    double h = _sheet.GetRowHeight(r);
                    double edgeY = y + h;
                    if (Math.Abs(pos.Y - edgeY) <= 4)
                    {
                        Cursor = Cursors.SizeNS;
                        resizeCursor = true;
                        break;
                    }
                    y += h;
                    r++;
                }
                if (!resizeCursor) Cursor = Cursors.Arrow;
            }
            else
            {
                Cursor = Cursors.Arrow;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (_isResizingColumn || _isResizingRow || _isDraggingSelection)
            {
                _isResizingColumn = false;
                _isResizingRow = false;
                _isDraggingSelection = false;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_verticalScrollBar != null)
            {
                int delta = e.Delta > 0 ? -3 : 3;
                _verticalScrollBar.Value = Math.Max(0, Math.Min(_verticalScrollBar.Maximum, _verticalScrollBar.Value + delta));
                InvalidateVisual();
            }
        }

        // In-Place Cell Editing
        public void BeginEdit(string? initialText = null)
        {
            if (_sheet == null || _editorTextBox == null) return;

            var active = _selection.ActiveCell;
            var cell = _sheet.GetCell(active.Row, active.Column);

            _isEditing = true;
            _editorTextBox.Visibility = Visibility.Visible;
            _editorTextBox.Text = initialText ?? (cell?.Value?.ToString() ?? string.Empty);
            _editorTextBox.SelectAll();
            _editorTextBox.Focus();

            InvalidateArrange();
        }

        public void CommitEdit()
        {
            if (!_isEditing || _sheet == null || _editorTextBox == null) return;

            string text = _editorTextBox.Text;
            var active = _selection.ActiveCell;

            // Automatically recognize numbers
            if (double.TryParse(text, out double dVal))
            {
                _sheet.SetCellValue(active.Row, active.Column, dVal);
            }
            else
            {
                _sheet.SetCellValue(active.Row, active.Column, string.IsNullOrEmpty(text) ? null : text);
            }

            _isEditing = false;
            _editorTextBox.Visibility = Visibility.Collapsed;
            Focus();
            EditCommitted?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        public void CancelEdit()
        {
            if (!_isEditing || _editorTextBox == null) return;
            _isEditing = false;
            _editorTextBox.Visibility = Visibility.Collapsed;
            Focus();
            InvalidateVisual();
        }

        public void CopySelectionToClipboard()
        {
            if (_sheet == null) return;
            string text = _clipboardService.CopyRangeToText(_sheet, _selection.SelectionRange);
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }

        public void PasteFromClipboard()
        {
            if (_sheet == null) return;
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                var range = _clipboardService.PasteTextToSheet(_sheet, _selection.ActiveCell, text);
                _selection.ExtendSelection(range.Bottom, range.Right);
                InvalidateVisual();
            }
        }

        private void EditorTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitEdit();
                NavigateTo(_selection.ActiveCell.Row + 1, _selection.ActiveCell.Column, false);
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                CommitEdit();
                NavigateTo(_selection.ActiveCell.Row, _selection.ActiveCell.Column + 1, false);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
        }

        private void EditorTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isEditing)
            {
                CommitEdit();
            }
        }
    }
}
