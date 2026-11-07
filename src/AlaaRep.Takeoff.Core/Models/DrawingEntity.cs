namespace AlaaRep.Takeoff.Core.Models;

public enum DrawingEntityType
{
    Line,
    Polyline,
    Polygon,
    Circle,
    Arc,
    Text,
    Path
}

public class DrawingEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DrawingEntityType Type { get; set; }
    public string? Layer { get; set; } = "0";
    public string? ColorHex { get; set; } = "#333333";
    public double StrokeWidth { get; set; } = 1.0;
    public List<Point2D> Points { get; set; } = new();
    public double Radius { get; set; }
    public string? Text { get; set; }
    public double TextSize { get; set; } = 12.0;
    public double RotationAngle { get; set; }
    public bool IsClosed { get; set; }
}
