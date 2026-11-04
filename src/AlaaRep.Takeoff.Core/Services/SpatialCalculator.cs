using AlaaRep.Takeoff.Core.Models;

namespace AlaaRep.Takeoff.Core.Services;

public static class SpatialCalculator
{
    private const double Epsilon = 1e-9;

    public static bool IsPointInPolygon(Point2D pt, IReadOnlyList<Point2D> polygon)
    {
        if (polygon == null || polygon.Count < 3) return false;

        bool inside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            if (((pi.Y > pt.Y) != (pj.Y > pt.Y)) &&
                (pt.X < (pj.X - pi.X) * (pt.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    public static double CalculatePolygonArea(IReadOnlyList<Point2D> polygon)
    {
        if (polygon == null || polygon.Count < 3) return 0.0;

        double sum = 0.0;
        int n = polygon.Count;
        for (int i = 0; i < n; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % n];
            sum += (p1.X * p2.Y) - (p2.X * p1.Y);
        }

        return Math.Abs(sum) * 0.5;
    }

    public static double CalculatePerimeter(IReadOnlyList<Point2D> polygon)
    {
        if (polygon == null || polygon.Count < 2) return 0.0;

        double perimeter = 0.0;
        int n = polygon.Count;
        for (int i = 0; i < n; i++)
        {
            perimeter += polygon[i].DistanceTo(polygon[(i + 1) % n]);
        }

        return perimeter;
    }

    public static double CalculatePolylineLength(IReadOnlyList<Point2D> points)
    {
        if (points == null || points.Count < 2) return 0.0;

        double length = 0.0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            length += points[i].DistanceTo(points[i + 1]);
        }

        return length;
    }

    public static double CalculatePolylineLengthInZone(IReadOnlyList<Point2D> polyline, IReadOnlyList<Point2D> zonePolygon)
    {
        if (polyline == null || polyline.Count < 2 || zonePolygon == null || zonePolygon.Count < 3)
            return 0.0;

        double totalInsideLength = 0.0;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            var p1 = polyline[i];
            var p2 = polyline[i + 1];
            totalInsideLength += CalculateSegmentLengthInZone(p1, p2, zonePolygon);
        }

        return totalInsideLength;
    }

    public static double CalculateSegmentLengthInZone(Point2D p1, Point2D p2, IReadOnlyList<Point2D> zonePolygon)
    {
        double segLength = p1.DistanceTo(p2);
        if (segLength < Epsilon)
        {
            return IsPointInPolygon(p1, zonePolygon) ? 0.0 : 0.0;
        }

        var tValues = new List<double> { 0.0, 1.0 };
        int n = zonePolygon.Count;

        for (int i = 0; i < n; i++)
        {
            var q1 = zonePolygon[i];
            var q2 = zonePolygon[(i + 1) % n];

            if (TryIntersectSegments(p1, p2, q1, q2, out double t, out _))
            {
                if (t > Epsilon && t < 1.0 - Epsilon)
                {
                    tValues.Add(t);
                }
            }
        }

        tValues.Sort();

        double insideLength = 0.0;
        for (int i = 0; i < tValues.Count - 1; i++)
        {
            double tStart = tValues[i];
            double tEnd = tValues[i + 1];
            if (tEnd - tStart < Epsilon) continue;

            double tMid = (tStart + tEnd) * 0.5;
            var midPoint = new Point2D(
                p1.X + tMid * (p2.X - p1.X),
                p1.Y + tMid * (p2.Y - p1.Y)
            );

            if (IsPointInPolygon(midPoint, zonePolygon))
            {
                insideLength += (tEnd - tStart) * segLength;
            }
        }

        return insideLength;
    }

    public static double CalculatePolygonAreaInZone(IReadOnlyList<Point2D> subjectPoly, IReadOnlyList<Point2D> zonePoly)
    {
        if (subjectPoly == null || subjectPoly.Count < 3 || zonePoly == null || zonePoly.Count < 3)
            return 0.0;

        var b1 = GetBoundingBox(subjectPoly);
        var b2 = GetBoundingBox(zonePoly);
        if (b1.maxX < b2.minX || b1.minX > b2.maxX || b1.maxY < b2.minY || b1.minY > b2.maxY)
        {
            return 0.0;
        }

        var outputList = new List<Point2D>(subjectPoly);
        int zoneCount = zonePoly.Count;

        for (int i = 0; i < zoneCount; i++)
        {
            if (outputList.Count == 0) break;

            var cp1 = zonePoly[i];
            var cp2 = zonePoly[(i + 1) % zoneCount];

            var inputList = new List<Point2D>(outputList);
            outputList.Clear();

            if (inputList.Count == 0) break;

            var s = inputList[^1];
            foreach (var e in inputList)
            {
                if (IsInsideEdge(e, cp1, cp2))
                {
                    if (IsInsideEdge(s, cp1, cp2))
                    {
                        outputList.Add(e);
                    }
                    else
                    {
                        outputList.Add(Intersection(s, e, cp1, cp2));
                        outputList.Add(e);
                    }
                }
                else if (IsInsideEdge(s, cp1, cp2))
                {
                    outputList.Add(Intersection(s, e, cp1, cp2));
                }
                s = e;
            }
        }

        double clippedArea = CalculatePolygonArea(outputList);
        if (clippedArea > Epsilon) return clippedArea;

        var centroid = ComputeCentroid(subjectPoly);
        if (IsPointInPolygon(centroid, zonePoly))
        {
            return CalculatePolygonArea(subjectPoly);
        }

        return 0.0;
    }

    public static Point2D ComputeCentroid(IReadOnlyList<Point2D> points)
    {
        if (points == null || points.Count == 0) return new Point2D(0, 0);
        double sx = 0, sy = 0;
        foreach (var p in points)
        {
            sx += p.X;
            sy += p.Y;
        }
        return new Point2D(sx / points.Count, sy / points.Count);
    }

    // Coordinate & Picking Transform Math (Universal for Canvas & WPF)
    public static Point2D ScreenToDrawing(Point2D screenPt, double panX, double panY, double scale)
    {
        return new Point2D((screenPt.X - panX) / scale, (screenPt.Y - panY) / scale);
    }

    public static Point2D DrawingToScreen(Point2D drawPt, double panX, double panY, double scale)
    {
        return new Point2D(drawPt.X * scale + panX, drawPt.Y * scale + panY);
    }

    public static double DistanceToSegment(Point2D p, Point2D v, Point2D w)
    {
        return DistanceToSegmentWithProjection(p, v, w, out _);
    }

    public static double DistanceToSegmentWithProjection(Point2D p, Point2D v, Point2D w, out Point2D projection)
    {
        double l2 = (w.X - v.X) * (w.X - v.X) + (w.Y - v.Y) * (w.Y - v.Y);
        if (l2 < Epsilon)
        {
            projection = v;
            return p.DistanceTo(v);
        }
        double t = ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2;
        t = Math.Max(0, Math.Min(1, t));
        projection = new Point2D(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y));
        return p.DistanceTo(projection);
    }

    /// <summary>
    /// Geometric Object Snapping (OSnap) engine.
    /// Evaluates cursor position against drawing entities, zones, and takeoff objects.
    /// Snaps to Endpoints/Vertices, Midpoints, and Nearest points on lines.
    /// </summary>
    public static SnapResult FindSnapPoint(Point2D rawPoint, DrawingDocument doc, double snapTolerance = 15.0)
    {
        var result = new SnapResult { DidSnap = false, SnappedPoint = rawPoint };
        if (doc == null) return result;

        double bestDistance = snapTolerance;

        void TestCandidate(Point2D pt, SnapType type, string desc)
        {
            double dist = rawPoint.DistanceTo(pt);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                result.DidSnap = true;
                result.SnappedPoint = pt;
                result.Type = type;
                result.Description = desc;
            }
        }

        // 1. Endpoint / Vertex Snapping (Highest priority)
        foreach (var obj in doc.TakeoffObjects)
        {
            foreach (var pt in obj.GeometryPoints)
            {
                TestCandidate(pt, SnapType.Endpoint, $"Takeoff Vertex: {obj.Name}");
            }
        }

        foreach (var zone in doc.Zones)
        {
            foreach (var pt in zone.Boundary)
            {
                TestCandidate(pt, SnapType.Endpoint, $"Zone Vertex: {zone.Name}");
            }
        }

        foreach (var ent in doc.Entities)
        {
            foreach (var pt in ent.Points)
            {
                TestCandidate(pt, SnapType.Endpoint, "CAD Entity Vertex");
            }
        }

        // If an endpoint snap was found within tight tolerance, return immediately
        if (result.DidSnap && result.Type == SnapType.Endpoint && bestDistance < snapTolerance * 0.5)
        {
            return result;
        }

        // 2. Midpoint Snapping (Higher priority than Nearest)
        void TestMidpoints(IReadOnlyList<Point2D> pts, bool isClosed, string label)
        {
            if (pts.Count < 2) return;
            int count = isClosed ? pts.Count : pts.Count - 1;
            for (int i = 0; i < count; i++)
            {
                var p1 = pts[i];
                var p2 = pts[(i + 1) % pts.Count];
                var mid = new Point2D((p1.X + p2.X) * 0.5, (p1.Y + p2.Y) * 0.5);
                TestCandidate(mid, SnapType.Midpoint, $"Midpoint: {label}");
            }
        }

        foreach (var obj in doc.TakeoffObjects)
        {
            if (obj.MeasurementType == TakeoffMeasurementType.Length)
                TestMidpoints(obj.GeometryPoints, false, obj.Name);
            else if (obj.MeasurementType == TakeoffMeasurementType.Area)
                TestMidpoints(obj.GeometryPoints, true, obj.Name);
        }

        foreach (var zone in doc.Zones)
        {
            TestMidpoints(zone.Boundary, true, zone.Name);
        }

        foreach (var ent in doc.Entities)
        {
            if (ent.Points.Count >= 2)
            {
                TestMidpoints(ent.Points, ent.IsClosed || ent.Type == DrawingEntityType.Polygon, ent.Layer ?? "Entity");
            }
        }

        // If Endpoint or Midpoint snap is already found, prefer it over Nearest line
        if (result.DidSnap && (result.Type == SnapType.Endpoint || result.Type == SnapType.Midpoint))
        {
            return result;
        }

        // 3. Nearest on Segment Snapping
        void TestNearestSegments(IReadOnlyList<Point2D> pts, bool isClosed, string label)
        {
            if (pts.Count < 2) return;
            int count = isClosed ? pts.Count : pts.Count - 1;
            for (int i = 0; i < count; i++)
            {
                var p1 = pts[i];
                var p2 = pts[(i + 1) % pts.Count];

                double dist = DistanceToSegmentWithProjection(rawPoint, p1, p2, out var proj);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    result.DidSnap = true;
                    result.SnappedPoint = proj;
                    result.Type = SnapType.Nearest;
                    result.Description = $"Nearest Line: {label}";
                }
            }
        }

        foreach (var obj in doc.TakeoffObjects)
        {
            if (obj.MeasurementType == TakeoffMeasurementType.Length)
                TestNearestSegments(obj.GeometryPoints, false, obj.Name);
            else if (obj.MeasurementType == TakeoffMeasurementType.Area)
                TestNearestSegments(obj.GeometryPoints, true, obj.Name);
        }

        foreach (var zone in doc.Zones)
        {
            TestNearestSegments(zone.Boundary, true, zone.Name);
        }

        foreach (var ent in doc.Entities)
        {
            if (ent.Points.Count >= 2)
            {
                TestNearestSegments(ent.Points, ent.IsClosed || ent.Type == DrawingEntityType.Polygon, ent.Layer ?? "Entity");
            }
        }

        return result;
    }

    private static (double minX, double minY, double maxX, double maxY) GetBoundingBox(IReadOnlyList<Point2D> points)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in points)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        return (minX, minY, maxX, maxY);
    }

    private static bool IsInsideEdge(Point2D p, Point2D a, Point2D b)
    {
        return ((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X)) >= -Epsilon;
    }

    private static Point2D Intersection(Point2D s, Point2D e, Point2D cp1, Point2D cp2)
    {
        double dcX = cp1.X - cp2.X;
        double dcY = cp1.Y - cp2.Y;
        double dpX = s.X - e.X;
        double dpY = s.Y - e.Y;

        double n1 = cp1.X * cp2.Y - cp1.Y * cp2.X;
        double n2 = s.X * e.Y - s.Y * e.X;
        double n3 = 1.0 / (dcX * dpY - dcY * dpX);

        double x = (n1 * dpX - n2 * dcX) * n3;
        double y = (n1 * dpY - n2 * dcY) * n3;

        return new Point2D(x, y);
    }

    private static bool TryIntersectSegments(Point2D p1, Point2D p2, Point2D q1, Point2D q2, out double t, out double u)
    {
        t = 0;
        u = 0;

        double dx1 = p2.X - p1.X;
        double dy1 = p2.Y - p1.Y;
        double dx2 = q2.X - q1.X;
        double dy2 = q2.Y - q1.Y;

        double denom = dx1 * dy2 - dy1 * dx2;
        if (Math.Abs(denom) < Epsilon)
        {
            return false;
        }

        double qpx = q1.X - p1.X;
        double qpy = q1.Y - p1.Y;

        t = (qpx * dy2 - qpy * dx2) / denom;
        u = (qpx * dy1 - qpy * dx1) / denom;

        return t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0;
    }
}
