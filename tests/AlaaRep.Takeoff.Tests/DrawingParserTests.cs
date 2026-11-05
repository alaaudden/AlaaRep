using System.Text;
using AlaaRep.Takeoff.Core.Models;
using AlaaRep.Takeoff.Core.Parsers;
using Xunit;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;

namespace AlaaRep.Takeoff.Tests;

public class DrawingParserTests
{
    [Fact]
    public void DxfDrawingReader_ParsesCadEntitiesAndLayers()
    {
        var dxf = new DxfFile();
        dxf.Entities.Add(new DxfLine(new DxfPoint(0, 0, 0), new DxfPoint(100, 0, 0)) { Layer = "WALL" });
        dxf.Entities.Add(new DxfLine(new DxfPoint(100, 0, 0), new DxfPoint(100, 50, 0)) { Layer = "WALL" });
        dxf.Entities.Add(new DxfCircle(new DxfPoint(50, 25, 0), 10) { Layer = "DOOR" });

        using var ms = new MemoryStream();
        dxf.Save(ms);
        ms.Position = 0;

        var reader = new DxfDrawingReader();
        var doc = reader.ReadDxf(ms, "FloorPlan.dxf");

        Assert.Equal("DXF", doc.FileType);
        Assert.True(doc.Entities.Count >= 3);
        Assert.Contains(doc.Entities, e => e.Layer == "WALL");
        Assert.Contains(doc.Entities, e => e.Layer == "DOOR" && e.Type == DrawingEntityType.Circle);
    }

    [Fact]
    public void BluebeamMarkupReader_ParsesCsvExportWithBimProperties()
    {
        string csv = @"Subject,Page Label,Label,Measurement,Area,Length,Material,Omniclass,UnitCost,Zone
Flooring,Page 1,Hardwood Oak,250 sq m,250,,White Oak,23-14 17 00,45.00,Zone 1
Partition,Page 1,Interior Wall,50 m,,50,Gypsum STC 50,23-13 25 11,60.00,Zone 2
Fire Sprinkler,Page 1,Pendant Head,12 count,,,,Fire Protection,120.00,Zone 1";

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var reader = new BluebeamMarkupReader();
        var markups = reader.ReadCsvMarkups(csv);

        Assert.Equal(3, markups.Count);

        var floor = markups[0];
        Assert.Equal("Hardwood Oak", floor.Name);
        Assert.Equal(TakeoffMeasurementType.Area, floor.MeasurementType);
        Assert.Equal("White Oak", floor.GetBimProperty("Material"));
        Assert.Equal("45.00", floor.GetBimProperty("UnitCost"));
        Assert.Equal("Zone 1", floor.ZoneId);

        var wall = markups[1];
        Assert.Equal("Interior Wall", wall.Name);
        Assert.Equal(TakeoffMeasurementType.Length, wall.MeasurementType);

        var sprinkler = markups[2];
        Assert.Equal("Pendant Head", sprinkler.Name);
        Assert.Equal(TakeoffMeasurementType.Count, sprinkler.MeasurementType);
    }

    [Fact]
    public void BluebeamMarkupReader_ParsesXmlAndToolSet()
    {
        string xml = @"<ToolSet Name=""BIM Structural"">
  <Tool Subject=""Concrete Column"" Label=""COL-01"" Type=""Count"" Material=""C35 Concrete"" Cost=""500"">
    <Points>100,200</Points>
  </Tool>
  <Tool Subject=""Slab Takeoff"" Label=""SLAB-A"" Type=""Area"" Material=""Post Tensioned"" Cost=""85"">
    <Points>0,0;100,0;100,100;0,100</Points>
  </Tool>
</ToolSet>";

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var reader = new BluebeamMarkupReader();
        var markups = reader.ReadXmlMarkups(xml);

        Assert.Equal(2, markups.Count);
        Assert.Equal("COL-01", markups[0].Name);
        Assert.Equal("C35 Concrete", markups[0].GetBimProperty("Material"));
        Assert.Equal("SLAB-A", markups[1].Name);
        Assert.Equal(TakeoffMeasurementType.Area, markups[1].MeasurementType);
        Assert.Equal(4, markups[1].GeometryPoints.Count);
    }

    [Fact]
    public void Verify_DrawingDocument_BackgroundImageProperties()
    {
        var doc = new DrawingDocument
        {
            FileName = "Architectural_Plan.png",
            FileType = "Raster",
            BackgroundImagePath = "/samples/architectural_plan_sample.png",
            BackgroundImageWidth = 1909,
            BackgroundImageHeight = 1005,
            BackgroundImageOpacity = 0.85
        };

        Assert.Equal("Raster", doc.FileType);
        Assert.Equal("/samples/architectural_plan_sample.png", doc.BackgroundImagePath);
        Assert.Equal(1909, doc.BackgroundImageWidth);
        Assert.Equal(1005, doc.BackgroundImageHeight);
        Assert.Equal(0.85, doc.BackgroundImageOpacity);
    }

    [Fact]
    public void Verify_Document_InitializesWithBackgroundMetadata()
    {
        var doc = new DrawingDocument();
        Assert.Null(doc.BackgroundImagePath);
        Assert.Equal(0.0, doc.BackgroundImageWidth);
        Assert.Equal(0.0, doc.BackgroundImageHeight);
        Assert.Equal(1.0, doc.BackgroundImageOpacity);

        doc.BackgroundImagePath = "plan.jpg";
        doc.BackgroundImageWidth = 3840;
        doc.BackgroundImageHeight = 2160;
        doc.Width = doc.BackgroundImageWidth;
        doc.Height = doc.BackgroundImageHeight;

        Assert.Equal(3840, doc.Width);
        Assert.Equal(2160, doc.Height);
    }
}
