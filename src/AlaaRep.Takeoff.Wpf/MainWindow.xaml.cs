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

    // Cached background raster image source for high-performance direct rendering
    private System.Windows.Media.Imaging.BitmapSource? _bgImageSource;

    // Tools: Select, Count, Length, Area, Zone, Calibrate
    private string _currentTool = "Select";
    private readonly List<TakeoffObject> _selectedObjects = new();
    private TakeoffObject? _selectedObject => _selectedObjects.LastOrDefault();
    private Zone? _selectedZone;
    private bool _showZones = true;

    // Viewport Panning & Zooming
    private bool _isPanning;
    private System.Windows.Point _panStart;

    // Interactive Marquee Rectangle Selection
    private bool _isMarqueeSelecting;
    private Point2D? _marqueeStart;

    // Interactive Object Moving & Vertex Dragging
    private bool _isDraggingObject;
    private int _dragVertexIndex = -1;
    private Point2D? _dragStartDrawPt;

    // Clipboard for Copy / Paste
    private readonly List<TakeoffObject> _clipboardObjects = new();

    // In-progress drawing points (drawing-space coordinates)
    private readonly List<Point2D> _drawingPoints = new();
    private readonly List<Point2D> _calibrationPoints = new();
    private System.Windows.Point _currentMousePos;
    private SnapResult _currentSnap = new();

    // Observable collections for UI bindings
    public ObservableCollection<BimKeyValue> BimPropertiesList { get; } = new();
    public ObservableCollection<QtoDisplayRow> QtoRows { get; } = new();
    public ObservableCollection<TreeItemViewModel> HierarchyTreeRoots { get; } = new();
    public ObservableCollection<LayerItemViewModel> LayersList { get; } = new();

    // Layer visibility lookup dictionary: layerName -> isVisible
    private readonly Dictionary<string, bool> _layerVisibility = new(StringComparer.OrdinalIgnoreCase);

    // Selected hierarchy node in tree view
    private BimHierarchyNode? _selectedTreeNode;

    public MainWindow()
    {
        InitializeComponent();
        DgBimProperties.ItemsSource = BimPropertiesList;
        DgQtoReport.ItemsSource = QtoRows;
        TvBimHierarchy.ItemsSource = HierarchyTreeRoots;
        LbLayers.ItemsSource = LayersList;
        LoadDefaultSample();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.ApplyTheme(this, AppTheme.Dark);
        ZoomFit();
    }

    private void CbThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CbThemeSelector.SelectedItem is ComboBoxItem item && item.Content is string themeName)
        {
            var theme = themeName switch
            {
                "Light" => AppTheme.Light,
                "Desert" => AppTheme.Desert,
                "Sea" => AppTheme.Sea,
                _ => AppTheme.Dark
            };
            ThemeManager.ApplyTheme(this, theme);
            RedrawCanvas();
        }
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

        // BIM Hierarchy Tree: Building -> Floor -> Discipline
        var buildingA = new BimHierarchyNode { Name = "Main Tower Building", NodeType = HierarchyNodeType.Building, Code = "BLD-A" };
        var floor01 = new BimHierarchyNode { Name = "Level 01 - Ground Floor", NodeType = HierarchyNodeType.Floor, Code = "LVL-01" };
        var floor02 = new BimHierarchyNode { Name = "Level 02 - Executive Suites", NodeType = HierarchyNodeType.Floor, Code = "LVL-02" };
        buildingA.AddChild(floor01);
        buildingA.AddChild(floor02);

        var arch01 = new BimHierarchyNode { Name = "Architectural Finishes", NodeType = HierarchyNodeType.Discipline, Code = "ARCH" };
        var mep01 = new BimHierarchyNode { Name = "Mechanical & Fire", NodeType = HierarchyNodeType.Discipline, Code = "MEP" };
        floor01.AddChild(arch01);
        floor01.AddChild(mep01);

        doc.HierarchyRoot.Children.Clear();
        doc.HierarchyRoot.AddChild(buildingA);

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
            Building = "Main Tower Building",
            Floor = "Level 01 - Ground Floor",
            Discipline = "Architectural",
            HierarchyNodeId = arch01.Id,
            ColorHex = "#D97706",
            GeometryPoints = new List<Point2D> { new(120, 120), new(480, 120), new(480, 380), new(120, 380) }
        };
        arch01.TakeoffObjectIds.Add(oakFloor.Id);
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
            Building = "Main Tower Building",
            Floor = "Level 01 - Ground Floor",
            Discipline = "Architectural",
            HierarchyNodeId = arch01.Id,
            Height = 3.2, // 3.2m wall height
            ColorHex = "#EF4444",
            GeometryPoints = new List<Point2D> { new(150, 400), new(450, 400), new(750, 450), new(1050, 450) }
        };
        arch01.TakeoffObjectIds.Add(wallPartition.Id);
        wallPartition.SetBimProperty("Family", "Wall:Basic Wall Interior");
        wallPartition.SetBimProperty("Omniclass", "23-13 25 11 Drywall");
        wallPartition.SetBimProperty("Height", "3.20");
        wallPartition.SetBimProperty("UnitCost", "65.00");
        doc.TakeoffObjects.Add(wallPartition);

        var sprinklers = new TakeoffObject
        {
            Id = "obj-sprinklers",
            Name = "Pendant Sprinkler Head",
            Category = "Fire Protection",
            MeasurementType = TakeoffMeasurementType.Count,
            Building = "Main Tower Building",
            Floor = "Level 01 - Ground Floor",
            Discipline = "Fire Protection",
            HierarchyNodeId = mep01.Id,
            ColorHex = "#DC2626",
            GeometryPoints = new List<Point2D>
            {
                new(200, 200), new(350, 250), new(200, 500), new(350, 580),
                new(650, 220), new(900, 220), new(650, 580), new(800, 580), new(950, 580)
            }
        };
        mep01.TakeoffObjectIds.Add(sprinklers.Id);
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

        // Preserve TreeView expansion state across operations
        var expandedIds = new HashSet<string>();
        CollectExpandedNodeIds(HierarchyTreeRoots, expandedIds);
        string? activeSelectedId = _selectedTreeNode?.Id ?? _selectedObject?.Id;

        // Refresh BIM Hierarchy Tree with preserved expansion state
        HierarchyTreeRoots.Clear();
        HierarchyTreeRoots.Add(TreeItemViewModel.FromNode(
            _doc.HierarchyRoot,
            _doc.TakeoffObjects,
            _doc.Calibration,
            expandedIds.Count > 0 ? expandedIds : null,
            activeSelectedId));

        UpdateLayersList();
        CalculateAndDisplayQto();
        RedrawCanvas();
    }

    private void UpdateLayersList()
    {
        // Aggregate layers from Entities and TakeoffObjects
        var entityLayers = _doc.Entities
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Layer) ? "0 (Default)" : e.Layer)
            .Select(g => new { Name = g.Key, Count = g.Count(), Type = "CAD Layer" });

        var takeoffLayers = _doc.TakeoffObjects
            .GroupBy(o => string.IsNullOrWhiteSpace(o.Category) ? "Takeoff" : o.Category)
            .Select(g => new { Name = g.Key, Count = g.Count(), Type = "Takeoff" });

        var allDiscovered = entityLayers.Concat(takeoffLayers).ToList();

        // Preserve existing visibility state for discovered layers
        foreach (var l in allDiscovered)
        {
            if (!_layerVisibility.ContainsKey(l.Name))
            {
                _layerVisibility[l.Name] = true;
            }
        }

        LayersList.Clear();
        foreach (var l in allDiscovered)
        {
            LayersList.Add(new LayerItemViewModel
            {
                Name = l.Name,
                Count = l.Count,
                LayerType = l.Type,
                IsVisible = _layerVisibility[l.Name]
            });
        }
    }

    private void LayerCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is LayerItemViewModel vm)
        {
            _layerVisibility[vm.Name] = vm.IsVisible;
            RedrawCanvas();
        }
    }

    private void BtnShowAllLayers_Click(object sender, RoutedEventArgs e)
    {
        foreach (var layer in LayersList)
        {
            layer.IsVisible = true;
            _layerVisibility[layer.Name] = true;
        }
        RedrawCanvas();
    }

    private void LbLayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LbLayers.SelectedItem is LayerItemViewModel vm)
        {
            TxtNewLayerName.Text = vm.Name;
        }
    }

    private void BtnAddLayer_Click(object sender, RoutedEventArgs e)
    {
        string newName = TxtNewLayerName.Text.Trim();
        if (string.IsNullOrWhiteSpace(newName)) return;

        if (!_layerVisibility.ContainsKey(newName))
        {
            _layerVisibility[newName] = true;
        }

        if (!LayersList.Any(l => string.Equals(l.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            LayersList.Add(new LayerItemViewModel
            {
                Name = newName,
                Count = 0,
                LayerType = "Custom",
                IsVisible = true
            });
        }

        TxtStatus.Text = $"Layer '{newName}' added.";
    }

    private void BtnDeleteLayer_Click(object sender, RoutedEventArgs e)
    {
        var selectedLayer = LbLayers.SelectedItem as LayerItemViewModel;
        string layerToDelete = selectedLayer?.Name ?? TxtNewLayerName.Text.Trim();
        if (string.IsNullOrWhiteSpace(layerToDelete) || string.Equals(layerToDelete, "0 (Default)", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Please select a custom layer to delete. Default layer cannot be deleted.", "Delete Layer", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Reassign affected takeoff objects to default "Takeoff"
        foreach (var obj in _doc.TakeoffObjects)
        {
            if (string.Equals(obj.Category, layerToDelete, StringComparison.OrdinalIgnoreCase))
            {
                obj.Category = "General";
            }
        }

        // Reassign affected CAD entities
        foreach (var ent in _doc.Entities)
        {
            if (string.Equals(ent.Layer, layerToDelete, StringComparison.OrdinalIgnoreCase))
            {
                ent.Layer = "0 (Default)";
            }
        }

        _layerVisibility.Remove(layerToDelete);
        UpdateUiFromDoc();
        TxtStatus.Text = $"Layer '{layerToDelete}' deleted. Objects moved to default layer.";
    }

    private void BtnAssignLayerToSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedLayer = LbLayers.SelectedItem as LayerItemViewModel;
        string targetLayer = selectedLayer?.Name ?? TxtNewLayerName.Text.Trim();

        if (string.IsNullOrWhiteSpace(targetLayer))
        {
            MessageBox.Show("Please select or type a layer name to assign.", "Assign Layer", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_selectedObjects.Count == 0)
        {
            MessageBox.Show("Please select one or more takeoff objects to assign this layer to.", "Assign Layer", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var obj in _selectedObjects)
        {
            obj.Category = targetLayer;
            obj.SetBimProperty("Layer", targetLayer);
        }

        if (!_layerVisibility.ContainsKey(targetLayer))
        {
            _layerVisibility[targetLayer] = true;
        }

        UpdateUiFromDoc();
        UpdateBimInspector();
        TxtStatus.Text = $"Assigned layer '{targetLayer}' to {_selectedObjects.Count} selected object(s).";
    }

    private void BtnHideAllLayers_Click(object sender, RoutedEventArgs e)
    {
        foreach (var layer in LayersList)
        {
            layer.IsVisible = false;
            _layerVisibility[layer.Name] = false;
        }
        RedrawCanvas();
    }

    private void BtnInvertLayers_Click(object sender, RoutedEventArgs e)
    {
        foreach (var layer in LayersList)
        {
            layer.IsVisible = !layer.IsVisible;
            _layerVisibility[layer.Name] = layer.IsVisible;
        }
        RedrawCanvas();
    }

    private static void CollectExpandedNodeIds(IEnumerable<TreeItemViewModel> items, HashSet<string> set)
    {
        foreach (var item in items)
        {
            if (item.IsExpanded) set.Add(item.Id);
            CollectExpandedNodeIds(item.Children, set);
        }
    }

    // Node Level / Hierarchy Re-parenting Methods
    private void BtnIndentNode_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTreeNode == null || _selectedTreeNode == _doc.HierarchyRoot) return;

        // Find parent and index of selected node among siblings
        var parent = _doc.HierarchyRoot.FindNode(_selectedTreeNode.ParentId ?? "");
        if (parent == null) return;

        int index = parent.Children.IndexOf(_selectedTreeNode);
        if (index > 0)
        {
            var newParent = parent.Children[index - 1];
            parent.Children.RemoveAt(index);
            newParent.AddChild(_selectedTreeNode);
            UpdateUiFromDoc();
            TxtStatus.Text = $"Indented '{_selectedTreeNode.Name}' to become child of '{newParent.Name}'.";
        }
    }

    private void BtnOutdentNode_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTreeNode == null || _selectedTreeNode == _doc.HierarchyRoot) return;

        var parent = _doc.HierarchyRoot.FindNode(_selectedTreeNode.ParentId ?? "");
        if (parent == null || parent == _doc.HierarchyRoot) return;

        var grandParent = _doc.HierarchyRoot.FindNode(parent.ParentId ?? "");
        if (grandParent != null)
        {
            parent.Children.Remove(_selectedTreeNode);
            grandParent.AddChild(_selectedTreeNode);
            UpdateUiFromDoc();
            TxtStatus.Text = $"Outdented '{_selectedTreeNode.Name}' to level under '{grandParent.Name}'.";
        }
    }

    private void BtnChangeParent_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTreeNode == null || _selectedTreeNode == _doc.HierarchyRoot)
        {
            MessageBox.Show("Please select a node in the BIM Tree to change its parent.", "Change Parent", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Collect candidate parent nodes (all nodes except self and descendants)
        var candidates = new List<BimHierarchyNode>();
        void Collect(BimHierarchyNode node)
        {
            if (node.Id != _selectedTreeNode.Id)
            {
                candidates.Add(node);
                foreach (var c in node.Children)
                {
                    if (c.Id != _selectedTreeNode.Id) Collect(c);
                }
            }
        }
        Collect(_doc.HierarchyRoot);

        if (candidates.Count == 0) return;

        // Change to next candidate or root
        var currentParent = _doc.HierarchyRoot.FindNode(_selectedTreeNode.ParentId ?? "");
        int curIdx = currentParent != null ? candidates.IndexOf(currentParent) : 0;
        var nextParent = candidates[(curIdx + 1) % candidates.Count];

        if (currentParent != null) currentParent.Children.Remove(_selectedTreeNode);
        nextParent.AddChild(_selectedTreeNode);
        UpdateUiFromDoc();
        TxtStatus.Text = $"Re-parented '{_selectedTreeNode.Name}' under '{nextParent.Name}' ({nextParent.NodeType}).";
    }

    // BIM Tree Actions
    private void TvBimHierarchy_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeItemViewModel item)
        {
            if (item.IsObjectLeaf && item.AssociatedObject != null)
            {
                // Single object clicked in tree: highlight on drawing and select in inspector
                SelectObject(item.AssociatedObject);
                TxtTreeNodeName.Text = item.AssociatedObject.Name;
                TxtTreeNodeCode.Text = item.AssociatedObject.Category;
                TxtStatus.Text = $"Tree Object Selected: {item.AssociatedObject.Name} ({item.AssociatedObject.Category})";
            }
            else if (item.AssociatedNode != null)
            {
                _selectedTreeNode = item.AssociatedNode;
                TxtTreeNodeName.Text = item.AssociatedNode.Name;
                TxtTreeNodeCode.Text = item.AssociatedNode.Code ?? "";

                var objectIds = item.AssociatedNode.GetAllContainedObjectIds().ToList();

                // Highlight all objects contained under this node
                _selectedObjects.Clear();
                foreach (var id in objectIds)
                {
                    var found = _doc.TakeoffObjects.FirstOrDefault(o => o.Id == id);
                    if (found != null) _selectedObjects.Add(found);
                }

                UpdateBimInspector();
                RedrawCanvas();
                TxtStatus.Text = $"Tree Node Selected: {item.AssociatedNode.Name} ({item.AssociatedNode.NodeType}) with {objectIds.Count} items.";
            }
        }
    }

    private void BtnRenameTreeNode_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTreeNode != null)
        {
            _selectedTreeNode.Name = TxtTreeNodeName.Text.Trim();
            _selectedTreeNode.Code = TxtTreeNodeCode.Text.Trim();
            UpdateUiFromDoc();
            TxtStatus.Text = $"Renamed tree node to '{_selectedTreeNode.Name}'.";
        }
        else if (_selectedObject != null)
        {
            _selectedObject.Name = TxtTreeNodeName.Text.Trim();
            _selectedObject.Category = TxtTreeNodeCode.Text.Trim();
            UpdateUiFromDoc();
            TxtStatus.Text = $"Updated object '{_selectedObject.Name}'.";
        }
    }

    private void AddNodeAndSelect(BimHierarchyNode parent, BimHierarchyNode newNode)
    {
        parent.AddChild(newNode);
        _selectedTreeNode = newNode;
        TxtTreeNodeName.Text = newNode.Name;
        TxtTreeNodeCode.Text = newNode.Code ?? "";
        UpdateUiFromDoc();
        TxtStatus.Text = $"Added '{newNode.Name}' ({newNode.NodeType}) under '{parent.Name}'.";
    }

    private void BtnAddBuilding_Click(object sender, RoutedEventArgs e)
    {
        // Buildings are top-level assets under root
        var parent = _doc.HierarchyRoot;
        int count = parent.Children.Count(c => c.NodeType == HierarchyNodeType.Building) + 1;
        var building = new BimHierarchyNode
        {
            Name = $"Building {count}",
            NodeType = HierarchyNodeType.Building,
            Code = $"BLD-{count:00}"
        };
        AddNodeAndSelect(parent, building);
    }

    private void BtnAddFloor_Click(object sender, RoutedEventArgs e)
    {
        // Floor goes under selected Building, or parent of selected if under Building, or first Building, or root
        BimHierarchyNode? targetBuilding = null;
        if (_selectedTreeNode != null)
        {
            if (_selectedTreeNode.NodeType == HierarchyNodeType.Building)
            {
                targetBuilding = _selectedTreeNode;
            }
            else
            {
                // Traverse up to find ancestor Building
                var cur = _selectedTreeNode;
                while (cur != null && !string.IsNullOrEmpty(cur.ParentId))
                {
                    var p = _doc.HierarchyRoot.FindNode(cur.ParentId);
                    if (p != null && p.NodeType == HierarchyNodeType.Building)
                    {
                        targetBuilding = p;
                        break;
                    }
                    cur = p;
                }
            }
        }

        targetBuilding ??= _doc.HierarchyRoot.Children.FirstOrDefault(c => c.NodeType == HierarchyNodeType.Building);

        // If no building exists yet, create one first
        if (targetBuilding == null)
        {
            targetBuilding = new BimHierarchyNode
            {
                Name = "Building 01",
                NodeType = HierarchyNodeType.Building,
                Code = "BLD-01"
            };
            _doc.HierarchyRoot.AddChild(targetBuilding);
        }

        int count = targetBuilding.Children.Count(c => c.NodeType == HierarchyNodeType.Floor) + 1;
        var floor = new BimHierarchyNode
        {
            Name = $"Level {count:00}",
            NodeType = HierarchyNodeType.Floor,
            Code = $"LVL-{count:00}"
        };
        AddNodeAndSelect(targetBuilding, floor);
    }

    private void BtnAddDiscipline_Click(object sender, RoutedEventArgs e)
    {
        // Discipline goes under selected Floor (or selected node), or under first floor, or root
        BimHierarchyNode? targetFloor = null;
        if (_selectedTreeNode != null)
        {
            if (_selectedTreeNode.NodeType == HierarchyNodeType.Floor)
            {
                targetFloor = _selectedTreeNode;
            }
            else if (_selectedTreeNode.NodeType == HierarchyNodeType.Discipline)
            {
                targetFloor = _doc.HierarchyRoot.FindNode(_selectedTreeNode.ParentId ?? "") ?? _selectedTreeNode;
            }
            else if (_selectedTreeNode.NodeType == HierarchyNodeType.Building)
            {
                targetFloor = _selectedTreeNode.Children.FirstOrDefault(c => c.NodeType == HierarchyNodeType.Floor);
            }
        }

        if (targetFloor == null)
        {
            var firstBuilding = _doc.HierarchyRoot.Children.FirstOrDefault(c => c.NodeType == HierarchyNodeType.Building) ?? _doc.HierarchyRoot;
            targetFloor = firstBuilding.Children.FirstOrDefault(c => c.NodeType == HierarchyNodeType.Floor) ?? firstBuilding;
        }

        string[] disciplines = { "Architectural", "Structural", "Mechanical", "Electrical", "Plumbing", "Fire Protection" };
        int idx = targetFloor.Children.Count % disciplines.Length;
        string disc = disciplines[idx];

        var discNode = new BimHierarchyNode
        {
            Name = $"{disc} Works",
            NodeType = HierarchyNodeType.Discipline,
            Code = disc.Length >= 4 ? disc.Substring(0, 4).ToUpper() : disc.ToUpper()
        };
        AddNodeAndSelect(targetFloor, discNode);
    }

    private void BtnAddChildNode_Click(object sender, RoutedEventArgs e)
    {
        var targetNode = _selectedTreeNode ?? _doc.HierarchyRoot;
        var nextType = targetNode.NodeType switch
        {
            HierarchyNodeType.Project => HierarchyNodeType.Building,
            HierarchyNodeType.Building => HierarchyNodeType.Floor,
            HierarchyNodeType.Floor => HierarchyNodeType.Discipline,
            HierarchyNodeType.Discipline => HierarchyNodeType.Category,
            _ => HierarchyNodeType.Category
        };

        int count = targetNode.Children.Count + 1;
        var childNode = new BimHierarchyNode
        {
            Name = $"{nextType} {count:00}",
            NodeType = nextType,
            Code = $"{nextType.ToString().Substring(0, Math.Min(3, nextType.ToString().Length)).ToUpper()}-{count:00}"
        };
        AddNodeAndSelect(targetNode, childNode);
    }

    private void BtnAssignToSelectedNode_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTreeNode == null)
        {
            MessageBox.Show("Please select a node in the BIM Tree first.", "BIM Hierarchy", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_selectedObjects.Count == 0)
        {
            MessageBox.Show("Please select one or more takeoff objects on the drawing or list to assign.", "BIM Hierarchy", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var obj in _selectedObjects)
        {
            // Remove from any prior node
            RemoveObjectFromHierarchyTree(_doc.HierarchyRoot, obj.Id);

            // Assign to selected node
            if (!_selectedTreeNode.TakeoffObjectIds.Contains(obj.Id))
            {
                _selectedTreeNode.TakeoffObjectIds.Add(obj.Id);
            }
            obj.HierarchyNodeId = _selectedTreeNode.Id;

            // Set BIM properties according to node path
            SetNodePathProperties(obj, _selectedTreeNode);
        }

        UpdateUiFromDoc();
        MessageBox.Show($"Assigned {_selectedObjects.Count} object(s) to '{_selectedTreeNode.Name}' ({_selectedTreeNode.NodeType})!", "Assigned", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SetNodePathProperties(TakeoffObject obj, BimHierarchyNode node)
    {
        if (node.NodeType == HierarchyNodeType.Building) obj.Building = node.Name;
        if (node.NodeType == HierarchyNodeType.Floor) obj.Floor = node.Name;
        if (node.NodeType == HierarchyNodeType.Discipline) obj.Discipline = node.Name;

        // Traverse up to root to populate building and floor
        var cur = node;
        while (cur != null && !string.IsNullOrEmpty(cur.ParentId))
        {
            var parent = _doc.HierarchyRoot.FindNode(cur.ParentId);
            if (parent != null)
            {
                if (parent.NodeType == HierarchyNodeType.Building) obj.Building = parent.Name;
                if (parent.NodeType == HierarchyNodeType.Floor) obj.Floor = parent.Name;
                if (parent.NodeType == HierarchyNodeType.Discipline) obj.Discipline = parent.Name;
            }
            cur = parent;
        }

        if (!string.IsNullOrEmpty(obj.Building)) obj.SetBimProperty("Building", obj.Building);
        if (!string.IsNullOrEmpty(obj.Floor)) obj.SetBimProperty("Floor", obj.Floor);
        if (!string.IsNullOrEmpty(obj.Discipline)) obj.SetBimProperty("Discipline", obj.Discipline);
    }

    private static void RemoveObjectFromHierarchyTree(BimHierarchyNode node, string objId)
    {
        node.TakeoffObjectIds.Remove(objId);
        foreach (var child in node.Children)
        {
            RemoveObjectFromHierarchyTree(child, objId);
        }
    }

    private void BtnDeleteTreeNode_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTreeNode == null || _selectedTreeNode == _doc.HierarchyRoot) return;

        // Remove from parent
        var parent = _doc.HierarchyRoot.FindNode(_selectedTreeNode.ParentId ?? "");
        if (parent != null)
        {
            parent.Children.Remove(_selectedTreeNode);
            _selectedTreeNode = null;
            UpdateUiFromDoc();
        }
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
            // 1. Draw Architectural Drawing Sheet / Layout Boundary
            double docW = Math.Max(500, _doc.Width);
            double docH = Math.Max(400, _doc.Height);
            var sheetBg = new SolidColorBrush(Color.FromRgb(10, 15, 29));
            sheetBg.Freeze();
            var sheetPen = new Pen(new SolidColorBrush(Color.FromRgb(51, 65, 85)), 1.5);
            sheetPen.Freeze();
            dc.DrawRectangle(sheetBg, sheetPen, new Rect(0, 0, docW, docH));

            // 1b. Draw Large Background Raster Image (if imported) with hardware acceleration
            if (_bgImageSource != null)
            {
                if (_doc.BackgroundImageOpacity < 1.0)
                {
                    dc.PushOpacity(_doc.BackgroundImageOpacity);
                }
                dc.DrawImage(_bgImageSource, new Rect(0, 0, _doc.Width, _doc.Height));
                if (_doc.BackgroundImageOpacity < 1.0)
                {
                    dc.Pop();
                }
            }

            // 2. High-Performance Viewport Frustum Culling & Sub-pixel Line Decimation (AutoCAD / Revit style)
            // Compute visible drawing bounds in drawing coordinates
            double vpW = ViewportGrid.ActualWidth > 0 ? ViewportGrid.ActualWidth : 1200;
            double vpH = ViewportGrid.ActualHeight > 0 ? ViewportGrid.ActualHeight : 800;
            var topLeftDraw = ScreenToDrawing(new System.Windows.Point(0, 0));
            var bottomRightDraw = ScreenToDrawing(new System.Windows.Point(vpW, vpH));

            var frustumBounds = new BoundingBox2D(
                Math.Min(topLeftDraw.X, bottomRightDraw.X) - 50,
                Math.Min(topLeftDraw.Y, bottomRightDraw.Y) - 50,
                Math.Max(topLeftDraw.X, bottomRightDraw.X) + 50,
                Math.Max(topLeftDraw.Y, bottomRightDraw.Y) + 50);

            // Query spatial QuadTree to only retrieve visible entities in O(log N) time
            var visibleEntities = new List<DrawingEntity>();
            var spatialIndex = _doc.GetOrBuildSpatialIndex();
            spatialIndex.Query(frustumBounds, visibleEntities);

            double currentScale = Math.Max(0.01, CanvasScale.ScaleX);
            double minPixelSpan = 1.0 / currentScale; // Entities smaller than 1 screen pixel are decimated

            foreach (var entity in visibleEntities)
            {
                string layerKey = string.IsNullOrWhiteSpace(entity.Layer) ? "0 (Default)" : entity.Layer;
                if (_layerVisibility.TryGetValue(layerKey, out bool isVis) && !isVis)
                {
                    continue; // Layer is hidden
                }

                // Sub-pixel line decimation: skip micro-geometry that cannot be resolved at current zoom level
                if (entity.Points.Count >= 2 && entity.Type != DrawingEntityType.Circle)
                {
                    double dx = entity.Points[^1].X - entity.Points[0].X;
                    double dy = entity.Points[^1].Y - entity.Points[0].Y;
                    if ((dx * dx + dy * dy) < (minPixelSpan * minPixelSpan) && entity.Type != DrawingEntityType.Text)
                    {
                        continue; // Decimated: smaller than 1 pixel
                    }
                }

                DrawEntityDirect(dc, entity);
            }

            // 3. Draw Specific Zones
            if (_showZones)
            {
                foreach (var zone in _doc.Zones)
                {
                    DrawZoneDirect(dc, zone);
                }
            }

            // 4. Draw Takeoff Objects (filtered by layer/category visibility)
            foreach (var obj in _doc.TakeoffObjects)
            {
                string catKey = string.IsNullOrWhiteSpace(obj.Category) ? "Takeoff" : obj.Category;
                if (_layerVisibility.TryGetValue(catKey, out bool isVis) && !isVis)
                {
                    continue; // Category/Layer is hidden
                }
                DrawTakeoffObjectDirect(dc, obj, _selectedObjects.Contains(obj));
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

        // Draw Interactive Vertex Handles for Selected Objects
        foreach (var obj in _selectedObjects)
        {
            if (obj.GeometryPoints.Count > 0)
            {
                DrawSelectionHandles(obj);
            }
        }

        // Draw Marquee Selection Box
        if (_isMarqueeSelecting && _marqueeStart != null)
        {
            DrawMarqueeBox(_marqueeStart, ScreenToDrawing(_currentMousePos));
        }

        // Draw Snapping Glyph (Orange square for Endpoint, Green triangle for Midpoint, Blue hourglass for Nearest)
        if (_currentSnap != null && _currentSnap.DidSnap)
        {
            DrawSnapGlyph(_currentSnap);
        }
    }

    private void DrawMarqueeBox(Point2D p1, Point2D p2)
    {
        double x = Math.Min(p1.X, p2.X);
        double y = Math.Min(p1.Y, p2.Y);
        double w = Math.Abs(p1.X - p2.X);
        double h = Math.Abs(p1.Y - p2.Y);

        var rect = new Rectangle
        {
            Width = w,
            Height = h,
            Fill = new SolidColorBrush(Color.FromArgb(40, 59, 130, 246)),
            Stroke = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 }
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        PreviewCanvas.Children.Add(rect);
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

    private void DrawTakeoffObjectDirect(DrawingContext dc, TakeoffObject obj, bool isSelected)
    {
        if (obj.GeometryPoints.Count == 0) return;
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
                    bool isShiftOrCtrl = (Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0;

                    // 1. Check if clicked an existing vertex handle of any selected object
                    if (_selectedObject != null)
                    {
                        double handleRadius = 12.0 / CanvasScale.ScaleX;
                        for (int i = 0; i < _selectedObject.GeometryPoints.Count; i++)
                        {
                            if (_selectedObject.GeometryPoints[i].DistanceTo(drawPt) <= handleRadius)
                            {
                                _isDraggingObject = true;
                                _dragVertexIndex = i;
                                _dragStartDrawPt = drawPt;
                                ViewportGrid.CaptureMouse();
                                return;
                            }
                        }
                    }

                    // 2. Hit test single object
                    var hit = FindObjectAt(drawPt);
                    if (hit != null)
                    {
                        if (isShiftOrCtrl)
                        {
                            if (_selectedObjects.Contains(hit)) _selectedObjects.Remove(hit);
                            else _selectedObjects.Add(hit);
                        }
                        else if (!_selectedObjects.Contains(hit))
                        {
                            _selectedObjects.Clear();
                            _selectedObjects.Add(hit);
                        }

                        _isDraggingObject = true;
                        _dragVertexIndex = -1;
                        _dragStartDrawPt = drawPt;
                        ViewportGrid.CaptureMouse();
                        UpdateBimInspector();
                        RedrawCanvas();
                        return;
                    }

                    // 3. Hit test zone
                    var hitZone = FindZoneAt(drawPt);
                    if (hitZone != null)
                    {
                        SelectZone(hitZone);
                        return;
                    }

                    // 4. Clicked empty space -> start Rectangle Marquee Selection
                    if (!isShiftOrCtrl)
                    {
                        _selectedObjects.Clear();
                        _selectedZone = null;
                        ClearBimInspector();
                    }
                    _isMarqueeSelecting = true;
                    _marqueeStart = drawPt;
                    ViewportGrid.CaptureMouse();
                    RedrawCanvas();
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
        if (_isDraggingObject && _selectedObjects.Count > 0 && _dragStartDrawPt != null)
        {
            double deltaX = rawDrawPt.X - _dragStartDrawPt.X;
            double deltaY = rawDrawPt.Y - _dragStartDrawPt.Y;

            if (_dragVertexIndex >= 0 && _selectedObject != null && _dragVertexIndex < _selectedObject.GeometryPoints.Count)
            {
                _selectedObject.GeometryPoints[_dragVertexIndex] = rawDrawPt;
            }
            else
            {
                // Move all selected objects together
                foreach (var obj in _selectedObjects)
                {
                    obj.MoveBy(deltaX, deltaY);
                }
                _dragStartDrawPt = rawDrawPt;
            }

            UpdateBimInspector();
            RedrawCanvas();
            return;
        }

        if (_isMarqueeSelecting)
        {
            RedrawPreview();
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

        if (_isMarqueeSelecting && _marqueeStart != null)
        {
            _isMarqueeSelecting = false;
            ViewportGrid.ReleaseMouseCapture();

            var marqueeEnd = ScreenToDrawing(_currentMousePos);
            double left = Math.Min(_marqueeStart.X, marqueeEnd.X);
            double right = Math.Max(_marqueeStart.X, marqueeEnd.X);
            double top = Math.Min(_marqueeStart.Y, marqueeEnd.Y);
            double bottom = Math.Max(_marqueeStart.Y, marqueeEnd.Y);

            // Select all objects intersecting the marquee rectangle
            foreach (var obj in _doc.TakeoffObjects)
            {
                if (SpatialCalculator.IntersectsRect(obj.GeometryPoints, left, top, right, bottom))
                {
                    if (!_selectedObjects.Contains(obj))
                    {
                        _selectedObjects.Add(obj);
                    }
                }
            }

            _marqueeStart = null;
            UpdateBimInspector();
            RedrawCanvas();
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
            if (_selectedObjects.Count > 0)
            {
                foreach (var obj in _selectedObjects)
                {
                    _doc.TakeoffObjects.Remove(obj);
                }
                _selectedObjects.Clear();
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
        if (_selectedObjects.Count > 0)
        {
            _clipboardObjects.Clear();
            foreach (var obj in _selectedObjects)
            {
                _clipboardObjects.Add(obj.Clone(0));
            }
            TxtStatus.Text = $"Copied {_clipboardObjects.Count} objects to clipboard.";
        }
    }

    private void PasteObject()
    {
        if (_clipboardObjects.Count > 0)
        {
            var pastedList = new List<TakeoffObject>();
            foreach (var clip in _clipboardObjects)
            {
                var pasted = clip.Clone(30.0);
                _doc.TakeoffObjects.Add(pasted);
                pastedList.Add(pasted);
            }

            _selectedObjects.Clear();
            _selectedObjects.AddRange(pastedList);
            UpdateBimInspector();
            UpdateUiFromDoc();
            TxtStatus.Text = $"Pasted {pastedList.Count} objects. You can drag them together.";
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

    private TakeoffObject? FindObjectAt(Point2D pt)
    {
        double scale = CanvasScale.ScaleX;
        double hitRadius = 15.0 / scale;

        foreach (var obj in _doc.TakeoffObjects.AsEnumerable().Reverse())
        {
            if (obj.MeasurementType == TakeoffMeasurementType.Count)
            {
                if (obj.GeometryPoints.Any(p => p.DistanceTo(pt) <= hitRadius))
                    return obj;
            }
            else if (obj.MeasurementType == TakeoffMeasurementType.Length)
            {
                for (int i = 0; i < obj.GeometryPoints.Count - 1; i++)
                {
                    if (SpatialCalculator.DistanceToSegment(pt, obj.GeometryPoints[i], obj.GeometryPoints[i + 1]) <= hitRadius)
                        return obj;
                }
            }
            else
            {
                if (SpatialCalculator.IsPointInPolygon(pt, obj.GeometryPoints))
                    return obj;
            }
        }
        return null;
    }

    private Zone? FindZoneAt(Point2D pt)
    {
        foreach (var zone in _doc.Zones.AsEnumerable().Reverse())
        {
            if (SpatialCalculator.IsPointInPolygon(pt, zone.Boundary))
                return zone;
        }
        return null;
    }

    private void SelectObject(TakeoffObject obj)
    {
        _selectedObjects.Clear();
        _selectedObjects.Add(obj);
        _selectedZone = null;
        UpdateBimInspector();
        RedrawCanvas();
    }

    private void UpdateBimInspector()
    {
        if (_selectedObjects.Count == 0)
        {
            ClearBimInspector();
            return;
        }

        var obj = _selectedObjects.Last();
        TxtPropName.Text = _selectedObjects.Count == 1 ? obj.Name : $"Multiple Objects ({_selectedObjects.Count} selected)";
        TxtPropCategory.Text = obj.Category;
        TxtPropType.Text = _selectedObjects.Count == 1 ? obj.MeasurementType.ToString() : "Mixed";

        if (TxtPropHeight != null)
        {
            if (_selectedObjects.Count == 1)
            {
                TxtPropHeight.Text = obj.Height > 0
                    ? obj.Height.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                    : (obj.MeasurementType == TakeoffMeasurementType.Length ? "3.00" : "0.00");
                TxtPropHeight.IsEnabled = (obj.MeasurementType == TakeoffMeasurementType.Length);
            }
            else
            {
                TxtPropHeight.Text = "";
                TxtPropHeight.IsEnabled = _selectedObjects.Any(o => o.MeasurementType == TakeoffMeasurementType.Length);
            }
        }

        // Refresh and select layer in CbObjectLayer dropdown
        RefreshObjectLayerDropdown(obj.Layer);

        double scale = _doc.Calibration.UnitsPerCoordinate;
        if (_selectedObjects.Count == 1)
        {
            if (obj.MeasurementType == TakeoffMeasurementType.Count)
                TxtPropQuantity.Text = $"{obj.GeometryPoints.Count} pcs";
            else if (obj.MeasurementType == TakeoffMeasurementType.Length)
                TxtPropQuantity.Text = $"{SpatialCalculator.CalculatePolylineLength(obj.GeometryPoints) * scale:F2} {_doc.Calibration.LinearUnit}";
            else
                TxtPropQuantity.Text = $"{SpatialCalculator.CalculatePolygonArea(obj.GeometryPoints) * scale * scale:F2} {_doc.Calibration.AreaUnit}";
        }
        else
        {
            TxtPropQuantity.Text = $"{_selectedObjects.Count} items selected";
        }

        BimPropertiesList.Clear();
        foreach (var kvp in obj.BimProperties)
        {
            BimPropertiesList.Add(new BimKeyValue { Key = kvp.Key, Value = kvp.Value });
        }

        if (TxtColorHex != null)
        {
            TxtColorHex.Text = obj.ColorHex ?? "#3B82F6";
        }
        if (SliderOpacity != null)
        {
            SliderOpacity.Value = obj.Opacity;
        }

        TxtStatus.Text = _selectedObjects.Count == 1
            ? $"Selected: {obj.Name} ({obj.Category})"
            : $"Selected {_selectedObjects.Count} objects via rectangle marquee.";
    }

    private void BtnColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            if (TxtColorHex != null) TxtColorHex.Text = hex;
            ApplyColorToSelected(hex);
        }
    }

    private void TxtColorHex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtColorHex == null) return;
        string hex = TxtColorHex.Text.Trim();
        if (hex.StartsWith("#") && (hex.Length == 4 || hex.Length == 7 || hex.Length == 9))
        {
            ApplyColorToSelected(hex);
        }
    }

    private void ApplyColorToSelected(string hex)
    {
        if (_selectedObjects.Count == 0 && _selectedZone == null) return;

        foreach (var obj in _selectedObjects)
        {
            obj.ColorHex = hex;
        }
        if (_selectedZone != null)
        {
            _selectedZone.ColorHex = hex;
        }
        RedrawCanvas();
    }

    private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtOpacityValue != null)
        {
            TxtOpacityValue.Text = $"{(int)(e.NewValue * 100)}%";
        }

        if (_selectedObjects.Count == 0 && _selectedZone == null) return;

        foreach (var obj in _selectedObjects)
        {
            obj.Opacity = e.NewValue;
        }
        if (_selectedZone != null)
        {
            _selectedZone.Opacity = e.NewValue;
        }
        RedrawCanvas();
    }

    private bool _isUpdatingLayerDropdown;
    private void RefreshObjectLayerDropdown(string currentLayer)
    {
        if (CbObjectLayer == null) return;
        _isUpdatingLayerDropdown = true;
        try
        {
            var layers = LayersList.Select(l => l.Name).Distinct().ToList();
            if (!string.IsNullOrEmpty(currentLayer) && !layers.Contains(currentLayer))
            {
                layers.Add(currentLayer);
            }
            CbObjectLayer.ItemsSource = layers;
            CbObjectLayer.SelectedItem = currentLayer;
        }
        finally
        {
            _isUpdatingLayerDropdown = false;
        }
    }

    private void CbObjectLayer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingLayerDropdown) return;
        if (CbObjectLayer.SelectedItem is string selectedLayer && _selectedObjects.Count > 0)
        {
            foreach (var obj in _selectedObjects)
            {
                obj.Layer = selectedLayer;
            }
            if (TxtPropCategory != null) TxtPropCategory.Text = selectedLayer;
            if (!_layerVisibility.ContainsKey(selectedLayer))
            {
                _layerVisibility[selectedLayer] = true;
            }
            UpdateUiFromDoc();
            TxtStatus.Text = $"Changed layer to '{selectedLayer}'.";
        }
    }

    private void ClearBimInspector()
    {
        TxtPropName.Text = "";
        TxtPropCategory.Text = "";
        TxtPropType.Text = "";
        if (TxtPropHeight != null) TxtPropHeight.Text = "";
        TxtPropQuantity.Text = "";
        if (CbObjectLayer != null) CbObjectLayer.SelectedItem = null;
        BimPropertiesList.Clear();
    }

    private void PromptScaleCalibration()
    {
        if (_calibrationPoints.Count < 2) return;
        double pixelDist = _calibrationPoints[0].DistanceTo(_calibrationPoints[1]);
        _calibrationPoints.Clear();

        var dialog = new CalibrationDialog(pixelDist, _doc.Calibration)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _doc.Calibration.CalibrationPixelDistance = dialog.PixelDistance;
            _doc.Calibration.CalibrationRealDistance = dialog.RealDistance;
            _doc.Calibration.LinearUnit = dialog.Unit;
            _doc.Calibration.AreaUnit = dialog.AreaUnit;
            _doc.Calibration.ScaleLabel = dialog.ScaleLabel;

            TxtStatus.Text = $"Scale calibrated: {dialog.PixelDistance:F1}px = {dialog.RealDistance:F2}{dialog.Unit}.";
        }

        SetTool("Select");
        UpdateUiFromDoc();
    }

    private void CalculateAndDisplayQto()
    {
        var report = _engine.CalculateReport(_doc);

        TxtGrandCount.Text = report.TotalItemCount.ToString();
        TxtGrandLength.Text = $"{report.GrandTotalLength:F2} {_doc.Calibration.LinearUnit}";
        TxtGrandArea.Text = $"{report.GrandTotalArea:F2} {_doc.Calibration.AreaUnit}";
        if (TxtGrandWallArea != null)
        {
            TxtGrandWallArea.Text = $"{report.GrandTotalWallArea:F2} {_doc.Calibration.AreaUnit}";
        }

        QtoRows.Clear();
        foreach (var item in report.Items)
        {
            var rawObj = _doc.TakeoffObjects.FirstOrDefault(o => o.Id == item.ObjectId);
            string hier = "";
            if (rawObj != null)
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(rawObj.Floor)) parts.Add(rawObj.Floor.Replace("Level ", "L"));
                if (!string.IsNullOrEmpty(rawObj.Discipline)) parts.Add(rawObj.Discipline.Length > 4 ? rawObj.Discipline.Substring(0, 4) : rawObj.Discipline);
                hier = string.Join(" / ", parts);
            }

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
                    string wallStr = zb.WallArea > 0 ? $"{zb.WallArea:F2} {_doc.Calibration.AreaUnit}" : "-";

                    QtoRows.Add(new QtoDisplayRow
                    {
                        ItemName = item.Name,
                        HierarchyPath = hier,
                        ZoneName = zb.ZoneName,
                        QuantityStr = qtyStr,
                        WallAreaStr = wallStr,
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
                string wallStr = item.TotalWallArea > 0 ? $"{item.TotalWallArea:F2} {_doc.Calibration.AreaUnit}" : "-";

                QtoRows.Add(new QtoDisplayRow
                {
                    ItemName = item.Name,
                    HierarchyPath = hier,
                    ZoneName = "Unassigned",
                    QuantityStr = qtyStr,
                    WallAreaStr = wallStr,
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

    private async void BtnOpenPdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Drawings (*.pdf;*.dxf;*.dwg)|*.pdf;*.dxf;*.dwg|PDF Drawings (*.pdf)|*.pdf|CAD Drawings (*.dxf;*.dwg)|*.dxf;*.dwg|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            string fileName = dlg.FileName;
            string shortName = System.IO.Path.GetFileName(fileName);
            string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

            TxtStatus.Text = $"Loading '{shortName}' in background thread (no freezing)...";
            Cursor = Cursors.Wait;

            try
            {
                var calib = _doc.Calibration;
                var existingZones = _doc.Zones.Count > 0 ? new List<Zone>(_doc.Zones) : null;

                var loadedDoc = await Task.Run(() =>
                {
                    using var stream = File.OpenRead(fileName);
                    if (ext == ".pdf")
                    {
                        var doc = _pdfReader.ReadPdf(stream, shortName);
                        doc.Calibration = calib;
                        if (existingZones != null && doc.Zones.Count == 0) doc.Zones = existingZones;
                        return doc;
                    }
                    else
                    {
                        var doc = _dxfReader.ReadDxf(stream, shortName);
                        doc.Calibration = calib;
                        if (existingZones != null && doc.Zones.Count == 0) doc.Zones = existingZones;
                        return doc;
                    }
                });

                _doc = loadedDoc;
                UpdateUiFromDoc();
                ZoomFit();
                TxtStatus.Text = $"Ready - Loaded '{shortName}' ({_doc.Entities.Count} entities).";
                MessageBox.Show($"Loaded drawing '{shortName}' successfully without freezing!", "Drawing Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Error loading drawing.";
                MessageBox.Show($"Failed to load drawing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Cursor = Cursors.Arrow;
            }
        }
    }

    private async void BtnImportImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import Large Architectural Plan / Drawing Image",
            Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif)|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            string fileName = dlg.FileName;
            string shortName = System.IO.Path.GetFileName(fileName);

            TxtStatus.Text = $"Loading large plan '{shortName}' asynchronously...";
            Cursor = Cursors.Wait;

            try
            {
                // Decode image on background thread for large high-res plans
                var bi = await Task.Run(() =>
                {
                    var image = new System.Windows.Media.Imaging.BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(fileName);
                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze(); // Allow cross-thread access
                    return image;
                });

                _bgImageSource = bi;

                // Create or adapt DrawingDocument to match image dimensions
                _doc.FileName = shortName;
                _doc.FileType = "Raster";
                _doc.Width = bi.PixelWidth;
                _doc.Height = bi.PixelHeight;
                _doc.BackgroundImagePath = fileName;
                _doc.BackgroundImageWidth = bi.PixelWidth;
                _doc.BackgroundImageHeight = bi.PixelHeight;

                if (SliderBgOpacity != null)
                {
                    SliderBgOpacity.Value = _doc.BackgroundImageOpacity;
                }

                UpdateUiFromDoc();
                ZoomFit();
                TxtStatus.Text = $"Ready - Plan loaded ({bi.PixelWidth} x {bi.PixelHeight} px).";

                var res = MessageBox.Show(
                    $"Imported drawing image ({bi.PixelWidth} x {bi.PixelHeight} px) without lag!\n\nWould you like to Calibrate Scale now (pick 2 points of known dimension on the plan)?",
                    "Drawing Imported",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    SetTool("Calibrate");
                    if (RbCalibrate != null) RbCalibrate.IsChecked = true;
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Error importing plan image.";
                MessageBox.Show($"Failed to load drawing image: {ex.Message}", "Image Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Cursor = Cursors.Arrow;
            }
        }
    }

    private void SliderBgOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtBgImageOpacity != null)
        {
            TxtBgImageOpacity.Text = $"{(int)(e.NewValue * 100)}%";
        }
        _doc.BackgroundImageOpacity = e.NewValue;
        RedrawCanvas();
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
            sb.AppendLine("ID,Name,Category,MeasurementType,Zone,Height,Count,Length,FloorArea,WallArea,Cost");

            foreach (var item in report.Items)
            {
                if (item.ZoneBreakdowns.Count > 0)
                {
                    foreach (var zb in item.ZoneBreakdowns)
                    {
                        sb.AppendLine($"\"{item.ObjectId}\",\"{item.Name}\",\"{item.Category}\",\"{item.MeasurementType}\",\"{zb.ZoneName}\",{item.Height:F2},{zb.Count},{zb.Length:F2},{zb.Area:F2},{zb.WallArea:F2},{zb.Cost:F2}");
                    }
                }
                else
                {
                    sb.AppendLine($"\"{item.ObjectId}\",\"{item.Name}\",\"{item.Category}\",\"{item.MeasurementType}\",\"Unassigned\",{item.Height:F2},{item.TotalCount},{item.TotalLength:F2},{item.TotalArea:F2},{item.TotalWallArea:F2},{item.TotalCost:F2}");
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
        if (CbObjectLayer.SelectedItem is string chosenLayer && !string.IsNullOrWhiteSpace(chosenLayer))
        {
            _selectedObject.Layer = chosenLayer;
        }
        else
        {
            _selectedObject.Layer = TxtPropCategory.Text;
        }
        _selectedObject.Category = _selectedObject.Layer;

        // Parse and store wall height if applicable
        if (TxtPropHeight != null && double.TryParse(TxtPropHeight.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedHeight))
        {
            _selectedObject.Height = parsedHeight;
        }

        _selectedObject.BimProperties.Clear();
        foreach (var kvp in BimPropertiesList)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Key))
            {
                _selectedObject.SetBimProperty(kvp.Key, kvp.Value ?? "");
            }
        }

        if (_selectedObject.Height > 0)
        {
            _selectedObject.SetBimProperty("Height", _selectedObject.Height.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        }

        UpdateUiFromDoc();
        MessageBox.Show("BIM properties updated!", "Properties Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnDeleteObject_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedObjects.Count == 0) return;
        foreach (var obj in _selectedObjects)
        {
            _doc.TakeoffObjects.Remove(obj);
        }
        _selectedObjects.Clear();
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
            SelectZone(zone);
        }
    }

    private void SelectZone(Zone zone)
    {
        _selectedZone = zone;
        _selectedObjects.Clear();
        TxtZoneName.Text = zone.Name;
        TxtZoneCode.Text = zone.Code;
        ClearBimInspector();
        RedrawCanvas();
        TxtStatus.Text = $"Selected Zone: {zone.Name} ({zone.Code})";
    }

    private void BtnRenameZone_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedZone == null)
        {
            MessageBox.Show("Please select a zone from the list or layout to rename.", "Rename Zone", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string newName = TxtZoneName.Text.Trim();
        string newCode = TxtZoneCode.Text.Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            MessageBox.Show("Zone name cannot be empty.", "Rename Zone", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _selectedZone.Name = newName;
        if (!string.IsNullOrWhiteSpace(newCode))
        {
            _selectedZone.Code = newCode;
        }

        UpdateUiFromDoc();
        TxtStatus.Text = $"Renamed zone to '{_selectedZone.Name}' ({_selectedZone.Code}).";
    }

    private void BtnDeleteZone_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedZone == null) return;
        _doc.Zones.Remove(_selectedZone);
        _selectedZone = null;
        TxtZoneName.Text = "";
        TxtZoneCode.Text = "";
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

    public Rect CalculateDocumentBounds()
    {
        double minX = 0, minY = 0;
        double maxX = Math.Max(200, _doc.Width);
        double maxY = Math.Max(200, _doc.Height);

        bool hasPoints = false;
        void IncludePoint(Point2D pt)
        {
            if (!hasPoints)
            {
                minX = maxX = pt.X;
                minY = maxY = pt.Y;
                hasPoints = true;
            }
            else
            {
                minX = Math.Min(minX, pt.X);
                minY = Math.Min(minY, pt.Y);
                maxX = Math.Max(maxX, pt.X);
                maxY = Math.Max(maxY, pt.Y);
            }
        }

        foreach (var ent in _doc.Entities)
        {
            foreach (var pt in ent.Points) IncludePoint(pt);
        }
        foreach (var zone in _doc.Zones)
        {
            foreach (var pt in zone.Boundary) IncludePoint(pt);
        }
        foreach (var obj in _doc.TakeoffObjects)
        {
            foreach (var pt in obj.GeometryPoints) IncludePoint(pt);
        }

        if (!hasPoints)
        {
            return new Rect(0, 0, Math.Max(200, _doc.Width), Math.Max(200, _doc.Height));
        }

        // Expand bounds slightly to give border breathing room
        double w = Math.Max(50, maxX - minX);
        double h = Math.Max(50, maxY - minY);
        return new Rect(minX, minY, w, h);
    }

    private void ZoomFit()
    {
        double viewportW = ViewportGrid.ActualWidth > 0 ? ViewportGrid.ActualWidth : 900;
        double viewportH = ViewportGrid.ActualHeight > 0 ? ViewportGrid.ActualHeight : 650;

        var bounds = CalculateDocumentBounds();
        double pad = 40.0;
        double availW = Math.Max(100, viewportW - pad * 2);
        double availH = Math.Max(100, viewportH - pad * 2);

        double s = Math.Clamp(Math.Min(availW / bounds.Width, availH / bounds.Height), 0.05, 10.0);
        CanvasScale.ScaleX = s;
        CanvasScale.ScaleY = s;

        // Center the content bounds in the viewport
        CanvasTranslate.X = (viewportW - bounds.Width * s) / 2 - bounds.X * s;
        CanvasTranslate.Y = (viewportH - bounds.Height * s) / 2 - bounds.Y * s;

        RedrawCanvas();
    }

    private void BtnToggleZones_Click(object sender, RoutedEventArgs e)
    {
        _showZones = !_showZones;
        BtnToggleZones.Content = _showZones ? "Zones (On)" : "Zones (Off)";
        RedrawCanvas();
    }

    private void ChkGpuAcceleration_Changed(object sender, RoutedEventArgs e)
    {
        if (FastCanvas != null && ChkGpuAcceleration != null)
        {
            bool isGpu = ChkGpuAcceleration.IsChecked == true;
            FastCanvas.GpuAccelerationEnabled = isGpu;
            TxtStatus.Text = isGpu ? "GPU Hardware Acceleration Enabled (DirectX / BitmapCache)" : "Software Rendering Mode (CPU Fallback)";
            RedrawCanvas();
        }
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
    public string HierarchyPath { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public string QuantityStr { get; set; } = string.Empty;
    public string WallAreaStr { get; set; } = string.Empty;
    public string CostStr { get; set; } = string.Empty;
}
