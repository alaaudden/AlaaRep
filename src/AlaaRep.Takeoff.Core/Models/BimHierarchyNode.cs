namespace AlaaRep.Takeoff.Core.Models;

public enum HierarchyNodeType
{
    Project,
    Building,
    Floor,
    Discipline, // e.g. Architectural, Structural, Mechanical, Electrical, Plumbing, FireProtection
    Zone,       // e.g. Zone A, Room 101
    Category,   // e.g. Walls, Flooring, Fixtures
    ObjectItem
}

public class BimHierarchyNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Node";
    public HierarchyNodeType NodeType { get; set; } = HierarchyNodeType.Building;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? ParentId { get; set; }

    // Child nodes in the hierarchy tree (e.g. Building -> Floor -> Discipline -> Objects)
    public List<BimHierarchyNode> Children { get; set; } = new();

    // IDs of takeoff objects directly belonging to this node
    public List<string> TakeoffObjectIds { get; set; } = new();

    public void AddChild(BimHierarchyNode child)
    {
        child.ParentId = Id;
        Children.Add(child);
    }

    public BimHierarchyNode? FindNode(string nodeId)
    {
        if (Id == nodeId) return this;
        foreach (var child in Children)
        {
            var found = child.FindNode(nodeId);
            if (found != null) return found;
        }
        return null;
    }

    public IEnumerable<string> GetAllContainedObjectIds()
    {
        var ids = new HashSet<string>(TakeoffObjectIds);
        foreach (var child in Children)
        {
            foreach (var childId in child.GetAllContainedObjectIds())
            {
                ids.Add(childId);
            }
        }
        return ids;
    }
}
