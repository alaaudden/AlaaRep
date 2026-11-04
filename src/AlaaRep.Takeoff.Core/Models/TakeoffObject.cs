namespace AlaaRep.Takeoff.Core.Models;

public enum TakeoffMeasurementType
{
    Count,
    Length,
    Area,
    Perimeter
}

public class TakeoffObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Takeoff Item";
    public string Category { get; set; } = "General";
    public TakeoffMeasurementType MeasurementType { get; set; } = TakeoffMeasurementType.Area;
    public string? ZoneId { get; set; }
    public string? ColorHex { get; set; } = "#3B82F6";
    public double Opacity { get; set; } = 0.4;
    public double StrokeWidth { get; set; } = 2.0;

    public List<Point2D> GeometryPoints { get; set; } = new();

    public Dictionary<string, string> BimProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Source { get; set; } = "User";
    public string? BluebeamSubject { get; set; }
    public string? BluebeamLabel { get; set; }

    public void SetBimProperty(string key, string value)
    {
        BimProperties[key] = value;
    }

    public string? GetBimProperty(string key)
    {
        return BimProperties.TryGetValue(key, out var val) ? val : null;
    }
}
