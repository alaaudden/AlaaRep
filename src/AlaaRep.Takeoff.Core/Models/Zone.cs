namespace AlaaRep.Takeoff.Core.Models;

public class Zone
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Zone 1";
    public string Code { get; set; } = "Z-01";
    public string Description { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#10B981";
    public double Opacity { get; set; } = 0.25;

    public List<Point2D> Boundary { get; set; } = new();

    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
