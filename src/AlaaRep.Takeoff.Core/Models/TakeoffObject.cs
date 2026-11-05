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
    private string _layer = "General";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Takeoff Item";
    public string Category { get; set; } = "General";

    public string Layer
    {
        get => !string.IsNullOrEmpty(_layer) ? _layer : Category;
        set
        {
            _layer = value;
            Category = value;
            BimProperties["Layer"] = value;
        }
    }

    public TakeoffMeasurementType MeasurementType { get; set; } = TakeoffMeasurementType.Area;
    public string? ZoneId { get; set; }
    public string? HierarchyNodeId { get; set; } // Position on the Building/Floor/Discipline tree
    public string? Building { get; set; }
    public string? Floor { get; set; }
    public string? Discipline { get; set; }
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

    public void MoveBy(double deltaX, double deltaY)
    {
        for (int i = 0; i < GeometryPoints.Count; i++)
        {
            var p = GeometryPoints[i];
            GeometryPoints[i] = new Point2D(p.X + deltaX, p.Y + deltaY);
        }
    }

    public TakeoffObject Clone(double offset = 20.0)
    {
        var clone = new TakeoffObject
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"{Name} (Copy)",
            Layer = Layer,
            Category = Category,
            MeasurementType = MeasurementType,
            ZoneId = ZoneId,
            ColorHex = ColorHex,
            Opacity = Opacity,
            StrokeWidth = StrokeWidth,
            Source = "User/Clone",
            BluebeamSubject = BluebeamSubject,
            BluebeamLabel = BluebeamLabel,
            GeometryPoints = GeometryPoints.Select(p => new Point2D(p.X + offset, p.Y + offset)).ToList(),
            BimProperties = new Dictionary<string, string>(BimProperties, StringComparer.OrdinalIgnoreCase)
        };
        return clone;
    }
}
