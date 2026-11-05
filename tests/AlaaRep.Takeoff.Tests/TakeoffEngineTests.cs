using AlaaRep.Takeoff.Core.Models;
using AlaaRep.Takeoff.Core.Services;
using Xunit;

namespace AlaaRep.Takeoff.Tests;

public class TakeoffEngineTests
{
    [Fact]
    public void TakeoffEngine_CalculatesQuantitiesPerZoneAccurately()
    {
        var doc = new DrawingDocument
        {
            FileName = "Test Floor Plan",
            Calibration = new ScaleCalibration
            {
                CalibrationPixelDistance = 100.0,
                CalibrationRealDistance = 10.0 // 1px = 0.1m, area multiplier = 0.01
            }
        };

        // Zone 1: (0,0) to (100, 100) -> 10m x 10m = 100 m²
        doc.Zones.Add(new Zone
        {
            Id = "zone-1",
            Name = "Zone 1",
            Code = "Z1",
            Boundary = new List<Point2D> { new(0, 0), new(100, 0), new(100, 100), new(0, 100) }
        });

        // Zone 2: (100, 0) to (200, 100) -> 10m x 10m = 100 m²
        doc.Zones.Add(new Zone
        {
            Id = "zone-2",
            Name = "Zone 2",
            Code = "Z2",
            Boundary = new List<Point2D> { new(100, 0), new(200, 0), new(200, 100), new(100, 100) }
        });

        // Takeoff Item 1: Count of light fixtures (2 in Zone 1, 1 in Zone 2)
        var lights = new TakeoffObject
        {
            Id = "lights-1",
            Name = "LED Troffer",
            Category = "Electrical",
            MeasurementType = TakeoffMeasurementType.Count,
            GeometryPoints = new List<Point2D>
            {
                new(25, 25), // In Zone 1
                new(75, 75), // In Zone 1
                new(150, 50) // In Zone 2
            }
        };
        lights.SetBimProperty("UnitCost", "80");
        doc.TakeoffObjects.Add(lights);

        // Takeoff Item 2: Pipe line running across both zones (from x=50 to x=150 at y=50)
        // Length in Zone 1: 50px = 5.0m
        // Length in Zone 2: 50px = 5.0m
        var pipe = new TakeoffObject
        {
            Id = "pipe-1",
            Name = "Copper Pipe 2-inch",
            Category = "Plumbing",
            MeasurementType = TakeoffMeasurementType.Length,
            GeometryPoints = new List<Point2D> { new(50, 50), new(150, 50) }
        };
        pipe.SetBimProperty("UnitCost", "20");
        doc.TakeoffObjects.Add(pipe);

        // Run Takeoff Calculation Engine
        var engine = new TakeoffEngine();
        var report = engine.CalculateReport(doc);

        Assert.NotNull(report);
        Assert.Equal(2, report.Zones.Count);
        Assert.Equal(2, report.Items.Count);

        // Verify Zone 1 quantities
        var z1 = report.Zones.First(z => z.ZoneId == "zone-1");
        Assert.Equal(100.0, z1.ZoneArea, 1);
        Assert.Equal(2, z1.TotalObjectCount);
        Assert.Equal(5.0, z1.TotalLength, 1);

        // Verify Zone 2 quantities
        var z2 = report.Zones.First(z => z.ZoneId == "zone-2");
        Assert.Equal(100.0, z2.ZoneArea, 1);
        Assert.Equal(1, z2.TotalObjectCount);
        Assert.Equal(5.0, z2.TotalLength, 1);

        // Grand totals
        Assert.Equal(3, report.TotalItemCount);
        Assert.Equal(10.0, report.GrandTotalLength, 1);
        Assert.Equal(440.0, report.GrandTotalCost, 1);
    }

    [Fact]
    public void TakeoffObject_MoveAndClone_PreservesAndOffsetsGeometry()
    {
        var obj = new TakeoffObject
        {
            Name = "Wall Segment",
            Category = "Partitions",
            MeasurementType = TakeoffMeasurementType.Length,
            GeometryPoints = new List<Point2D> { new(10, 20), new(50, 60) }
        };
        obj.SetBimProperty("Material", "Gypsum");
        obj.SetBimProperty("UnitCost", "35.00");

        // Test MoveBy
        obj.MoveBy(100, 50);
        Assert.Equal(110.0, obj.GeometryPoints[0].X);
        Assert.Equal(70.0, obj.GeometryPoints[0].Y);
        Assert.Equal(150.0, obj.GeometryPoints[1].X);
        Assert.Equal(110.0, obj.GeometryPoints[1].Y);

        // Test Clone with offset
        var clone = obj.Clone(offset: 25.0);
        Assert.NotEqual(obj.Id, clone.Id);
        Assert.Contains("(Copy)", clone.Name);
        Assert.Equal(135.0, clone.GeometryPoints[0].X);
        Assert.Equal(95.0, clone.GeometryPoints[0].Y);
        Assert.Equal("Gypsum", clone.GetBimProperty("Material"));
        Assert.Equal("35.00", clone.GetBimProperty("UnitCost"));
    }

    [Fact]
    public void BimHierarchy_RollupAndObjectAssignment_CalculatesCostsPerHierarchyLevel()
    {
        var doc = new DrawingDocument();

        var bld = new BimHierarchyNode { Name = "HQ Tower", NodeType = HierarchyNodeType.Building };
        var floor = new BimHierarchyNode { Name = "Floor 3", NodeType = HierarchyNodeType.Floor };
        var disc = new BimHierarchyNode { Name = "HVAC", NodeType = HierarchyNodeType.Discipline };
        bld.AddChild(floor);
        floor.AddChild(disc);
        doc.HierarchyRoot.AddChild(bld);

        var obj1 = new TakeoffObject
        {
            Name = "Air Diffuser",
            MeasurementType = TakeoffMeasurementType.Count,
            Building = "HQ Tower",
            Floor = "Floor 3",
            Discipline = "HVAC",
            HierarchyNodeId = disc.Id,
            GeometryPoints = new List<Point2D> { new(10, 10), new(20, 20) } // 2 diffusers
        };
        obj1.SetBimProperty("UnitCost", "100.00"); // 2 * 100 = $200
        disc.TakeoffObjectIds.Add(obj1.Id);
        doc.TakeoffObjects.Add(obj1);

        var engine = new TakeoffEngine();
        var report = engine.CalculateReport(doc);

        Assert.Equal(200.0, report.QuantitiesByBuilding["HQ Tower"]);
        Assert.Equal(200.0, report.QuantitiesByFloor["Floor 3"]);
        Assert.Equal(200.0, report.QuantitiesByDiscipline["HVAC"]);

        // Verify GetAllContainedObjectIds
        var bldContained = bld.GetAllContainedObjectIds().ToList();
        Assert.Contains(obj1.Id, bldContained);
    }

    [Fact]
    public void BimHierarchy_NodeRenamingAndObjectAssociation()
    {
        var root = new BimHierarchyNode { Name = "Initial Building", Code = "B1" };
        root.Name = "Renamed Building 42";
        root.Code = "BLD-42";

        Assert.Equal("Renamed Building 42", root.Name);
        Assert.Equal("BLD-42", root.Code);

        // Associate multiple objects
        root.TakeoffObjectIds.Add("obj-101");
        root.TakeoffObjectIds.Add("obj-102");

        var allIds = root.GetAllContainedObjectIds().ToList();
        Assert.Equal(2, allIds.Count);
        Assert.Contains("obj-101", allIds);
        Assert.Contains("obj-102", allIds);
    }

    [Fact]
    public void BimHierarchy_Reparenting_ChangesNodeParentAndChildren()
    {
        var root = new BimHierarchyNode { Name = "Root", Id = "root" };
        var bld1 = new BimHierarchyNode { Name = "Bld 1", Id = "b1" };
        var bld2 = new BimHierarchyNode { Name = "Bld 2", Id = "b2" };
        var floor = new BimHierarchyNode { Name = "Floor 1", Id = "f1" };

        root.AddChild(bld1);
        root.AddChild(bld2);
        bld1.AddChild(floor);

        Assert.Equal("b1", floor.ParentId);
        Assert.Contains(floor, bld1.Children);

        // Re-parent floor from bld1 to bld2
        bld1.Children.Remove(floor);
        bld2.AddChild(floor);

        Assert.Equal("b2", floor.ParentId);
        Assert.Contains(floor, bld2.Children);
        Assert.DoesNotContain(floor, bld1.Children);
    }
}
