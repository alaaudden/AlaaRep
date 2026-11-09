using AlaaRep.Takeoff.Core.Models;

namespace AlaaRep.Takeoff.Core.Services;

/// <summary>
/// A 2D bounding box representing an axis-aligned bounding box (AABB).
/// </summary>
public readonly struct BoundingBox2D
{
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }

    public BoundingBox2D(double minX, double minY, double maxX, double maxY)
    {
        MinX = Math.Min(minX, maxX);
        MinY = Math.Min(minY, maxY);
        MaxX = Math.Max(minX, maxX);
        MaxY = Math.Max(minY, maxY);
    }

    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;

    public bool Intersects(BoundingBox2D other)
    {
        return !(MaxX < other.MinX || MinX > other.MaxX ||
                 MaxY < other.MinY || MinY > other.MaxY);
    }

    public bool Contains(Point2D pt)
    {
        return pt.X >= MinX && pt.X <= MaxX && pt.Y >= MinY && pt.Y <= MaxY;
    }

    public static BoundingBox2D FromPoints(IEnumerable<Point2D> points)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool hasPoint = false;

        foreach (var p in points)
        {
            hasPoint = true;
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        return hasPoint ? new BoundingBox2D(minX, minY, maxX, maxY) : new BoundingBox2D(0, 0, 0, 0);
    }
}

/// <summary>
/// High-performance spatial indexing structure (QuadTree) for 2D CAD entities.
/// Enables O(log N) viewport frustum culling and collision detection across hundreds of thousands of entities,
/// matching the spatial partitioning engines used by AutoCAD and Bluebeam.
/// </summary>
/// <typeparam name="T">The indexed item type</typeparam>
public class SpatialQuadTree<T>
{
    private const int MaxElementsPerNode = 32;
    private const int MaxTreeDepth = 8;

    private readonly BoundingBox2D _bounds;
    private readonly int _depth;
    private readonly List<(BoundingBox2D Bounds, T Item)> _items = new();
    private SpatialQuadTree<T>[]? _children;

    public SpatialQuadTree(BoundingBox2D bounds, int depth = 0)
    {
        _bounds = bounds;
        _depth = depth;
    }

    public int TotalItemCount
    {
        get
        {
            int count = _items.Count;
            if (_children != null)
            {
                foreach (var child in _children)
                {
                    count += child.TotalItemCount;
                }
            }
            return count;
        }
    }

    public void Insert(BoundingBox2D itemBounds, T item)
    {
        if (!_bounds.Intersects(itemBounds))
        {
            // If completely outside root bounds, still keep at root level to prevent losing entities
            if (_depth == 0)
            {
                _items.Add((itemBounds, item));
            }
            return;
        }

        if (_children != null)
        {
            int targetChild = GetQuadrant(itemBounds);
            if (targetChild != -1)
            {
                _children[targetChild].Insert(itemBounds, item);
                return;
            }
        }

        _items.Add((itemBounds, item));

        if (_items.Count > MaxElementsPerNode && _depth < MaxTreeDepth && _children == null)
        {
            Subdivide();
            // Re-distribute existing items into children where possible
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                int target = GetQuadrant(_items[i].Bounds);
                if (target != -1)
                {
                    _children![target].Insert(_items[i].Bounds, _items[i].Item);
                    _items.RemoveAt(i);
                }
            }
        }
    }

    private void Subdivide()
    {
        double midX = (_bounds.MinX + _bounds.MaxX) * 0.5;
        double midY = (_bounds.MinY + _bounds.MaxY) * 0.5;

        _children = new SpatialQuadTree<T>[4]
        {
            new(new BoundingBox2D(_bounds.MinX, _bounds.MinY, midX, midY), _depth + 1), // NW
            new(new BoundingBox2D(midX, _bounds.MinY, _bounds.MaxX, midY), _depth + 1), // NE
            new(new BoundingBox2D(_bounds.MinX, midY, midX, _bounds.MaxY), _depth + 1), // SW
            new(new BoundingBox2D(midX, midY, _bounds.MaxX, _bounds.MaxY), _depth + 1)  // SE
        };
    }

    private int GetQuadrant(BoundingBox2D box)
    {
        if (_children == null) return -1;
        double midX = (_bounds.MinX + _bounds.MaxX) * 0.5;
        double midY = (_bounds.MinY + _bounds.MaxY) * 0.5;

        bool top = box.MaxY <= midY;
        bool bottom = box.MinY >= midY;
        bool left = box.MaxX <= midX;
        bool right = box.MinX >= midX;

        if (left && top) return 0;
        if (right && top) return 1;
        if (left && bottom) return 2;
        if (right && bottom) return 3;

        return -1; // Straddles quadrants
    }

    /// <summary>
    /// Queries all items whose bounding box intersects the given query/frustum area.
    /// Used for sub-millisecond viewport frustum culling.
    /// </summary>
    public void Query(BoundingBox2D queryArea, List<T> results)
    {
        if (!_bounds.Intersects(queryArea)) return;

        foreach (var (box, item) in _items)
        {
            if (box.Intersects(queryArea))
            {
                results.Add(item);
            }
        }

        if (_children != null)
        {
            foreach (var child in _children)
            {
                child.Query(queryArea, results);
            }
        }
    }

    /// <summary>
    /// Clears the tree and all children.
    /// </summary>
    public void Clear()
    {
        _items.Clear();
        _children = null;
    }
}
