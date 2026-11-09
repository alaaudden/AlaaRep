using Xunit;
using AlaaRep.Takeoff.Core.Models;
using AlaaRep.Takeoff.Core.Services;

namespace AlaaRep.Takeoff.Tests;

public class ZoneAndCalibrationTests
{
    [Fact]
    public void ScaleCalibration_UserDefinedRealDistance_UpdatesUnitsPerCoordinate()
    {
        var calib = new ScaleCalibration
        {
            CalibrationPixelDistance = 200.0,
            CalibrationRealDistance = 10.0,
            LinearUnit = "m",
            AreaUnit = "m²"
        };

        // 10m / 200px = 0.05 m/px
        Assert.Equal(0.05, calib.UnitsPerCoordinate, 4);

        // Update real distance as provided by user
        calib.CalibrationRealDistance = 20.0;
        // 20m / 200px = 0.10 m/px
        Assert.Equal(0.10, calib.UnitsPerCoordinate, 4);
    }

    [Fact]
    public void ZoneRenaming_PropagatesToQtoReport()
    {
        var engine = new TakeoffEngine();
        var doc = new DrawingDocument
        {
            Calibration = new ScaleCalibration
            {
                CalibrationPixelDistance = 100.0,
                CalibrationRealDistance = 5.0, // 0.05 m/px
                LinearUnit = "m",
                AreaUnit = "m²"
            }
        };

        var zone = new Zone
        {
            Id = "z-1",
            Name = "Initial Zone Name",
            Code = "ZN-01",
            Boundary = new List<Point2D> { new(0, 0), new(200, 0), new(200, 200), new(0, 200) }
        };
        doc.Zones.Add(zone);

        var obj = new TakeoffObject
        {
            Id = "obj-1",
            Name = "Office Fixture",
            MeasurementType = TakeoffMeasurementType.Count,
            GeometryPoints = new List<Point2D> { new(50, 50) }
        };
        doc.TakeoffObjects.Add(obj);

        var report1 = engine.CalculateReport(doc);
        Assert.Equal("Initial Zone Name", report1.Items[0].ZoneBreakdowns[0].ZoneName);

        // Rename the zone
        zone.Name = "Executive Conference Room";
        zone.Code = "CONF-01";

        var report2 = engine.CalculateReport(doc);
        Assert.Equal("Executive Conference Room", report2.Items[0].ZoneBreakdowns[0].ZoneName);
    }

    [Fact]
    public void LayerAssignment_UpdatesTakeoffObjectCategoryAndVisibility()
    {
        var obj = new TakeoffObject
        {
            Id = "obj-wall",
            Name = "Partition",
            Category = "General"
        };

        // Assign to new layer
        obj.Category = "Interior Partitions";
        obj.SetBimProperty("Layer", "Interior Partitions");

        Assert.Equal("Interior Partitions", obj.Category);
        Assert.Equal("Interior Partitions", obj.GetBimProperty("Layer"));
    }
}
