using System.Collections.ObjectModel;
using AlaaRep.Takeoff.Core.Models;
using AlaaRep.Takeoff.Core.Services;

namespace AlaaRep.Takeoff.Wpf;

public class TreeItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string GeometrySummary { get; set; } = string.Empty;
    public string? TagColor { get; set; }
    public bool IsObjectLeaf { get; set; }
    public TakeoffObject? AssociatedObject { get; set; }
    public BimHierarchyNode? AssociatedNode { get; set; }

    public ObservableCollection<TreeItemViewModel> Children { get; set; } = new();

    public static TreeItemViewModel FromNode(BimHierarchyNode node, IReadOnlyList<TakeoffObject> allObjects, ScaleCalibration? calib = null)
    {
        double scale = calib?.UnitsPerCoordinate ?? 0.05;
        string lUnit = calib?.LinearUnit ?? "m";
        string aUnit = calib?.AreaUnit ?? "m²";

        var vm = new TreeItemViewModel
        {
            Id = node.Id,
            Name = node.Name,
            TypeLabel = node.NodeType.ToString().ToUpper(),
            Code = node.Code ?? "",
            GeometrySummary = $"{node.GetAllContainedObjectIds().Count()} items",
            IsObjectLeaf = false,
            AssociatedNode = node
        };

        // Add sub-nodes
        foreach (var childNode in node.Children)
        {
            vm.Children.Add(FromNode(childNode, allObjects, calib));
        }

        // Add assigned takeoff geometry objects directly as children in the tree view
        foreach (var objId in node.TakeoffObjectIds)
        {
            var obj = allObjects.FirstOrDefault(o => o.Id == objId);
            if (obj != null)
            {
                string geomStr = "";
                if (obj.MeasurementType == TakeoffMeasurementType.Count)
                    geomStr = $"{obj.GeometryPoints.Count} pts";
                else if (obj.MeasurementType == TakeoffMeasurementType.Length)
                    geomStr = $"{SpatialCalculator.CalculatePolylineLength(obj.GeometryPoints) * scale:F1} {lUnit}";
                else
                    geomStr = $"{SpatialCalculator.CalculatePolygonArea(obj.GeometryPoints) * scale * scale:F1} {aUnit}";

                vm.Children.Add(new TreeItemViewModel
                {
                    Id = obj.Id,
                    Name = obj.Name,
                    TypeLabel = obj.MeasurementType.ToString(),
                    Code = obj.Category,
                    GeometrySummary = geomStr,
                    TagColor = obj.ColorHex ?? "#3B82F6",
                    IsObjectLeaf = true,
                    AssociatedObject = obj
                });
            }
        }

        return vm;
    }
}
