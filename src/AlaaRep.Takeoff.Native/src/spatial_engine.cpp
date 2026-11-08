#include "spatial_engine.h"
#include <cmath>
#include <vector>
#include <algorithm>

namespace {
    constexpr double EPSILON = 1e-9;

    struct Point {
        double x;
        double y;
    };

    inline double dist(Point a, Point b) {
        double dx = b.x - a.x;
        double dy = b.y - a.y;
        return std::sqrt(dx * dx + dy * dy);
    }

    inline bool try_intersect_segments(Point p1, Point p2, Point q1, Point q2, double& t, double& u) {
        double dx1 = p2.x - p1.x;
        double dy1 = p2.y - p1.y;
        double dx2 = q2.x - q1.x;
        double dy2 = q2.y - q1.y;

        double denom = dx1 * dy2 - dy1 * dx2;
        if (std::abs(denom) < EPSILON) {
            return false;
        }

        double qpx = q1.x - p1.x;
        double qpy = q1.y - p1.y;

        t = (qpx * dy2 - qpy * dx2) / denom;
        u = (qpx * dy1 - qpy * dx1) / denom;

        return t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0;
    }
}

extern "C" {

TAKEOFF_EXPORT double takeoff_native_polygon_area(const double* coords, int pointCount) {
    if (!coords || pointCount < 3) return 0.0;

    double sum = 0.0;
    for (int i = 0; i < pointCount; ++i) {
        int next = (i + 1) % pointCount;
        double x1 = coords[i * 2];
        double y1 = coords[i * 2 + 1];
        double x2 = coords[next * 2];
        double y2 = coords[next * 2 + 1];

        sum += (x1 * y2) - (x2 * y1);
    }

    return std::abs(sum) * 0.5;
}

TAKEOFF_EXPORT double takeoff_native_polyline_length(const double* coords, int pointCount) {
    if (!coords || pointCount < 2) return 0.0;

    double length = 0.0;
    for (int i = 0; i < pointCount - 1; ++i) {
        double dx = coords[(i + 1) * 2] - coords[i * 2];
        double dy = coords[(i + 1) * 2 + 1] - coords[i * 2 + 1];
        length += std::sqrt(dx * dx + dy * dy);
    }

    return length;
}

TAKEOFF_EXPORT int takeoff_native_point_in_polygon(double px, double py, const double* polyCoords, int polyPointCount) {
    if (!polyCoords || polyPointCount < 3) return 0;

    bool inside = false;
    int j = polyPointCount - 1;

    for (int i = 0; i < polyPointCount; ++i) {
        double pix = polyCoords[i * 2];
        double piy = polyCoords[i * 2 + 1];
        double pjx = polyCoords[j * 2];
        double pjy = polyCoords[j * 2 + 1];

        if (((piy > py) != (pjy > py)) &&
            (px < (pjx - pix) * (py - piy) / (pjy - piy) + pix)) {
            inside = !inside;
        }
        j = i;
    }

    return inside ? 1 : 0;
}

TAKEOFF_EXPORT double takeoff_native_polyline_length_in_zone(
    const double* lineCoords, int linePointCount,
    const double* zoneCoords, int zonePointCount) {

    if (!lineCoords || linePointCount < 2 || !zoneCoords || zonePointCount < 3) {
        return 0.0;
    }

    double totalInside = 0.0;

    for (int i = 0; i < linePointCount - 1; ++i) {
        Point p1{ lineCoords[i * 2], lineCoords[i * 2 + 1] };
        Point p2{ lineCoords[(i + 1) * 2], lineCoords[(i + 1) * 2 + 1] };

        double segLength = dist(p1, p2);
        if (segLength < EPSILON) continue;

        std::vector<double> tValues;
        tValues.push_back(0.0);
        tValues.push_back(1.0);

        for (int j = 0; j < zonePointCount; ++j) {
            int nextJ = (j + 1) % zonePointCount;
            Point q1{ zoneCoords[j * 2], zoneCoords[j * 2 + 1] };
            Point q2{ zoneCoords[nextJ * 2], zoneCoords[nextJ * 2 + 1] };

            double t = 0.0, u = 0.0;
            if (try_intersect_segments(p1, p2, q1, q2, t, u)) {
                if (t > EPSILON && t < 1.0 - EPSILON) {
                    tValues.push_back(t);
                }
            }
        }

        std::sort(tValues.begin(), tValues.end());

        for (size_t k = 0; k + 1 < tValues.size(); ++k) {
            double tStart = tValues[k];
            double tEnd = tValues[k + 1];
            if (tEnd - tStart < EPSILON) continue;

            double tMid = (tStart + tEnd) * 0.5;
            double midX = p1.x + tMid * (p2.x - p1.x);
            double midY = p1.y + tMid * (p2.y - p1.y);

            if (takeoff_native_point_in_polygon(midX, midY, zoneCoords, zonePointCount)) {
                totalInside += (tEnd - tStart) * segLength;
            }
        }
    }

    return totalInside;
}

TAKEOFF_EXPORT double takeoff_native_wall_area_in_zone(
    const double* lineCoords, int linePointCount, double wallHeight,
    const double* zoneCoords, int zonePointCount) {

    double length = takeoff_native_polyline_length_in_zone(lineCoords, linePointCount, zoneCoords, zonePointCount);
    return length * wallHeight;
}

TAKEOFF_EXPORT double takeoff_native_distance_to_segment(
    double px, double py,
    double vx, double vy,
    double wx, double wy,
    double* outProjX, double* outProjY) {

    double l2 = (wx - vx) * (wx - vx) + (wy - vy) * (wy - vy);
    if (l2 < EPSILON) {
        if (outProjX) *outProjX = vx;
        if (outProjY) *outProjY = vy;
        double dx = px - vx;
        double dy = py - vy;
        return std::sqrt(dx * dx + dy * dy);
    }

    double t = ((px - vx) * (wx - vx) + (py - vy) * (wy - vy)) / l2;
    t = std::max(0.0, std::min(1.0, t));

    double projX = vx + t * (wx - vx);
    double projY = vy + t * (wy - vy);

    if (outProjX) *outProjX = projX;
    if (outProjY) *outProjY = projY;

    double dx = px - projX;
    double dy = py - projY;
    return std::sqrt(dx * dx + dy * dy);
}

}
