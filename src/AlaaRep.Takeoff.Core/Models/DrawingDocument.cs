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

    // High-resolution background raster image support
    public string? BackgroundImagePath { get; set; }
    public double BackgroundImageWidth { get; set; }
    public double BackgroundImageHeight { get; set; }
    public double BackgroundImageOpacity { get; set; } = 1.0;

    // BIM Project Hierarchy Tree (Project -> Building -> Floor -> Discipline -> Objects)
    public BimHierarchyNode HierarchyRoot { get; set; } = new()
    {
        Name = "Project Site",
        NodeType = HierarchyNodeType.Project,
        Code = "PRJ-01"
    };

    // Accelerated Spatial Index for O(log N) viewport frustum culling
    private AlaaRep.Takeoff.Core.Services.SpatialQuadTree<DrawingEntity>? _spatialIndex;

    /// <summary>
    /// Builds or rebuilds the spatial QuadTree index of drawing entities.
    /// Enables sub-millisecond frustum culling of 100,000+ CAD vector entities.
    /// </summary>
    public AlaaRep.Takeoff.Core.Services.SpatialQuadTree<DrawingEntity> GetOrBuildSpatialIndex()
    {
        if (_spatialIndex != null && _spatialIndex.TotalItemCount == Entities.Count)
        {
            return _spatialIndex;
        }

        var bounds = new AlaaRep.Takeoff.Core.Services.BoundingBox2D(0, 0, Math.Max(Width, 1000), Math.Max(Height, 1000));
        _spatialIndex = new AlaaRep.Takeoff.Core.Services.SpatialQuadTree<DrawingEntity>(bounds);

        foreach (var entity in Entities)
        {
            var entityBox = AlaaRep.Takeoff.Core.Services.BoundingBox2D.FromPoints(entity.Points);
            _spatialIndex.Insert(entityBox, entity);
        }

        return _spatialIndex;
    }

    /// <summary>
    /// Invalidates the spatial cache when entities are added, modified, or cleared.
    /// </summary>
    public void InvalidateSpatialIndex()
    {
        _spatialIndex = null;
    }
}
