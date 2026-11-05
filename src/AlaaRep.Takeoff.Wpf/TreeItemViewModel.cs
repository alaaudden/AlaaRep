using System.Collections.ObjectModel;
using AlaaRep.Takeoff.Core.Models;

namespace AlaaRep.Takeoff.Wpf;

public class TreeItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? TagColor { get; set; }
    public bool IsObjectLeaf { get; set; }
    public TakeoffObject? AssociatedObject { get; set; }
    public BimHierarchyNode? AssociatedNode { get; set; }

    public ObservableCollection<TreeItemViewModel> Children { get; set; } = new();

    public static TreeItemViewModel FromNode(BimHierarchyNode node, IReadOnlyList<TakeoffObject> allObjects)
    {
        var vm = new TreeItemViewModel
        {
            Id = node.Id,
            Name = node.Name,
            TypeLabel = node.NodeType.ToString(),
            Code = node.Code ?? "",
            IsObjectLeaf = false,
            AssociatedNode = node
        };

        // Add sub-nodes
        foreach (var childNode in node.Children)
        {
            vm.Children.Add(FromNode(childNode, allObjects));
        }

        // Add assigned takeoff objects directly as children in the tree view
        foreach (var objId in node.TakeoffObjectIds)
        {
            var obj = allObjects.FirstOrDefault(o => o.Id == objId);
            if (obj != null)
            {
                vm.Children.Add(new TreeItemViewModel
                {
                    Id = obj.Id,
                    Name = obj.Name,
                    TypeLabel = obj.MeasurementType.ToString(),
                    Code = obj.Category,
                    TagColor = obj.ColorHex,
                    IsObjectLeaf = true,
                    AssociatedObject = obj
                });
            }
        }

        return vm;
    }
}
