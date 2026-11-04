using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using AlaaRep.Takeoff.Core.Models;
using AlaaRep.Takeoff.Core.Parsers;
using AlaaRep.Takeoff.Core.Services;

namespace AlaaRep.Takeoff.Wpf;

public partial class MainWindow : Window
{
    private DrawingDocument _doc = new();
    private readonly TakeoffEngine _engine = new();
    private readonly PdfDrawingReader _pdfReader = new();
    private readonly DxfDrawingReader _dxfReader = new();
    private readonly BluebeamMarkupReader _bluebeamReader = new();

    // Tools: Select, Count, Length, Area, Zone, Calibrate
    private string _currentTool = "Select";
    private TakeoffObject? _selectedObject;
    private Zone? _selectedZone;
    private bool _showZones = true;

    // Viewport Panning & Zooming
    private bool _isPanning;
    private System.Windows.Point _panStart;

    // Interactive Object Moving & Vertex Dragging
    private bool _isDraggingObject;
    private int _dragVertexIndex = -1;
    private Point2D? _dragStartDrawPt;

    // Clipboard for Copy / Paste
    private TakeoffObject? _clipboardObject;

    // In-progress drawing points (drawing-space coordinates)
    private readonly List<Point2D> _drawingPoints = new();
    private readonly List<Point2D> _calibrationPoints = new();
    private System.Windows.Point _currentMousePos;
    private SnapResult _currentSnap = new();

    // Observable collections for UI bindings
    public ObservableCollection<BimKeyValue> BimPropertiesList { get; } = new();
    public ObservableCollection<QtoDisplayRow> QtoRows { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DgBimProperties.ItemsSource = BimPropertiesList;
        DgQtoReport.ItemsSource = QtoRows;
        LoadDefaultSample();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ZoomFit();
    }

    private void LoadDefaultSample()
    {
        _doc = new DrawingDocument
        {
            FileName = "Sample Architectural Floor Plan",
            FileType = "CAD",
            Width = 1400,
            Height = 900,
            Calibration = new ScaleCalibration
            {
                ScaleLabel = "1:100 (Metric)",
                CalibrationPixelDistance = 100.0,
                CalibrationRealDistance = 5.0,
                LinearUnit = "m",
                AreaUnit = "m²"
            }
        };

        SeedSampleLayout(_doc);
        UpdateUiFromDoc();
        ZoomFit();
    }

    private void SeedSampleLayout(DrawingDocument doc)
    {
        // Perimeter wall
        doc.Entities.Add(new DrawingEntity
        {
            Type = DrawingEntityType.Polygon,
            Layer = "A-WALL-EXTR",
            ColorHex = "#94A3B8",
            StrokeWidth = 3,
            Points = new List<Point2D> { new(100, 100), new(1100, 100), new(1100, 700), new(100, 700) }
        });

        // Interior wall partitions
        doc.Entities.Add(new DrawingEntity { Type = DrawingEntityType.Line, Layer = "A-WALL-INTR", ColorHex = "#64748B", StrokeWidth = 2, Points = new List<Point2D> { new(500, 100), new(500, 700) } });
        doc.Entities.Add(new DrawingEntity { Type = DrawingEntityType.Line, Layer = "A-WALL-INTR", ColorHex = "#64748B", StrokeWidth = 2, Points = new List<Point2D> { new(100, 400), new(500, 400) } });
        doc.Entities.Add(new DrawingEntity { Type = DrawingEntityType.Line, Layer = "A-WALL-INTR", ColorHex = "#64748B", StrokeWidth = 2, Points = new List<Point2D> { new(500, 450), new(1100, 450) } });

        // Room Text Labels
        doc.Entities.Add(new DrawingEntity { Type = DrawingEntityType.Text, Text = "ROOM 101 - EXEC OFFICE", TextSize = 14, Points = new List<Point2D> { new(150, 150) }, ColorHex = "#94A3B8" });
        doc.Entities.Add(new DrawingEntity { Type = DrawingEntityType.Text, Text = "ROOM 102 - MEETING ROOM", TextSize = 14, Points = new List<Point2D> { new(150, 450) }, ColorHex = "#94A3B8" });
        doc.Entities.Add(new DrawingEntity { Type = DrawingEntityType.Text, Text = "ZONE C - CONFERENCE", TextSize = 14, Points = new List<Point2D> { new(550, 150) }, ColorHex = "#94A3B8" });
        doc.Entities.Add(new DrawingEntity { Type = DrawingEntityType.Text, Text = "ZONE D - OPEN WORKSPACE", TextSize = 14, Points = new List<Point2D> { new(550, 500) }, ColorHex = "#94A3B8" });

        // Specific Zones
        doc.Zones.Add(new Zone
        {
            Id = "zone-101",
            Name = "Executive Office (Room 101)",
            Code = "ZN-101",
            ColorHex = "#10B981",
            Opacity = 0.22,
            Boundary = new List<Point2D> { new(100, 100), new(500, 100), new(500, 400), new(100, 400) }
        });
        doc.Zones.Add(new Zone
        {
            Id = "zone-102",
            Name = "Meeting Room (Room 102)",
            Code = "ZN-102",
            ColorHex = "#3B82F6",
            Opacity = 0.22,
            Boundary = new List<Point2D> { new(100, 400), new(500, 400), new(500, 700), new(100, 700) }
        });
        doc.Zones.Add(new Zone
        {
            Id = "zone-103",
            Name = "Conference Hall (Zone C)",
            Code = "ZN-103",
            ColorHex = "#F59E0B",
            Opacity = 0.22,
            Boundary = new List<Point2D> { new(500, 100), new(1100, 100), new(1100, 450), new(500, 450) }
        });
        doc.Zones.Add(new Zone
        {
            Id = "zone-104",
            Name = "Open Workspace (Zone D)",
            Code = "ZN-104",
            ColorHex = "#8B5CF6",
            Opacity = 0.22,
            Boundary = new List<Point2D> { new(500, 450), new(1100, 450), new(1100, 700), new(500, 700) }
        });

        // Takeoff Objects
        var oakFloor = new TakeoffObject
        {
            Id = "obj-floor-1",
            Name = "Engineered Oak Flooring",
            Category = "Finishes",
            MeasurementType = TakeoffMeasurementType.Area,
            ColorHex = "#D97706",
            GeometryPoints = new List<Point2D> { new(120, 120), new(480, 120), new(480, 380), new(120, 380) }
        };
        oakFloor.SetBimProperty("Family", "Floor:Wood Finish");
        oakFloor.SetBimProperty("Omniclass", "23-14 17 00 Flooring");
        oakFloor.SetBimProperty("Material", "Engineered White Oak");
        oakFloor.SetBimProperty("UnitCost", "45.00");
        doc.TakeoffObjects.Add(oakFloor);

        var wallPartition = new TakeoffObject
        {
            Id = "obj-wall-1",
            Name = "Drywall Partition STC 52",
            Category = "Partitions",
            MeasurementType = TakeoffMeasurementType.Length,
            ColorHex = "#EF4444",
            GeometryPoints = new List<Point2D> { new(150, 400), new(450, 400), new(750, 450), new(1050, 450) }
        };
        wallPartition.SetBimProperty("Family", "Wall:Basic Wall Interior");
        wallPartition.SetBimProperty("Omniclass", "23-13 25 11 Drywall");
        wallPartition.SetBimProperty("UnitCost", "65.00");
        doc.TakeoffObjects.Add(wallPartition);

        var sprinklers = new TakeoffObject
        {
            Id = "obj-sprinklers",
            Name = "Pendant Sprinkler Head",
            Category = "Fire Protection",
            MeasurementType = TakeoffMeasurementType.Count,
            ColorHex = "#DC2626",
            GeometryPoints = new List<Point2D>
            {
                new(200, 200), new(350, 250), new(200, 500), new(350, 580),
                new(650, 220), new(900, 220), new(650, 580), new(800, 580), new(950, 580)
            }
        };
        sprinklers.SetBimProperty("Family", "FireProtection:Sprinkler");
        sprinklers.SetBimProperty("Omniclass", "23-35 15 11");
        sprinklers.SetBimProperty("UnitCost", "120.00");
        doc.TakeoffObjects.Add(sprinklers);
    }

    private void UpdateUiFromDoc()
    {
        TxtScaleHud.Text = $"{_doc.Calibration.ScaleLabel} ({_doc.Calibration.CalibrationPixelDistance:F0}px = {_doc.Calibration.CalibrationRealDistance:F1}{_doc.Calibration.LinearUnit})";
        LbTakeoffObjects.ItemsSource = null;
        LbTakeoffObjects.ItemsSource = _doc.TakeoffObjects;

        LbZones.ItemsSource = null;
        LbZones.ItemsSource = _doc.Zones;

        CalculateAndDisplayQto();
        RedrawCanvas();
    }

    // Precise coordinate conversion: screen pixel point to drawing coordinates
    private Point2D ScreenToDrawing(System.Windows.Point screenPoint)
    {
        // Viewport transform: Screen = Drawing * Scale + Translate
        // Inverse transform: Drawing = (Screen - Translate) / Scale
        return SpatialCalculator.ScreenToDrawing(
            new Point2D(screenPoint.X, screenPoint.Y),
            CanvasTranslate.X,
            CanvasTranslate.Y,
            CanvasScale.ScaleX);
    }

    private void RedrawCanvas()
    {
        if (FastCanvas == null) return;

        using (var dc = FastCanvas.RenderOpen())
        {
            // 1. Draw CAD / PDF Vector Entities directly via DrawingContext
            foreach (var entity in _doc.Entities)
            {
                DrawEntityDirect(dc, entity);
            }

            // 2. Draw Specific Zones
            if (_showZones)
            {
                foreach (var zone in _doc.Zones)
                {
                    DrawZoneDirect(dc, zone);
                }
            }

            // 3. Draw Takeoff Objects
            foreach (var obj in _doc.TakeoffObjects)
            {
                DrawTakeoffObjectDirect(dc, obj);
            }
        }

        RedrawPreview();
    }

    private void RedrawPreview()
    {
        if (PreviewCanvas == null) return;
        PreviewCanvas.Children.Clear();

        if (_drawingPoints.Count > 0)
        {
            DrawActivePoly();
        }

        if (_calibrationPoints.Count > 0)
        {
            DrawCalibrationLine();
        }

        // Draw Interactive Vertex Handles for Selected Object
        if (_selectedObject != null && _selectedObject.GeometryPoints.Count > 0)
        {
            DrawSelectionHandles(_selectedObject);
        }

        // Draw Snapping Glyph (Orange square for Endpoint, Green triangle for Midpoint, Blue hourglass for Nearest)
        if (_currentSnap != null && _currentSnap.DidSnap)
        {
            DrawSnapGlyph(_currentSnap);
        }
    }

    private void DrawSelectionHandles(TakeoffObject obj)
    {
        for (int i = 0; i < obj.GeometryPoints.Count; i++)
        {
            var p = obj.GeometryPoints[i];
            var handle = new Rectangle
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)), // Blue
                StrokeThickness = 2
            };
            Canvas.SetLeft(handle, p.X - 5);
            Canvas.SetTop(handle, p.Y - 5);
            PreviewCanvas.Children.Add(handle);
        }
    }

    private void DrawSnapGlyph(SnapResult snap)
    {
        var pt = snap.SnappedPoint;
        if (snap.Type == SnapType.Endpoint)
        {
            var rect = new Rectangle
            {
                Width = 12,
                Height = 12,
                Stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(90, 245, 158, 11))
            };
            Canvas.SetLeft(rect, pt.X - 6);
            Canvas.SetTop(rect, pt.Y - 6);
            PreviewCanvas.Children.Add(rect);
        }
        else if (snap.Type == SnapType.Midpoint)
        {
            var tri = new Polygon
            {
                Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Emerald
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(90, 16, 185, 129))
            };
            tri.Points.Add(new System.Windows.Point(pt.X, pt.Y - 7));
            tri.Points.Add(new System.Windows.Point(pt.X - 6, pt.Y + 5));
            tri.Points.Add(new System.Windows.Point(pt.X + 6, pt.Y + 5));
            PreviewCanvas.Children.Add(tri);
        }
        else if (snap.Type == SnapType.Nearest)
        {
            var circle = new Ellipse
            {
                Width = 10,
                Height = 10,
                Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)), // Cyan
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(circle, pt.X - 5);
            Canvas.SetTop(circle, pt.Y - 5);
            PreviewCanvas.Children.Add(circle);
        }
    }

    private void DrawEntityDirect(DrawingContext dc, DrawingEntity ent)
    {
        if (ent.Points.Count == 0) return;
        var brush = ParseColor(ent.ColorHex ?? "#64748B");
        var pen = new Pen(brush, Math.Max(0.75, ent.StrokeWidth));
        pen.Freeze();

        if (ent.Type == DrawingEntityType.Text)
        {
            var formattedText = new FormattedText(
                ent.Text ?? "",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                Math.Max(10, ent.TextSize),
                brush,
                1.0);
            dc.DrawText(formattedText, new System.Windows.Point(ent.Points[0].X, ent.Points[0].Y));
        }
        else if (ent.Type == DrawingEntityType.Circle)
        {
            dc.DrawEllipse(null, pen, new System.Windows.Point(ent.Points[0].X, ent.Points[0].Y), ent.Radius, ent.Radius);
        }
        else
        {
            var streamGeometry = new StreamGeometry();
            using (var ctx = streamGeometry.Open())
            {
                ctx.BeginFigure(new System.Windows.Point(ent.Points[0].X, ent.Points[0].Y), ent.IsClosed || ent.Type == DrawingEntityType.Polygon, ent.IsClosed || ent.Type == DrawingEntityType.Polygon);
                for (int i = 1; i < ent.Points.Count; i++)
                {
                    ctx.LineTo(new System.Windows.Point(ent.Points[i].X, ent.Points[i].Y), true, false);
                }
            }
            streamGeometry.Freeze();
            dc.DrawGeometry(null, pen, streamGeometry);
        }
    }

    private void DrawZoneDirect(DrawingContext dc, Zone zone)
    {
        if (zone.Boundary.Count < 3) return;

        bool isSelected = (_selectedZone?.Id == zone.Id);
        var baseColor = ParseColorValue(zone.ColorHex ?? "#10B981");
        var fillBrush = new SolidColorBrush(Color.FromArgb((byte)(zone.Opacity * 255), baseColor.R, baseColor.G, baseColor.B));
        fillBrush.Freeze();
        var strokeBrush = new SolidColorBrush(baseColor);
        strokeBrush.Freeze();
        var pen = new Pen(strokeBrush, isSelected ? 3 : 1.5);
        if (isSelected) pen.DashStyle = DashStyles.Dash;
        pen.Freeze();

        var streamGeometry = new StreamGeometry();
        using (var ctx = streamGeometry.Open())
        {
            ctx.BeginFigure(new System.Windows.Point(zone.Boundary[0].X, zone.Boundary[0].Y), true, true);
            for (int i = 1; i < zone.Boundary.Count; i++)
            {
                ctx.LineTo(new System.Windows.Point(zone.Boundary[i].X, zone.Boundary[i].Y), true, false);
            }
        }
        streamGeometry.Freeze();
        dc.DrawGeometry(fillBrush, pen, streamGeometry);

        var centroid = SpatialCalculator.ComputeCentroid(zone.Boundary);
        var formattedText = new FormattedText(
            $"{zone.Name} ({zone.Code})",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            12,
            Brushes.White,
            1.0);
        dc.DrawText(formattedText, new System.Windows.Point(centroid.X - 40, centroid.Y - 8));
    }

    private void DrawTakeoffObjectDirect(DrawingContext dc, TakeoffObject obj)
    {
        if (obj.GeometryPoints.Count == 0) return;

        bool isSelected = (_selectedObject?.Id == obj.Id);
        var baseColor = ParseColorValue(obj.ColorHex ?? "#3B82F6");
        var fillBrush = new SolidColorBrush(Color.FromArgb((byte)(obj.Opacity * 255), baseColor.R, baseColor.G, baseColor.B));
        fillBrush.Freeze();
        var strokeBrush = new SolidColorBrush(baseColor);
        strokeBrush.Freeze();
        var pen = new Pen(strokeBrush, isSelected ? 3.5 : obj.StrokeWidth);
        pen.Freeze();

        if (obj.MeasurementType == TakeoffMeasurementType.Count)
        {
            var whitePen = new Pen(Brushes.White, 2);
            whitePen.Freeze();
            foreach (var pt in obj.GeometryPoints)
            {
                dc.DrawEllipse(strokeBrush, whitePen, new System.Windows.Point(pt.X, pt.Y), 7, 7);
            }
        }
        else if (obj.MeasurementType == TakeoffMeasurementType.Length)
        {
            var streamGeometry = new StreamGeometry();
            using (var ctx = streamGeometry.Open())
            {
                ctx.BeginFigure(new System.Windows.Point(obj.GeometryPoints[0].X, obj.GeometryPoints[0].Y), false, false);
                for (int i = 1; i < obj.GeometryPoints.Count; i++)
                {
                    ctx.LineTo(new System.Windows.Point(obj.GeometryPoints[i].X, obj.GeometryPoints[i].Y), true, false);
                }
            }
            streamGeometry.Freeze();
            dc.DrawGeometry(null, pen, streamGeometry);

            foreach (var pt in obj.GeometryPoints)
            {
                dc.DrawEllipse(Brushes.White, null, new System.Windows.Point(pt.X, pt.Y), 3, 3);
            }
        }
        else // Area or Perimeter
        {
            var streamGeometry = new StreamGeometry();
            using (var ctx = streamGeometry.Open())
            {
                ctx.BeginFigure(new System.Windows.Point(obj.GeometryPoints[0].X, obj.GeometryPoints[0].Y), true, true);
                for (int i = 1; i < obj.GeometryPoints.Count; i++)
                {
                    ctx.LineTo(new System.Windows.Point(obj.GeometryPoints[i].X, obj.GeometryPoints[i].Y), true, false);
                }
            }
            streamGeometry.Freeze();
            dc.DrawGeometry(fillBrush, pen, streamGeometry);
        }
    }

    private void DrawActivePoly()
    {
        var stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        var fill = new SolidColorBrush(Color.FromArgb(60, 245, 158, 11));
        var cur = ScreenToDrawing(_currentMousePos);

        if (_currentTool == "Area" || _currentTool == "Zone")
        {
            var poly = new Polygon { Stroke = stroke, StrokeThickness = 2, Fill = fill };
            foreach (var p in _drawingPoints) poly.Points.Add(new System.Windows.Point(p.X, p.Y));
            poly.Points.Add(new System.Windows.Point(cur.X, cur.Y));
            PreviewCanvas.Children.Add(poly);
        }
        else if (_currentTool == "Length")
        {
            var polyline = new Polyline { Stroke = stroke, StrokeThickness = 2 };
            foreach (var p in _drawingPoints) polyline.Points.Add(new System.Windows.Point(p.X, p.Y));
            polyline.Points.Add(new System.Windows.Point(cur.X, cur.Y));
            PreviewCanvas.Children.Add(polyline);
        }

        for (int i = 0; i < _drawingPoints.Count; i++)
        {
            var p = _drawingPoints[i];
            var dot = new Ellipse { Width = 8, Height = 8, Fill = stroke, Stroke = Brushes.White, StrokeThickness = 1.5 };
            Canvas.SetLeft(dot, p.X - 4);
            Canvas.SetTop(dot, p.Y - 4);
            PreviewCanvas.Children.Add(dot);

            // Point number badge
            var badge = new TextBlock
            {
                Text = (i + 1).ToString(),
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(badge, p.X + 6);
            Canvas.SetTop(badge, p.Y - 12);
            PreviewCanvas.Children.Add(badge);
        }
    }

    private void DrawCalibrationLine()
    {
        if (_calibrationPoints.Count == 0) return;
        var cur = (_calibrationPoints.Count > 1) ? _calibrationPoints[1] : ScreenToDrawing(_currentMousePos);
        var line = new Line
        {
            X1 = _calibrationPoints[0].X,
            Y1 = _calibrationPoints[0].Y,
            X2 = cur.X,
            Y2 = cur.Y,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 3 }
        };
        PreviewCanvas.Children.Add(line);
    }

    // Viewport Mouse Events with Accurate Transformation
    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var screenPt = e.GetPosition(ViewportGrid);
        var rawDrawPt = ScreenToDrawing(screenPt);
        var snap = SpatialCalculator.FindSnapPoint(rawDrawPt, _doc, 15.0 / CanvasScale.ScaleX);
        var drawPt = snap.DidSnap ? snap.SnappedPoint : rawDrawPt;

        // If drawing a shape (Length, Area, Zone), Right-Click finishes and closes the operation
        if (e.RightButton == MouseButtonState.Pressed)
        {
            if ((_currentTool == "Length" || _currentTool == "Area" || _currentTool == "Zone") && _drawingPoints.Count >= 2)
            {
                FinishCurrentShape();
                e.Handled = true;
                return;
            }

            // Otherwise right-click initiates viewport pan
            _isPanning = true;
            _panStart = screenPt;
            ViewportGrid.CaptureMouse();
            return;
        }

        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _isPanning = true;
            _panStart = screenPt;
            ViewportGrid.CaptureMouse();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            // Reliable double-click detection without rejecting distinct second points
            bool isDblClick = (e.ClickCount >= 2);

            if (isDblClick && (_currentTool == "Length" || _currentTool == "Area" || _currentTool == "Zone"))
            {
                FinishCurrentShape();
                return;
            }

            switch (_currentTool)
            {
                case "Select":
                    // Check if clicked an existing handle of the selected object for vertex editing
                    if (_selectedObject != null)
                    {
                        double handleRadius = 12.0 / CanvasScale.ScaleX;
                        int hitIdx = -1;
                        for (int i = 0; i < _selectedObject.GeometryPoints.Count; i++)
                        {
                            if (_selectedObject.GeometryPoints[i].DistanceTo(drawPt) <= handleRadius)
                            {
                                hitIdx = i;
                                break;
                            }
                        }

                        if (hitIdx >= 0)
                        {
                            _isDraggingObject = true;
                            _dragVertexIndex = hitIdx;
                            _dragStartDrawPt = drawPt;
                            ViewportGrid.CaptureMouse();
                            return;
                        }
                    }

                    PickObjectOrZone(drawPt);

                    // If an object is selected and user clicked on it, prepare to move it
                    if (_selectedObject != null)
                    {
                        _isDraggingObject = true;
                        _dragVertexIndex = -1; // -1 means moving whole object
                        _dragStartDrawPt = drawPt;
                        ViewportGrid.CaptureMouse();
                    }
                    break;

                case "Count":
                    CreateCountObject(drawPt);
                    break;

                case "Length":
                case "Area":
                case "Zone":
                    // Every left-click unconditionally records a point
                    _drawingPoints.Add(drawPt);
                    if (BtnFinishShapeHud != null && _drawingPoints.Count >= 2)
                    {
                        BtnFinishShapeHud.Visibility = Visibility.Visible;
                    }
                    TxtStatus.Text = $"{_currentTool} point {_drawingPoints.Count} added at ({drawPt.X:F1}, {drawPt.Y:F1}). Double-click or Right-click or press Enter to close.";
                    RedrawPreview();
                    break;

                case "Calibrate":
                    _calibrationPoints.Add(drawPt);
                    if (_calibrationPoints.Count == 2)
                    {
                        PromptScaleCalibration();
                    }
                    RedrawPreview();
                    break;
            }
        }
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        _currentMousePos = e.GetPosition(ViewportGrid);
        var rawDrawPt = ScreenToDrawing(_currentMousePos);

        if (_isPanning)
        {
            double dx = _currentMousePos.X - _panStart.X;
            double dy = _currentMousePos.Y - _panStart.Y;
            CanvasTranslate.X += dx;
            CanvasTranslate.Y += dy;
            _panStart = _currentMousePos;
            return;
        }

        // Handle Object Moving or Vertex Dragging
        if (_isDraggingObject && _selectedObject != null && _dragStartDrawPt != null)
        {
            double deltaX = rawDrawPt.X - _dragStartDrawPt.X;
            double deltaY = rawDrawPt.Y - _dragStartDrawPt.Y;

            if (_dragVertexIndex >= 0 && _dragVertexIndex < _selectedObject.GeometryPoints.Count)
            {
                // Drag single vertex
                _selectedObject.GeometryPoints[_dragVertexIndex] = rawDrawPt;
            }
            else
            {
                // Move entire object
                _selectedObject.MoveBy(deltaX, deltaY);
                _dragStartDrawPt = rawDrawPt;
            }

            SelectObject(_selectedObject);
            RedrawCanvas();
            return;
        }

        // Calculate dynamic Object Snap (OSnap)
        double snapTolerance = 15.0 / CanvasScale.ScaleX;
        _currentSnap = SpatialCalculator.FindSnapPoint(rawDrawPt, _doc, snapTolerance);

        var effectivePt = _currentSnap.DidSnap ? _currentSnap.SnappedPoint : rawDrawPt;
        TxtCursorCoords.Text = $"X: {effectivePt.X:F1}, Y: {effectivePt.Y:F1} (px)";
        TxtSnapHud.Text = _currentSnap.DidSnap ? $"[SNAP: {_currentSnap.Type}] {_currentSnap.Description}" : "";

        // Redraw preview layer with rubberband and snap glyph
        if (_drawingPoints.Count > 0 || _calibrationPoints.Count > 0 || _currentSnap.DidSnap)
        {
            RedrawPreview();
        }
    }

    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ViewportGrid.ReleaseMouseCapture();
        }

        if (_isDraggingObject)
        {
            _isDraggingObject = false;
            _dragVertexIndex = -1;
            _dragStartDrawPt = null;
            ViewportGrid.ReleaseMouseCapture();
            CalculateAndDisplayQto();
        }
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var screenPt = e.GetPosition(ViewportGrid);
        double zoomFactor = e.Delta > 0 ? 1.15 : 0.85;

        double oldScale = CanvasScale.ScaleX;
        double newScale = Math.Clamp(oldScale * zoomFactor, 0.05, 20.0);

        // Zoom relative to mouse cursor
        CanvasTranslate.X = screenPt.X - (screenPt.X - CanvasTranslate.X) * (newScale / oldScale);
        CanvasTranslate.Y = screenPt.Y - (screenPt.Y - CanvasTranslate.Y) * (newScale / oldScale);
        CanvasScale.ScaleX = newScale;
        CanvasScale.ScaleY = newScale;

        RedrawCanvas();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if ((_currentTool == "Length" || _currentTool == "Area" || _currentTool == "Zone") && _drawingPoints.Count >= 2)
            {
                FinishCurrentShape();
            }
        }
        else if (e.Key == Key.Escape)
        {
            _drawingPoints.Clear();
            _calibrationPoints.Clear();
            RedrawPreview();
            SetTool("Select");
            TxtStatus.Text = "Drawing operation canceled.";
        }
        else if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            CopySelectedObject();
        }
        else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            PasteObject();
        }
        else if (e.Key == Key.Delete)
        {
            if (_selectedObject != null)
            {
                _doc.TakeoffObjects.Remove(_selectedObject);
                _selectedObject = null;
                ClearBimInspector();
                UpdateUiFromDoc();
            }
            else if (_selectedZone != null)
            {
                _doc.Zones.Remove(_selectedZone);
                _selectedZone = null;
                UpdateUiFromDoc();
            }
        }
    }

    private void CopySelectedObject()
    {
        if (_selectedObject != null)
        {
            _clipboardObject = _selectedObject.Clone(0);
            TxtStatus.Text = $"Copied: {_selectedObject.Name} to clipboard.";
        }
    }

    private void PasteObject()
    {
        if (_clipboardObject != null)
        {
            var pasted = _clipboardObject.Clone(30.0); // 30px offset
            _doc.TakeoffObjects.Add(pasted);
            SelectObject(pasted);
            UpdateUiFromDoc();
            TxtStatus.Text = $"Pasted: {pasted.Name}. You can drag it to position.";
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e) => CopySelectedObject();
    private void BtnPaste_Click(object sender, RoutedEventArgs e) => PasteObject();

    private void BtnFinishShapeHud_Click(object sender, RoutedEventArgs e)
    {
        FinishCurrentShape();
    }

    private void FinishCurrentShape()
    {
        if (_drawingPoints.Count < 2) return;
        var points = new List<Point2D>(_drawingPoints);
        _drawingPoints.Clear();
        if (BtnFinishShapeHud != null) BtnFinishShapeHud.Visibility = Visibility.Collapsed;
        RedrawPreview();

        if (_currentTool == "Length")
        {
            var wall = new TakeoffObject
            {
                Name = $"Linear Takeoff {_doc.TakeoffObjects.Count + 1}",
                Category = "Partitions",
                MeasurementType = TakeoffMeasurementType.Length,
                ColorHex = "#EF4444",
                GeometryPoints = points
            };
            wall.SetBimProperty("Family", "Wall:Interior");
            wall.SetBimProperty("UnitCost", "50.00");
            _doc.TakeoffObjects.Add(wall);
            SelectObject(wall);
        }
        else if (_currentTool == "Area")
        {
            var area = new TakeoffObject
            {
                Name = $"Area Takeoff {_doc.TakeoffObjects.Count + 1}",
                Category = "Finishes",
                MeasurementType = TakeoffMeasurementType.Area,
                ColorHex = "#10B981",
                GeometryPoints = points
            };
            area.SetBimProperty("Family", "Floor:Tile");
            area.SetBimProperty("UnitCost", "40.00");
            _doc.TakeoffObjects.Add(area);
            SelectObject(area);
        }
        else if (_currentTool == "Zone")
        {
            int zoneNum = _doc.Zones.Count + 1;
            var zone = new Zone
            {
                Name = $"Zone {zoneNum}",
                Code = $"ZN-{zoneNum:00}",
                ColorHex = "#8B5CF6",
                Opacity = 0.22,
                Boundary = points
            };
            _doc.Zones.Add(zone);
            _selectedZone = zone;
        }

        SetTool("Select");
        UpdateUiFromDoc();
    }

    private void CreateCountObject(Point2D pt)
    {
        var countObj = new TakeoffObject
        {
            Name = $"Equipment #{_doc.TakeoffObjects.Count + 1}",
            Category = "Equipment",
            MeasurementType = TakeoffMeasurementType.Count,
            ColorHex = "#3B82F6",
            GeometryPoints = new List<Point2D> { pt }
        };
        countObj.SetBimProperty("Family", "Mechanical:Fixture");
        countObj.SetBimProperty("UnitCost", "150.00");
        _doc.TakeoffObjects.Add(countObj);

        SelectObject(countObj);
        UpdateUiFromDoc();
    }

    private void PickObjectOrZone(Point2D pt)
    {
        // 1. Hit test Takeoff Objects
        double scale = CanvasScale.ScaleX;
        double hitRadius = 15.0 / scale;

        foreach (var obj in _doc.TakeoffObjects.AsEnumerable().Reverse())
        {
            if (obj.MeasurementType == TakeoffMeasurementType.Count)
            {
                if (obj.GeometryPoints.Any(p => p.DistanceTo(pt) <= hitRadius))
                {
                    SelectObject(obj);
                    return;
                }
            }
            else if (obj.MeasurementType == TakeoffMeasurementType.Length)
            {
                for (int i = 0; i < obj.GeometryPoints.Count - 1; i++)
                {
                    if (SpatialCalculator.DistanceToSegment(pt, obj.GeometryPoints[i], obj.GeometryPoints[i + 1]) <= hitRadius)
                    {
                        SelectObject(obj);
                        return;
                    }
                }
            }
            else
            {
                if (SpatialCalculator.IsPointInPolygon(pt, obj.GeometryPoints))
                {
                    SelectObject(obj);
                    return;
                }
            }
        }

        // 2. Hit test Zones
        foreach (var zone in _doc.Zones.AsEnumerable().Reverse())
        {
            if (SpatialCalculator.IsPointInPolygon(pt, zone.Boundary))
            {
                _selectedZone = zone;
                _selectedObject = null;
                TxtStatus.Text = $"Selected Zone: {zone.Name} ({zone.Code})";
                RedrawCanvas();
                return;
            }
        }

        // Empty space clicked
        _selectedObject = null;
        _selectedZone = null;
        ClearBimInspector();
        RedrawCanvas();
    }

    private void SelectObject(TakeoffObject obj)
    {
        _selectedObject = obj;
        _selectedZone = null;

        TxtPropName.Text = obj.Name;
        TxtPropCategory.Text = obj.Category;
        TxtPropType.Text = obj.MeasurementType.ToString();

        double scale = _doc.Calibration.UnitsPerCoordinate;
        if (obj.MeasurementType == TakeoffMeasurementType.Count)
        {
            TxtPropQuantity.Text = $"{obj.GeometryPoints.Count} pcs";
        }
        else if (obj.MeasurementType == TakeoffMeasurementType.Length)
        {
            double len = SpatialCalculator.CalculatePolylineLength(obj.GeometryPoints) * scale;
            TxtPropQuantity.Text = $"{len:F2} {_doc.Calibration.LinearUnit}";
        }
        else
        {
            double area = SpatialCalculator.CalculatePolygonArea(obj.GeometryPoints) * scale * scale;
            TxtPropQuantity.Text = $"{area:F2} {_doc.Calibration.AreaUnit}";
        }

        BimPropertiesList.Clear();
        foreach (var kvp in obj.BimProperties)
        {
            BimPropertiesList.Add(new BimKeyValue { Key = kvp.Key, Value = kvp.Value });
        }

        TxtStatus.Text = $"Selected Takeoff Object: {obj.Name} ({obj.Category})";
        RedrawCanvas();
    }

    private void ClearBimInspector()
    {
        TxtPropName.Text = "";
        TxtPropCategory.Text = "";
        TxtPropType.Text = "";
        TxtPropQuantity.Text = "";
        BimPropertiesList.Clear();
    }

    private void PromptScaleCalibration()
    {
        if (_calibrationPoints.Count < 2) return;
        double pixelDist = _calibrationPoints[0].DistanceTo(_calibrationPoints[1]);
        _calibrationPoints.Clear();

        // Calibration prompt
        _doc.Calibration.CalibrationPixelDistance = pixelDist;
        _doc.Calibration.CalibrationRealDistance = 5.0; // 5 meters default
        _doc.Calibration.ScaleLabel = $"Calibrated ({pixelDist:F1}px = 5.0m)";

        MessageBox.Show($"Scale Calibrated successfully!\n{pixelDist:F1} drawing pixels = 5.0 {_doc.Calibration.LinearUnit}.",
            "Scale Calibration", MessageBoxButton.OK, MessageBoxImage.Information);

        SetTool("Select");
        UpdateUiFromDoc();
    }

    private void CalculateAndDisplayQto()
    {
        var report = _engine.CalculateReport(_doc);

        TxtGrandCount.Text = report.TotalItemCount.ToString();
        TxtGrandLength.Text = $"{report.GrandTotalLength:F2} {_doc.Calibration.LinearUnit}";
        TxtGrandArea.Text = $"{report.GrandTotalArea:F2} {_doc.Calibration.AreaUnit}";

        QtoRows.Clear();
        foreach (var item in report.Items)
        {
            if (item.ZoneBreakdowns.Count > 0)
            {
                foreach (var zb in item.ZoneBreakdowns)
                {
                    string qtyStr = item.MeasurementType switch
                    {
                        TakeoffMeasurementType.Count => $"{zb.Count} pcs",
                        TakeoffMeasurementType.Length => $"{zb.Length:F2} {_doc.Calibration.LinearUnit}",
                        _ => $"{zb.Area:F2} {_doc.Calibration.AreaUnit}"
                    };
                    QtoRows.Add(new QtoDisplayRow
                    {
                        ItemName = item.Name,
                        ZoneName = zb.ZoneName,
                        QuantityStr = qtyStr,
                        CostStr = $"${zb.Cost:F2}"
                    });
                }
            }
            else
            {
                string qtyStr = item.MeasurementType switch
                {
                    TakeoffMeasurementType.Count => $"{item.TotalCount} pcs",
                    TakeoffMeasurementType.Length => $"{item.TotalLength:F2} {_doc.Calibration.LinearUnit}",
                    _ => $"{item.TotalArea:F2} {_doc.Calibration.AreaUnit}"
                };
                QtoRows.Add(new QtoDisplayRow
                {
                    ItemName = item.Name,
                    ZoneName = "Unassigned",
                    QuantityStr = qtyStr,
                    CostStr = $"${item.TotalCost:F2}"
                });
            }
        }
    }

    // UI Event Handlers
    private void Tool_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.IsChecked == true)
        {
            string toolName = rb.Name switch
            {
                "RbCount" => "Count",
                "RbLength" => "Length",
                "RbArea" => "Area",
                "RbZone" => "Zone",
                "RbCalibrate" => "Calibrate",
                _ => "Select"
            };
            SetTool(toolName);
        }
    }

    private void SetTool(string tool)
    {
        _currentTool = tool;
        _drawingPoints.Clear();
        _calibrationPoints.Clear();

        if (TxtActiveToolHud != null)
        {
            TxtActiveToolHud.Text = tool;
        }

        if (TxtPromptHud != null)
        {
            TxtPromptHud.Text = (tool == "Length" || tool == "Area" || tool == "Zone")
                ? "Click points, double-click to finish"
                : (tool == "Calibrate" ? "Pick 2 points with known distance" : "");
        }

        if (tool == "Select" && RbSelect != null && RbSelect.IsChecked != true)
        {
            RbSelect.IsChecked = true;
        }

        if (BtnFinishShapeHud != null)
        {
            BtnFinishShapeHud.Visibility = Visibility.Collapsed;
        }

        if (FastCanvas != null)
        {
            RedrawCanvas();
        }
    }

    private void BtnOpenPdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Drawings (*.pdf;*.dxf;*.dwg)|*.pdf;*.dxf;*.dwg|PDF Drawings (*.pdf)|*.pdf|CAD Drawings (*.dxf;*.dwg)|*.dxf;*.dwg|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();
                using var stream = File.OpenRead(dlg.FileName);

                if (ext == ".pdf")
                {
                    var doc = _pdfReader.ReadPdf(stream, System.IO.Path.GetFileName(dlg.FileName));
                    doc.Calibration = _doc.Calibration;
                    if (_doc.Zones.Count > 0 && doc.Zones.Count == 0) doc.Zones = new List<Zone>(_doc.Zones);
                    _doc = doc;
                }
                else
                {
                    var doc = _dxfReader.ReadDxf(stream, System.IO.Path.GetFileName(dlg.FileName));
                    doc.Calibration = _doc.Calibration;
                    if (_doc.Zones.Count > 0 && doc.Zones.Count == 0) doc.Zones = new List<Zone>(_doc.Zones);
                    _doc = doc;
                }

                UpdateUiFromDoc();
                ZoomFit();
                MessageBox.Show($"Loaded drawing '{System.IO.Path.GetFileName(dlg.FileName)}' successfully!", "Drawing Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load drawing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnOpenBluebeam_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Bluebeam Revu Markups (*.csv;*.xml;*.btx;*.json)|*.csv;*.xml;*.btx;*.json|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                using var stream = File.OpenRead(dlg.FileName);
                var markups = _bluebeamReader.ReadMarkups(stream, dlg.FileName);
                _doc.TakeoffObjects.AddRange(markups);
                UpdateUiFromDoc();
                MessageBox.Show($"Successfully imported {markups.Count} Bluebeam markups with BIM property sets!", "Bluebeam Import", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import Bluebeam file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnLoadSample_Click(object sender, RoutedEventArgs e)
    {
        LoadDefaultSample();
    }

    private void BtnCalculateQto_Click(object sender, RoutedEventArgs e)
    {
        CalculateAndDisplayQto();
        MessageBox.Show("QTO calculations updated successfully across all defined layout zones!", "QTO Calculation", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "CSV File (*.csv)|*.csv",
            FileName = $"QTO_Takeoff_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (sfd.ShowDialog() == true)
        {
            var report = _engine.CalculateReport(_doc);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("ID,Name,Category,MeasurementType,Zone,Count,Length,Area,Cost");

            foreach (var item in report.Items)
            {
                if (item.ZoneBreakdowns.Count > 0)
                {
                    foreach (var zb in item.ZoneBreakdowns)
                    {
                        sb.AppendLine($"\"{item.ObjectId}\",\"{item.Name}\",\"{item.Category}\",\"{item.MeasurementType}\",\"{zb.ZoneName}\",{zb.Count},{zb.Length},{zb.Area},{zb.Cost}");
                    }
                }
                else
                {
                    sb.AppendLine($"\"{item.ObjectId}\",\"{item.Name}\",\"{item.Category}\",\"{item.MeasurementType}\",\"Unassigned\",{item.TotalCount},{item.TotalLength},{item.TotalArea},{item.TotalCost}");
                }
            }

            File.WriteAllText(sfd.FileName, sb.ToString());
            MessageBox.Show($"Takeoff Report exported successfully to {sfd.FileName}", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnSaveBim_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedObject == null) return;
        _selectedObject.Name = TxtPropName.Text;
        _selectedObject.Category = TxtPropCategory.Text;

        _selectedObject.BimProperties.Clear();
        foreach (var kvp in BimPropertiesList)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Key))
            {
                _selectedObject.SetBimProperty(kvp.Key, kvp.Value ?? "");
            }
        }

        UpdateUiFromDoc();
        MessageBox.Show("BIM properties updated!", "Properties Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnDeleteObject_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedObject == null) return;
        _doc.TakeoffObjects.Remove(_selectedObject);
        _selectedObject = null;
        ClearBimInspector();
        UpdateUiFromDoc();
    }

    private void BtnAddBimProp_Click(object sender, RoutedEventArgs e)
    {
        BimPropertiesList.Add(new BimKeyValue { Key = "NewProperty", Value = "Value" });
    }

    private void LbTakeoffObjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LbTakeoffObjects.SelectedItem is TakeoffObject obj)
        {
            SelectObject(obj);
        }
    }

    private void LbZones_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LbZones.SelectedItem is Zone zone)
        {
            _selectedZone = zone;
            _selectedObject = null;
            RedrawCanvas();
        }
    }

    private void BtnDeleteZone_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedZone == null) return;
        _doc.Zones.Remove(_selectedZone);
        _selectedZone = null;
        UpdateUiFromDoc();
    }

    private void BtnStartDrawZone_Click(object sender, RoutedEventArgs e)
    {
        RbZone.IsChecked = true;
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        CanvasScale.ScaleX *= 1.2;
        CanvasScale.ScaleY *= 1.2;
        RedrawCanvas();
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        CanvasScale.ScaleX /= 1.2;
        CanvasScale.ScaleY /= 1.2;
        RedrawCanvas();
    }

    private void BtnZoomFit_Click(object sender, RoutedEventArgs e) => ZoomFit();

    private void ZoomFit()
    {
        double viewportW = ViewportGrid.ActualWidth > 0 ? ViewportGrid.ActualWidth : 900;
        double viewportH = ViewportGrid.ActualHeight > 0 ? ViewportGrid.ActualHeight : 650;
        double docW = Math.Max(200, _doc.Width);
        double docH = Math.Max(200, _doc.Height);

        double s = Math.Min(viewportW / docW, viewportH / docH) * 0.85;
        CanvasScale.ScaleX = s;
        CanvasScale.ScaleY = s;
        CanvasTranslate.X = (viewportW - docW * s) / 2;
        CanvasTranslate.Y = (viewportH - docH * s) / 2;

        RedrawCanvas();
    }

    private void BtnToggleZones_Click(object sender, RoutedEventArgs e)
    {
        _showZones = !_showZones;
        BtnToggleZones.Content = _showZones ? "Zones (On)" : "Zones (Off)";
        RedrawCanvas();
    }

    private static SolidColorBrush ParseColor(string hex)
    {
        return new SolidColorBrush(ParseColorValue(hex));
    }

    private static Color ParseColorValue(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Color.FromRgb(59, 130, 246);
        }
    }
}

public class BimKeyValue
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class QtoDisplayRow
{
    public string ItemName { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public string QuantityStr { get; set; } = string.Empty;
    public string CostStr { get; set; } = string.Empty;
}
