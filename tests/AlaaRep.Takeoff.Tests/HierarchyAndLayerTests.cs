using Xunit;
using AlaaRep.Takeoff.Core.Models;
using AlaaRep.Takeoff.Core.Services;

namespace AlaaRep.Takeoff.Tests;

public class HierarchyAndLayerTests
{
    [Fact]
    public void BimHierarchyNode_AddBuildingAndFloor_EstablishesCorrectParenting()
    {
        var root = new BimHierarchyNode { Name = "Project", NodeType = HierarchyNodeType.Project };
        var bld1 = new BimHierarchyNode { Name = "Tower A", NodeType = HierarchyNodeType.Building };
        root.AddChild(bld1);

        var floor1 = new BimHierarchyNode { Name = "Level 01", NodeType = HierarchyNodeType.Floor };
        bld1.AddChild(floor1);

        var disc1 = new BimHierarchyNode { Name = "Architectural", NodeType = HierarchyNodeType.Discipline };
        floor1.AddChild(disc1);

        Assert.Equal(root.Id, bld1.ParentId);
        Assert.Equal(bld1.Id, floor1.ParentId);
        Assert.Equal(floor1.Id, disc1.ParentId);
        Assert.Single(root.Children);
        Assert.Single(bld1.Children);
        Assert.Single(floor1.Children);
    }

    [Fact]
    public void BimHierarchyNode_FindNode_RecursivelyFindsDescendant()
    {
        var root = new BimHierarchyNode { Name = "Root" };
        var bld = new BimHierarchyNode { Name = "Building" };
        var flr = new BimHierarchyNode { Name = "Floor" };
        var disc = new BimHierarchyNode { Name = "Discipline" };

        root.AddChild(bld);
        bld.AddChild(flr);
        flr.AddChild(disc);

        var found = root.FindNode(disc.Id);
        Assert.NotNull(found);
        Assert.Equal("Discipline", found.Name);
    }

    [Fact]
    public void LayerVisibility_DictionaryLookup_CorrectlyTogglesVisibility()
    {
        var layerVisibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["A-WALL-EXTR"] = true,
            ["A-WALL-INTR"] = false,
            ["Finishes"] = true
        };

        Assert.True(layerVisibility.TryGetValue("A-WALL-EXTR", out bool visExtr) && visExtr);
        Assert.True(layerVisibility.TryGetValue("A-WALL-INTR", out bool visIntr) && !visIntr);
        Assert.False(layerVisibility.TryGetValue("NON-EXISTENT", out _));
    }

    [Fact]
    public void ContentBoundingBox_CalculatesAccurateExtents()
    {
        var doc = new DrawingDocument();
        doc.Entities.Add(new DrawingEntity
        {
            Type = DrawingEntityType.Line,
            Points = new List<Point2D> { new(100, 200), new(500, 600) }
        });
        doc.Zones.Add(new Zone
        {
            Name = "Zone 1",
            Boundary = new List<Point2D> { new(50, 80), new(600, 80), new(600, 750), new(50, 750) }
        });

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        void Include(Point2D pt)
        {
            minX = Math.Min(minX, pt.X);
            minY = Math.Min(minY, pt.Y);
            maxX = Math.Max(maxX, pt.X);
            maxY = Math.Max(maxY, pt.Y);
        }

        foreach (var ent in doc.Entities) foreach (var pt in ent.Points) Include(pt);
        foreach (var z in doc.Zones) foreach (var pt in z.Boundary) Include(pt);

        Assert.Equal(50, minX);
        Assert.Equal(80, minY);
        Assert.Equal(600, maxX);
        Assert.Equal(750, maxY);
        Assert.Equal(550, maxX - minX);
        Assert.Equal(670, maxY - minY);
    }
}
