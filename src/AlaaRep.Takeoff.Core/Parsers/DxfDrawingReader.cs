using IxMilia.Dxf;
using IxMilia.Dxf.Entities;
using AlaaRep.Takeoff.Core.Models;

namespace AlaaRep.Takeoff.Core.Parsers;

public class DxfDrawingReader
{
    public DrawingDocument ReadDxf(Stream stream, string fileName = "Drawing.dxf")
    {
        var doc = new DrawingDocument
        {
            FileName = fileName,
            FileType = "DXF"
        };

        DxfFile dxfFile;
        try
        {
            dxfFile = DxfFile.Load(stream);
        }
        catch (Exception ex)
        {
            throw new NotSupportedException($"Unable to parse CAD file '{fileName}'. If this is a binary DWG file, please save or export it as DXF (AutoCAD DXF format) to inspect vector layers: {ex.Message}", ex);
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        void TrackBounds(double x, double y)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y)) return;
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }

        var rawEntities = new List<DrawingEntity>();

        foreach (var entity in dxfFile.Entities)
        {
            if (entity is DxfLine line)
            {
                var de = new DrawingEntity
                {
                    Type = DrawingEntityType.Line,
                    Layer = line.Layer,
                    StrokeWidth = 1.0,
                    Points = new List<Point2D>
                    {
                        new(line.P1.X, line.P1.Y),
                        new(line.P2.X, line.P2.Y)
                    }
                };
                TrackBounds(line.P1.X, line.P1.Y);
                TrackBounds(line.P2.X, line.P2.Y);
                rawEntities.Add(de);
            }
            else if (entity is DxfLwPolyline lw)
            {
                var pts = lw.Vertices.Select(v => new Point2D(v.X, v.Y)).ToList();
                foreach (var p in pts) TrackBounds(p.X, p.Y);

                var de = new DrawingEntity
                {
                    Type = lw.IsClosed ? DrawingEntityType.Polygon : DrawingEntityType.Polyline,
                    Layer = lw.Layer,
                    StrokeWidth = 1.5,
                    IsClosed = lw.IsClosed,
                    Points = pts
                };
                rawEntities.Add(de);
            }
            else if (entity is DxfPolyline pl)
            {
                var pts = pl.Vertices.Select(v => new Point2D(v.Location.X, v.Location.Y)).ToList();
                foreach (var p in pts) TrackBounds(p.X, p.Y);

                var de = new DrawingEntity
                {
                    Type = DrawingEntityType.Polyline,
                    Layer = pl.Layer,
                    StrokeWidth = 1.0,
                    Points = pts
                };
                rawEntities.Add(de);
            }
            else if (entity is DxfCircle circle)
            {
                TrackBounds(circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius);
                TrackBounds(circle.Center.X + circle.Radius, circle.Center.Y + circle.Radius);

                var de = new DrawingEntity
                {
                    Type = DrawingEntityType.Circle,
                    Layer = circle.Layer,
                    Radius = circle.Radius,
                    Points = new List<Point2D> { new(circle.Center.X, circle.Center.Y) }
                };
                rawEntities.Add(de);
            }
            else if (entity is DxfText txt)
            {
                TrackBounds(txt.Location.X, txt.Location.Y);
                var de = new DrawingEntity
                {
                    Type = DrawingEntityType.Text,
                    Layer = txt.Layer,
                    Text = txt.Value,
                    TextSize = Math.Max(8.0, txt.TextHeight),
                    RotationAngle = txt.Rotation,
                    Points = new List<Point2D> { new(txt.Location.X, txt.Location.Y) }
                };
                rawEntities.Add(de);
            }
            else if (entity is DxfInsert insert)
            {
                TrackBounds(insert.Location.X, insert.Location.Y);
                var takeoff = new TakeoffObject
                {
                    Name = insert.Name ?? "Block Instance",
                    Category = insert.Layer ?? "Equipment",
                    MeasurementType = TakeoffMeasurementType.Count,
                    Source = "DXF_Block",
                    ColorHex = "#8B5CF6",
                    GeometryPoints = new List<Point2D> { new(insert.Location.X, insert.Location.Y) }
                };
                takeoff.SetBimProperty("CAD_Block", insert.Name ?? string.Empty);
                takeoff.SetBimProperty("Layer", insert.Layer ?? string.Empty);
                doc.TakeoffObjects.Add(takeoff);
            }
        }

        if (minX > maxX || minY > maxY)
        {
            minX = 0; minY = 0;
            maxX = 1000; maxY = 800;
        }

        double width = Math.Max(100, maxX - minX);
        double height = Math.Max(100, maxY - minY);
        double margin = Math.Max(width, height) * 0.05;

        doc.Width = width + margin * 2;
        doc.Height = height + margin * 2;

        foreach (var entity in rawEntities)
        {
            for (int i = 0; i < entity.Points.Count; i++)
            {
                var p = entity.Points[i];
                entity.Points[i] = new Point2D((p.X - minX) + margin, (maxY - p.Y) + margin);
            }
            doc.Entities.Add(entity);
        }

        foreach (var obj in doc.TakeoffObjects)
        {
            for (int i = 0; i < obj.GeometryPoints.Count; i++)
            {
                var p = obj.GeometryPoints[i];
                obj.GeometryPoints[i] = new Point2D((p.X - minX) + margin, (maxY - p.Y) + margin);
            }
        }

        return doc;
    }
}
