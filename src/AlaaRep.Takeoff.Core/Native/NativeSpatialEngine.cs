using System;
using System.IO;
using System.Runtime.InteropServices;
using AlaaRep.Takeoff.Core.Models;

namespace AlaaRep.Takeoff.Core.Native;

/// <summary>
/// High-performance C++ Native Geometry Engine Bridge.
/// Accelerates polygon area, ray-casting point containment, polyline length,
/// and zone boundary clipping using native compiled SIMD-optimized C++ algorithms.
/// Automatically falls back to managed algorithms if the native binary is not available.
/// </summary>
public static class NativeSpatialEngine
{
    private const string LibraryName = "takeoff_spatial";

    private static readonly Lazy<bool> _isAvailable = new(CheckNativeAvailability);

    public static bool IsAvailable => _isAvailable.Value;

    [DllImport(LibraryName, EntryPoint = "takeoff_native_polygon_area", CallingConvention = CallingConvention.Cdecl)]
    private static extern double NativePolygonArea(double[] coords, int pointCount);

    [DllImport(LibraryName, EntryPoint = "takeoff_native_polyline_length", CallingConvention = CallingConvention.Cdecl)]
    private static extern double NativePolylineLength(double[] coords, int pointCount);

    [DllImport(LibraryName, EntryPoint = "takeoff_native_point_in_polygon", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativePointInPolygon(double px, double py, double[] polyCoords, int polyPointCount);

    [DllImport(LibraryName, EntryPoint = "takeoff_native_polyline_length_in_zone", CallingConvention = CallingConvention.Cdecl)]
    private static extern double NativePolylineLengthInZone(
        double[] lineCoords, int linePointCount,
        double[] zoneCoords, int zonePointCount);

    [DllImport(LibraryName, EntryPoint = "takeoff_native_wall_area_in_zone", CallingConvention = CallingConvention.Cdecl)]
    private static extern double NativeWallAreaInZone(
        double[] lineCoords, int linePointCount, double wallHeight,
        double[] zoneCoords, int zonePointCount);

    [DllImport(LibraryName, EntryPoint = "takeoff_native_distance_to_segment", CallingConvention = CallingConvention.Cdecl)]
    private static extern double NativeDistanceToSegment(
        double px, double py,
        double vx, double vy,
        double wx, double wy,
        out double outProjX, out double outProjY);

    private static bool CheckNativeAvailability()
    {
        try
        {
            // Try resolving native library from local runtimes path if needed
            string baseDir = AppContext.BaseDirectory;
            string runtimesPath = Path.Combine(baseDir, "runtimes", "linux-x64", "native", "libtakeoff_spatial.so");
            if (File.Exists(runtimesPath))
            {
                NativeLibrary.TryLoad(runtimesPath, out _);
            }

            double[] dummy = { 0, 0, 10, 0, 10, 10, 0, 10 };
            double area = NativePolygonArea(dummy, 4);
            return Math.Abs(area - 100.0) < 1e-4;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryCalculatePolygonArea(IReadOnlyList<Point2D> polygon, out double area)
    {
        area = 0.0;
        if (!IsAvailable || polygon == null || polygon.Count < 3) return false;

        try
        {
            double[] coords = FlattenPoints(polygon);
            area = NativePolygonArea(coords, polygon.Count);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryCalculatePolylineLength(IReadOnlyList<Point2D> points, out double length)
    {
        length = 0.0;
        if (!IsAvailable || points == null || points.Count < 2) return false;

        try
        {
            double[] coords = FlattenPoints(points);
            length = NativePolylineLength(coords, points.Count);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryIsPointInPolygon(Point2D pt, IReadOnlyList<Point2D> polygon, out bool inside)
    {
        inside = false;
        if (!IsAvailable || polygon == null || polygon.Count < 3) return false;

        try
        {
            double[] coords = FlattenPoints(polygon);
            inside = NativePointInPolygon(pt.X, pt.Y, coords, polygon.Count) != 0;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryCalculatePolylineLengthInZone(
        IReadOnlyList<Point2D> polyline,
        IReadOnlyList<Point2D> zonePolygon,
        out double length)
    {
        length = 0.0;
        if (!IsAvailable || polyline == null || polyline.Count < 2 || zonePolygon == null || zonePolygon.Count < 3)
            return false;

        try
        {
            double[] lineCoords = FlattenPoints(polyline);
            double[] zoneCoords = FlattenPoints(zonePolygon);
            length = NativePolylineLengthInZone(lineCoords, polyline.Count, zoneCoords, zonePolygon.Count);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryCalculateWallAreaInZone(
        IReadOnlyList<Point2D> polyline,
        double wallHeight,
        IReadOnlyList<Point2D> zonePolygon,
        out double wallArea)
    {
        wallArea = 0.0;
        if (!IsAvailable || polyline == null || polyline.Count < 2 || zonePolygon == null || zonePolygon.Count < 3)
            return false;

        try
        {
            double[] lineCoords = FlattenPoints(polyline);
            double[] zoneCoords = FlattenPoints(zonePolygon);
            wallArea = NativeWallAreaInZone(lineCoords, polyline.Count, wallHeight, zoneCoords, zonePolygon.Count);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryDistanceToSegmentWithProjection(
        Point2D p, Point2D v, Point2D w,
        out Point2D projection, out double distance)
    {
        projection = v;
        distance = 0.0;
        if (!IsAvailable) return false;

        try
        {
            distance = NativeDistanceToSegment(p.X, p.Y, v.X, v.Y, w.X, w.Y, out double px, out double py);
            projection = new Point2D(px, py);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static double[] FlattenPoints(IReadOnlyList<Point2D> points)
    {
        double[] array = new double[points.Count * 2];
        for (int i = 0; i < points.Count; i++)
        {
            array[i * 2] = points[i].X;
            array[i * 2 + 1] = points[i].Y;
        }
        return array;
    }
}
