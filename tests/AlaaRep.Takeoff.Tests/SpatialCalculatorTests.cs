using AlaaRep.Takeoff.Core.Models;
using AlaaRep.Takeoff.Core.Services;
using Xunit;

namespace AlaaRep.Takeoff.Tests;

public class SpatialCalculatorTests
{
    [Fact]
    public void IsPointInPolygon_IdentifiesInsideAndOutsidePoints()
    {
        var square = new List<Point2D>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100)
        };

        Assert.True(SpatialCalculator.IsPointInPolygon(new Point2D(50, 50), square));
        Assert.True(SpatialCalculator.IsPointInPolygon(new Point2D(10, 10), square));
        Assert.False(SpatialCalculator.IsPointInPolygon(new Point2D(150, 50), square));
        Assert.False(SpatialCalculator.IsPointInPolygon(new Point2D(-10, 50), square));
    }

    [Fact]
    public void CalculatePolygonArea_ComputesShoelaceAreaCorrectly()
    {
        var rect = new List<Point2D>
        {
            new(0, 0),
            new(200, 0),
            new(200, 100),
            new(0, 100)
        };

        double area = SpatialCalculator.CalculatePolygonArea(rect);
        Assert.Equal(20000.0, area, 1);
    }

    [Fact]
    public void CalculatePerimeter_ComputesPolygonBoundaryLength()
    {
        var rect = new List<Point2D>
        {
            new(0, 0),
            new(100, 0),
            new(100, 50),
            new(0, 50)
        };

        double perimeter = SpatialCalculator.CalculatePerimeter(rect);
        Assert.Equal(300.0, perimeter, 1);
    }

    [Fact]
    public void CalculatePolylineLength_SumsSegmentDistances()
    {
        var polyline = new List<Point2D>
        {
            new(0, 0),
            new(30, 40),
            new(30, 90)
        };

        double length = SpatialCalculator.CalculatePolylineLength(polyline);
        Assert.Equal(100.0, length, 1);
    }

    [Fact]
    public void CalculatePolylineLengthInZone_ClipsLineAgainstZoneBoundary()
    {
        var zone = new List<Point2D>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100)
        };

        var line = new List<Point2D>
        {
            new(-50, 50),
            new(150, 50)
        };

        double insideLength = SpatialCalculator.CalculatePolylineLengthInZone(line, zone);
        Assert.Equal(100.0, insideLength, 1);
    }

    [Fact]
    public void CalculatePolygonAreaInZone_ClipsAreaAgainstZone()
    {
        var zone = new List<Point2D>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100)
        };

        var subject = new List<Point2D>
        {
            new(50, 0),
            new(150, 0),
            new(150, 100),
            new(50, 100)
        };

        double clippedArea = SpatialCalculator.CalculatePolygonAreaInZone(subject, zone);
        Assert.Equal(5000.0, clippedArea, 1);
    }

    [Fact]
    public void CoordinateTransformation_ConvertsScreenToDrawingAndBackAccurately()
    {
        double panX = 120.5;
        double panY = -45.0;
        double scale = 1.75;

        var originalDrawingPoint = new Point2D(350.25, 620.75);

        // Transform to screen coordinates
        var screenPoint = SpatialCalculator.DrawingToScreen(originalDrawingPoint, panX, panY, scale);

        // Expected screen: drawX * scale + panX
        Assert.Equal(350.25 * 1.75 + 120.5, screenPoint.X, 3);
        Assert.Equal(620.75 * 1.75 - 45.0, screenPoint.Y, 3);

        // Transform back from screen to drawing coordinates
        var backToDrawing = SpatialCalculator.ScreenToDrawing(screenPoint, panX, panY, scale);

        // The picked position must match the original drawing coordinate exactly
        Assert.Equal(originalDrawingPoint.X, backToDrawing.X, 4);
        Assert.Equal(originalDrawingPoint.Y, backToDrawing.Y, 4);
    }

    [Fact]
    public void DistanceToSegment_IdentifiesProximityForPicking()
    {
        var v = new Point2D(100, 100);
        var w = new Point2D(200, 100);

        // Point exactly on segment
        Assert.Equal(0.0, SpatialCalculator.DistanceToSegment(new Point2D(150, 100), v, w), 3);

        // Point 5 units above segment
        Assert.Equal(5.0, SpatialCalculator.DistanceToSegment(new Point2D(150, 105), v, w), 3);

        // Point before segment start (distance to v)
        Assert.Equal(10.0, SpatialCalculator.DistanceToSegment(new Point2D(90, 100), v, w), 3);
    }
}
