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
}
