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

    // In-progress drawing points (drawing-space coordinates)
    private readonly List<Point2D> _drawingPoints = new();
    private readonly List<Point2D> _calibrationPoints = new();
    private System.Windows.Point _currentMousePos;

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
        DrawingCanvas.Children.Clear();

        // 1. Draw Grid
        DrawGrid();

        // 2. Draw CAD / PDF Vector Entities
        foreach (var entity in _doc.Entities)
        {
            DrawEntity(entity);
        }

        // 3. Draw Specific Zones
        if (_showZones)
        {
            foreach (var zone in _doc.Zones)
            {
                DrawZone(zone);
            }
        }

        // 4. Draw Takeoff Objects
        foreach (var obj in _doc.TakeoffObjects)
        {
            DrawTakeoffObject(obj);
        }

        // 5. Draw Active In-Progress Polyline/Polygon
        if (_drawingPoints.Count > 0)
        {
            DrawActivePoly();
        }

        // 6. Draw Calibration Line
        if (_calibrationPoints.Count > 0)
        {
            DrawCalibrationLine();
        }
    }

    private void DrawGrid()
    {
        double width = Math.Max(2000, _doc.Width * 1.5);
        double height = Math.Max(1500, _doc.Height * 1.5);
        var gridBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));

        for (double x = 0; x <= width; x += 50)
        {
            var line = new Line { X1 = x, Y1 = 0, X2 = x, Y2 = height, Stroke = gridBrush, StrokeThickness = 0.5 };
            DrawingCanvas.Children.Add(line);
        }
        for (double y = 0; y <= height; y += 50)
        {
            var line = new Line { X1 = 0, Y1 = y, X2 = width, Y2 = y, Stroke = gridBrush, StrokeThickness = 0.5 };
            DrawingCanvas.Children.Add(line);
        }
    }

    private void DrawEntity(DrawingEntity ent)
    {
        if (ent.Points.Count == 0) return;
        var brush = ParseColor(ent.ColorHex ?? "#64748B");

        if (ent.Type == DrawingEntityType.Text)
        {
            var tb = new TextBlock
            {
                Text = ent.Text ?? "",
                Foreground = brush,
                FontSize = Math.Max(9, ent.TextSize),
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(tb, ent.Points[0].X);
            Canvas.SetTop(tb, ent.Points[0].Y);
            DrawingCanvas.Children.Add(tb);
        }
        else if (ent.Type == DrawingEntityType.Circle)
        {
            var ellipse = new Ellipse
            {
                Width = ent.Radius * 2,
                Height = ent.Radius * 2,
                Stroke = brush,
                StrokeThickness = ent.StrokeWidth
            };
            Canvas.SetLeft(ellipse, ent.Points[0].X - ent.Radius);
            Canvas.SetTop(ellipse, ent.Points[0].Y - ent.Radius);
            DrawingCanvas.Children.Add(ellipse);
        }
        else if (ent.Type == DrawingEntityType.Polygon || ent.IsClosed)
        {
            var poly = new Polygon
            {
                Stroke = brush,
                StrokeThickness = ent.StrokeWidth,
                Fill = Brushes.Transparent
            };
            foreach (var p in ent.Points) poly.Points.Add(new System.Windows.Point(p.X, p.Y));
            DrawingCanvas.Children.Add(poly);
        }
        else
        {
            var polyline = new Polyline
            {
                Stroke = brush,
                StrokeThickness = ent.StrokeWidth
            };
            foreach (var p in ent.Points) polyline.Points.Add(new System.Windows.Point(p.X, p.Y));
            DrawingCanvas.Children.Add(polyline);
        }
    }

    private void DrawZone(Zone zone)
    {
        if (zone.Boundary.Count < 3) return;

        bool isSelected = (_selectedZone?.Id == zone.Id);
        var baseColor = ParseColorValue(zone.ColorHex ?? "#10B981");
        var fillBrush = new SolidColorBrush(Color.FromArgb((byte)(zone.Opacity * 255), baseColor.R, baseColor.G, baseColor.B));
        var strokeBrush = new SolidColorBrush(baseColor);

        var poly = new Polygon
        {
            Fill = fillBrush,
            Stroke = strokeBrush,
            StrokeThickness = isSelected ? 3 : 1.5
        };
        if (isSelected)
        {
            poly.StrokeDashArray = new DoubleCollection { 4, 3 };
        }
        foreach (var p in zone.Boundary) poly.Points.Add(new System.Windows.Point(p.X, p.Y));
        DrawingCanvas.Children.Add(poly);

        // Zone text at centroid
        var centroid = SpatialCalculator.ComputeCentroid(zone.Boundary);
        var tb = new TextBlock
        {
            Text = $"{zone.Name} ({zone.Code})",
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 12
        };
        Canvas.SetLeft(tb, centroid.X - 40);
        Canvas.SetTop(tb, centroid.Y - 8);
        DrawingCanvas.Children.Add(tb);
    }

    private void DrawTakeoffObject(TakeoffObject obj)
    {
        if (obj.GeometryPoints.Count == 0) return;

        bool isSelected = (_selectedObject?.Id == obj.Id);
        var baseColor = ParseColorValue(obj.ColorHex ?? "#3B82F6");
        var fillBrush = new SolidColorBrush(Color.FromArgb((byte)(obj.Opacity * 255), baseColor.R, baseColor.G, baseColor.B));
        var strokeBrush = new SolidColorBrush(baseColor);

        if (obj.MeasurementType == TakeoffMeasurementType.Count)
        {
            foreach (var pt in obj.GeometryPoints)
            {
                var el = new Ellipse
                {
                    Width = 14,
                    Height = 14,
                    Fill = strokeBrush,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(el, pt.X - 7);
                Canvas.SetTop(el, pt.Y - 7);
                DrawingCanvas.Children.Add(el);
            }
        }
        else if (obj.MeasurementType == TakeoffMeasurementType.Length)
        {
            var polyline = new Polyline
            {
                Stroke = strokeBrush,
                StrokeThickness = isSelected ? 4 : obj.StrokeWidth
            };
            foreach (var p in obj.GeometryPoints) polyline.Points.Add(new System.Windows.Point(p.X, p.Y));
            DrawingCanvas.Children.Add(polyline);

            // Handle points
            foreach (var pt in obj.GeometryPoints)
            {
                var dot = new Ellipse { Width = 6, Height = 6, Fill = Brushes.White };
                Canvas.SetLeft(dot, pt.X - 3);
                Canvas.SetTop(dot, pt.Y - 3);
                DrawingCanvas.Children.Add(dot);
            }
        }
        else // Area or Perimeter
        {
            var poly = new Polygon
            {
                Fill = fillBrush,
                Stroke = strokeBrush,
                StrokeThickness = isSelected ? 3 : obj.StrokeWidth
            };
            foreach (var p in obj.GeometryPoints) poly.Points.Add(new System.Windows.Point(p.X, p.Y));
            DrawingCanvas.Children.Add(poly);
        }
    }

    private void DrawActivePoly()
    {
        var stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        var fill = new SolidColorBrush(Color.FromArgb(60, 245, 158, 11));

        if (_currentTool == "Area" || _currentTool == "Zone")
        {
            var poly = new Polygon { Stroke = stroke, StrokeThickness = 2, Fill = fill };
            foreach (var p in _drawingPoints) poly.Points.Add(new System.Windows.Point(p.X, p.Y));
            var cur = ScreenToDrawing(_currentMousePos);
            poly.Points.Add(new System.Windows.Point(cur.X, cur.Y));
            DrawingCanvas.Children.Add(poly);
        }
        else if (_currentTool == "Length")
        {
            var polyline = new Polyline { Stroke = stroke, StrokeThickness = 2 };
            foreach (var p in _drawingPoints) polyline.Points.Add(new System.Windows.Point(p.X, p.Y));
            var cur = ScreenToDrawing(_currentMousePos);
            polyline.Points.Add(new System.Windows.Point(cur.X, cur.Y));
            DrawingCanvas.Children.Add(polyline);
        }

        foreach (var p in _drawingPoints)
        {
            var dot = new Ellipse { Width = 8, Height = 8, Fill = stroke, Stroke = Brushes.White, StrokeThickness = 1 };
            Canvas.SetLeft(dot, p.X - 4);
            Canvas.SetTop(dot, p.Y - 4);
            DrawingCanvas.Children.Add(dot);
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
        DrawingCanvas.Children.Add(line);
    }

    // Viewport Mouse Events with Accurate Transformation
    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var screenPt = e.GetPosition(ViewportGrid);
        var drawPt = ScreenToDrawing(screenPt);

        // Middle button or right button or Space drags viewport
        if (e.MiddleButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
        {
            _isPanning = true;
            _panStart = screenPt;
            ViewportGrid.CaptureMouse();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if (e.ClickCount == 2)
            {
                // Double-click finishes polyline or polygon
                FinishCurrentShape();
                return;
            }

            switch (_currentTool)
            {
                case "Select":
                    PickObjectOrZone(drawPt);
                    break;

                case "Count":
                    CreateCountObject(drawPt);
                    break;

                case "Length":
                case "Area":
                case "Zone":
                    _drawingPoints.Add(drawPt);
                    RedrawCanvas();
                    break;

                case "Calibrate":
                    _calibrationPoints.Add(drawPt);
                    if (_calibrationPoints.Count == 2)
                    {
                        PromptScaleCalibration();
                    }
                    RedrawCanvas();
                    break;
            }
        }
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        _currentMousePos = e.GetPosition(ViewportGrid);
        var drawPt = ScreenToDrawing(_currentMousePos);
        TxtCursorCoords.Text = $"X: {drawPt.X:F1}, Y: {drawPt.Y:F1} (px)";

        if (_isPanning)
        {
            double dx = _currentMousePos.X - _panStart.X;
            double dy = _currentMousePos.Y - _panStart.Y;
            CanvasTranslate.X += dx;
            CanvasTranslate.Y += dy;
            _panStart = _currentMousePos;
            return;
        }

        if (_drawingPoints.Count > 0 || _calibrationPoints.Count > 0)
        {
            RedrawCanvas();
        }
    }

    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ViewportGrid.ReleaseMouseCapture();
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

    private void FinishCurrentShape()
    {
        if (_drawingPoints.Count < 2) return;
        var points = new List<Point2D>(_drawingPoints);
        _drawingPoints.Clear();

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

        if (DrawingCanvas != null)
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
