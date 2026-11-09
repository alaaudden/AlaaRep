namespace AlaaRep.Takeoff.Core.Models;

public enum SnapType
{
    None,
    Endpoint,
    Midpoint,
    Nearest
}

public class SnapResult
{
    public bool DidSnap { get; set; }
    public Point2D SnappedPoint { get; set; } = new(0, 0);
    public SnapType Type { get; set; } = SnapType.None;
    public string Description { get; set; } = string.Empty;
}
