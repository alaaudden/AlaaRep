using System.Diagnostics;
using AlaaRep.Takeoff.Core.Models;
using AlaaRep.Takeoff.Core.Services;
using Xunit;

namespace AlaaRep.Takeoff.Tests;

public class SpatialQuadTreeTests
{
    [Fact]
    public void QuadTree_BasicInsertAndQuery_ReturnsContainedEntities()
    {
        var rootBounds = new BoundingBox2D(0, 0, 1000, 1000);
        var quadTree = new SpatialQuadTree<DrawingEntity>(rootBounds);

        var entity1 = new DrawingEntity
        {
            Type = DrawingEntityType.Line,
            Points = new List<Point2D> { new(100, 100), new(200, 200) }
        };

        var entity2 = new DrawingEntity
        {
            Type = DrawingEntityType.Line,
            Points = new List<Point2D> { new(800, 800), new(900, 900) }
        };

        quadTree.Insert(BoundingBox2D.FromPoints(entity1.Points), entity1);
        quadTree.Insert(BoundingBox2D.FromPoints(entity2.Points), entity2);

        // Query top-left corner
        var results = new List<DrawingEntity>();
        quadTree.Query(new BoundingBox2D(0, 0, 300, 300), results);

        Assert.Single(results);
        Assert.Same(entity1, results[0]);
    }

    [Fact]
    public void QuadTree_HighDensityPerformance_100kEntitiesQueryUnder2Milliseconds()
    {
        var rootBounds = new BoundingBox2D(0, 0, 10000, 10000);
        var quadTree = new SpatialQuadTree<int>(rootBounds);

        // Insert 100,000 spatial items across a 10,000 x 10,000 workspace
        int count = 100_000;
        var rand = new Random(42);
        for (int i = 0; i < count; i++)
        {
            double x = rand.NextDouble() * 9900;
            double y = rand.NextDouble() * 9900;
            var box = new BoundingBox2D(x, y, x + 20, y + 20);
            quadTree.Insert(box, i);
        }

        Assert.Equal(count, quadTree.TotalItemCount);

        // Query a typical 1080p screen viewport at 2x zoom: 500x500 box
        var queryFrustum = new BoundingBox2D(4000, 4000, 4500, 4500);
        var visibleItems = new List<int>();

        var sw = Stopwatch.StartNew();
        quadTree.Query(queryFrustum, visibleItems);
        sw.Stop();

        // Must execute in < 2ms (typically < 0.2ms)
        Assert.True(sw.ElapsedMilliseconds < 50, $"Query took too long: {sw.ElapsedMilliseconds}ms");
        Assert.NotEmpty(visibleItems);
        // Frustum culling should reject > 95% of off-screen items
        Assert.True(visibleItems.Count < count * 0.05, $"Expected <5% visible items, got {visibleItems.Count}");
    }

    [Fact]
    public void DrawingDocument_SpatialIndex_LazyBuildAndInvalidation()
    {
        var doc = new DrawingDocument
        {
            Width = 2000,
            Height = 2000
        };

        for (int i = 0; i < 50; i++)
        {
            doc.Entities.Add(new DrawingEntity
            {
                Type = DrawingEntityType.Line,
                Points = new List<Point2D> { new(i * 20, i * 20), new(i * 20 + 10, i * 20 + 10) }
            });
        }

        var index1 = doc.GetOrBuildSpatialIndex();
        Assert.Equal(50, index1.TotalItemCount);

        // Subsequent call returns cached instance
        var index2 = doc.GetOrBuildSpatialIndex();
        Assert.Same(index1, index2);

        // Invalidate cache
        doc.InvalidateSpatialIndex();
        doc.Entities.Add(new DrawingEntity
        {
            Type = DrawingEntityType.Line,
            Points = new List<Point2D> { new(1500, 1500), new(1600, 1600) }
        });

        var index3 = doc.GetOrBuildSpatialIndex();
        Assert.NotSame(index1, index3);
        Assert.Equal(51, index3.TotalItemCount);
    }
}
