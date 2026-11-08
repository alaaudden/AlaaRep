using AlaaRep.Takeoff.Core.Models;
using AlaaRep.Takeoff.Core.Native;
using AlaaRep.Takeoff.Core.Services;
using Xunit;

namespace AlaaRep.Takeoff.Tests;

public class NativeSpatialEngineTests
{
    [Fact]
    public void Verify_NativeEngine_IsLoadedAndAvailable()
    {
        Assert.True(NativeSpatialEngine.IsAvailable, "Native C++ spatial engine shared library should be loaded and available.");
    }

    [Fact]
    public void Verify_NativePolygonArea_MatchesManaged()
    {
        // 100 x 50 rectangle = 5000
        var poly = new List<Point2D>
        {
            new(0, 0),
            new(100, 0),
            new(100, 50),
            new(0, 50)
        };

        bool ok = NativeSpatialEngine.TryCalculatePolygonArea(poly, out double nativeArea);
        Assert.True(ok);
        Assert.Equal(5000.0, nativeArea, 2);

        // Verify routing via SpatialCalculator
        double calcArea = SpatialCalculator.CalculatePolygonArea(poly);
        Assert.Equal(5000.0, calcArea, 2);
    }

    [Fact]
    public void Verify_NativePointInPolygon_MatchesManaged()
    {
        var poly = new List<Point2D>
        {
            new(10, 10),
            new(100, 10),
            new(100, 100),
            new(10, 100)
        };

        var insidePt = new Point2D(50, 50);
        var outsidePt = new Point2D(5, 5);

        bool ok1 = NativeSpatialEngine.TryIsPointInPolygon(insidePt, poly, out bool insideResult);
        Assert.True(ok1);
        Assert.True(insideResult);

        bool ok2 = NativeSpatialEngine.TryIsPointInPolygon(outsidePt, poly, out bool outsideResult);
        Assert.True(ok2);
        Assert.False(outsideResult);

        Assert.True(SpatialCalculator.IsPointInPolygon(insidePt, poly));
        Assert.False(SpatialCalculator.IsPointInPolygon(outsidePt, poly));
    }

    [Fact]
    public void Verify_NativePolylineLength_MatchesManaged()
    {
        var line = new List<Point2D>
        {
            new(0, 0),
            new(30, 40),   // dist = 50
            new(30, 140)   // dist = 100 -> total = 150
        };

        bool ok = NativeSpatialEngine.TryCalculatePolylineLength(line, out double nativeLen);
        Assert.True(ok);
        Assert.Equal(150.0, nativeLen, 2);

        double calcLen = SpatialCalculator.CalculatePolylineLength(line);
        Assert.Equal(150.0, calcLen, 2);
    }

    [Fact]
    public void Verify_NativeWallAreaInZone_MatchesManaged()
    {
        // Zone: (0,0) to (100, 100)
        var zone = new List<Point2D>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100)
        };

        // Line: (-50, 50) to (150, 50) -> 100 units inside the zone
        var wallLine = new List<Point2D>
        {
            new(-50, 50),
            new(150, 50)
        };

        double wallHeight = 3.5;

        bool ok = NativeSpatialEngine.TryCalculateWallAreaInZone(wallLine, wallHeight, zone, out double wallArea);
        Assert.True(ok);
        Assert.Equal(350.0, wallArea, 2); // 100 * 3.5 = 350.0
    }
}
