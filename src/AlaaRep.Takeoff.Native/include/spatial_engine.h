#pragma once

#ifdef _WIN32
  #define TAKEOFF_EXPORT __declspec(dllexport)
#else
  #define TAKEOFF_EXPORT __attribute__((visibility("default")))
#endif

extern "C" {

/**
 * High-performance C++ Polygon Area (Shoelace algorithm).
 * coords: array of interleaved x, y coordinates [x0, y0, x1, y1, ...]
 * pointCount: number of 2D points (coords has size pointCount * 2)
 */
TAKEOFF_EXPORT double takeoff_native_polygon_area(const double* coords, int pointCount);

/**
 * High-performance C++ Polyline Length.
 * coords: array of interleaved x, y coordinates
 * pointCount: number of 2D points
 */
TAKEOFF_EXPORT double takeoff_native_polyline_length(const double* coords, int pointCount);

/**
 * High-performance C++ Ray-Casting Point In Polygon test.
 * Returns 1 if inside, 0 if outside.
 */
TAKEOFF_EXPORT int takeoff_native_point_in_polygon(double px, double py, const double* polyCoords, int polyPointCount);

/**
 * High-performance C++ Segment In Zone Clipping.
 * Computes exact length of polyline inside a convex or non-convex zone polygon.
 */
TAKEOFF_EXPORT double takeoff_native_polyline_length_in_zone(
    const double* lineCoords, int linePointCount,
    const double* zoneCoords, int zonePointCount);

/**
 * High-performance C++ Vertical Wall Area in Zone.
 * lengthInZone * wallHeight.
 */
TAKEOFF_EXPORT double takeoff_native_wall_area_in_zone(
    const double* lineCoords, int linePointCount, double wallHeight,
    const double* zoneCoords, int zonePointCount);

/**
 * Distance from point (px, py) to segment (vx, vy)-(wx, wy)
 * Outputs closest projection coordinates in outProjX, outProjY.
 */
TAKEOFF_EXPORT double takeoff_native_distance_to_segment(
    double px, double py,
    double vx, double vy,
    double wx, double wy,
    double* outProjX, double* outProjY);

}
