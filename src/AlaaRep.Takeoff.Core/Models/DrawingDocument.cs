namespace AlaaRep.Takeoff.Core.Models;

public class DrawingDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "Untitled Drawing";
    public string FileType { get; set; } = "PDF";
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 800;

    public List<DrawingEntity> Entities { get; set; } = new();
    public List<Zone> Zones { get; set; } = new();
    public List<TakeoffObject> TakeoffObjects { get; set; } = new();
    public ScaleCalibration Calibration { get; set; } = new();
    public string? BackgroundDataUrl { get; set; }

    // BIM Project Hierarchy Tree (Project -> Building -> Floor -> Discipline -> Objects)
    public BimHierarchyNode HierarchyRoot { get; set; } = new()
    {
        Name = "Project Site",
        NodeType = HierarchyNodeType.Project,
        Code = "PRJ-01"
    };
}
