#include "TakeoffEngine.h"
#include <fstream>
#include <cmath>
#include <algorithm>
#include <iomanip>

namespace AlaaRep::Takeoff {

static constexpr double EPSILON = 1e-9;

double TakeoffEngine::CalculatePolygonArea(const std::vector<Point2D>& poly) {
    if (poly.size() < 3) return 0.0;
    double sum = 0.0;
    int n = (int)poly.size();
    for (int i = 0; i < n; ++i) {
        int next = (i + 1) % n;
        sum += (poly[i].x * poly[next].y) - (poly[next].x * poly[i].y);
    }
    return std::abs(sum) * 0.5;
}

double TakeoffEngine::CalculatePolylineLength(const std::vector<Point2D>& points) {
    if (points.size() < 2) return 0.0;
    double length = 0.0;
    for (size_t i = 0; i + 1 < points.size(); ++i) {
        length += points[i].distanceTo(points[i + 1]);
    }
    return length;
}

bool TakeoffEngine::IsPointInPolygon(const Point2D& pt, const std::vector<Point2D>& poly) {
    if (poly.size() < 3) return false;
    bool inside = false;
    size_t j = poly.size() - 1;
    for (size_t i = 0; i < poly.size(); ++i) {
        if (((poly[i].y > pt.y) != (poly[j].y > pt.y)) &&
            (pt.x < (poly[j].x - poly[i].x) * (pt.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) {
            inside = !inside;
        }
        j = i;
    }
    return inside;
}

static bool TryIntersectSegments(Point2D p1, Point2D p2, Point2D q1, Point2D q2, double& t, double& u) {
    double dx1 = p2.x - p1.x;
    double dy1 = p2.y - p1.y;
    double dx2 = q2.x - q1.x;
    double dy2 = q2.y - q1.y;

    double denom = dx1 * dy2 - dy1 * dx2;
    if (std::abs(denom) < EPSILON) return false;

    double qpx = q1.x - p1.x;
    double qpy = q1.y - p1.y;

    t = (qpx * dy2 - qpy * dx2) / denom;
    u = (qpx * dy1 - qpy * dx1) / denom;

    return t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0;
}

double TakeoffEngine::CalculatePolylineLengthInZone(const std::vector<Point2D>& line, const std::vector<Point2D>& zonePoly) {
    if (line.size() < 2 || zonePoly.size() < 3) return 0.0;
    double insideTotal = 0.0;

    for (size_t i = 0; i + 1 < line.size(); ++i) {
        Point2D p1 = line[i];
        Point2D p2 = line[i + 1];
        double segLength = p1.distanceTo(p2);
        if (segLength < EPSILON) continue;

        std::vector<double> tValues = { 0.0, 1.0 };
        for (size_t j = 0; j < zonePoly.size(); ++j) {
            size_t nextJ = (j + 1) % zonePoly.size();
            double t = 0, u = 0;
            if (TryIntersectSegments(p1, p2, zonePoly[j], zonePoly[nextJ], t, u)) {
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
            Point2D mid(p1.x + tMid * (p2.x - p1.x), p1.y + tMid * (p2.y - p1.y));
            if (IsPointInPolygon(mid, zonePoly)) {
                insideTotal += (tEnd - tStart) * segLength;
            }
        }
    }

    return insideTotal;
}

CppQuantityReport TakeoffEngine::CalculateReport(const DrawingDocument& doc) {
    CppQuantityReport report;
    report.documentTitle = doc.fileName;

    double scale = doc.calibration.getUnitsPerPixel();
    double areaScale = scale * scale;

    for (const auto& obj : doc.takeoffObjects) {
        QtoItem item;
        item.id = obj.id;
        item.name = obj.name;
        item.category = obj.category;
        item.measurementType = obj.measurementType;
        item.height = obj.height;

        double unitCost = 0.0;
        std::string costStr = obj.getBimProperty("UnitCost");
        if (costStr.empty()) costStr = obj.getBimProperty("Cost");
        if (!costStr.empty()) {
            try { unitCost = std::stod(costStr); } catch (...) {}
        }
        item.unitCost = unitCost;

        switch (obj.measurementType) {
            case MeasurementType::Count:
                item.totalCount = (int)std::max((size_t)1, obj.points.size());
                item.totalCost = item.totalCount * unitCost;
                break;
            case MeasurementType::Length:
                item.totalLength = CalculatePolylineLength(obj.points) * scale;
                if (item.height > 0.0) {
                    item.totalWallArea = item.totalLength * item.height;
                }
                item.totalCost = item.totalLength * unitCost;
                break;
            case MeasurementType::Area:
                item.totalArea = CalculatePolygonArea(obj.points) * areaScale;
                item.totalCost = item.totalArea * unitCost;
                break;
            case MeasurementType::Perimeter:
                item.totalLength = CalculatePolylineLength(obj.points) * scale;
                item.totalCost = item.totalLength * unitCost;
                break;
        }

        // Zone Breakdown
        for (const auto& zone : doc.zones) {
            if (zone.boundary.size() < 3) continue;

            ZoneBreakdown zb;
            zb.zoneId = zone.id;
            zb.zoneName = zone.name;
            zb.zoneCode = zone.code;

            if (obj.measurementType == MeasurementType::Count) {
                for (const auto& pt : obj.points) {
                    if (IsPointInPolygon(pt, zone.boundary)) {
                        zb.count++;
                    }
                }
                if (zb.count > 0) {
                    zb.cost = zb.count * unitCost;
                    item.zoneBreakdowns.push_back(zb);
                }
            } else if (obj.measurementType == MeasurementType::Length) {
                double lenInZone = CalculatePolylineLengthInZone(obj.points, zone.boundary) * scale;
                if (lenInZone > 1e-4) {
                    zb.length = lenInZone;
                    if (item.height > 0.0) {
                        zb.wallArea = lenInZone * item.height;
                    }
                    zb.cost = lenInZone * unitCost;
                    item.zoneBreakdowns.push_back(zb);
                }
            } else if (obj.measurementType == MeasurementType::Area) {
                // Polygon area inside zone
                Point2D centroid(0, 0);
                for (const auto& p : obj.points) { centroid.x += p.x; centroid.y += p.y; }
                if (!obj.points.empty()) { centroid.x /= obj.points.size(); centroid.y /= obj.points.size(); }

                if (IsPointInPolygon(centroid, zone.boundary)) {
                    zb.area = item.totalArea;
                    zb.cost = item.totalCost;
                    item.zoneBreakdowns.push_back(zb);
                }
            }
        }

        report.grandTotalCount += item.totalCount;
        report.grandTotalLength += item.totalLength;
        report.grandTotalArea += item.totalArea;
        report.grandTotalWallArea += item.totalWallArea;
        report.grandTotalCost += item.totalCost;

        report.items.push_back(item);
    }

    return report;
}

bool TakeoffEngine::ExportToCsv(const CppQuantityReport& report, const std::string& outputPath) {
    std::ofstream out(outputPath);
    if (!out.is_open()) return false;

    out << "ID,Name,Category,Type,Zone,WallHeight,Count,Length,FloorArea,WallArea,Cost\n";
    for (const auto& item : report.items) {
        std::string typeStr = (item.measurementType == MeasurementType::Count) ? "Count" :
                              (item.measurementType == MeasurementType::Length) ? "Length/Wall" : "Area";

        if (!item.zoneBreakdowns.empty()) {
            for (const auto& zb : item.zoneBreakdowns) {
                out << "\"" << item.id << "\",\""
                    << item.name << "\",\""
                    << item.category << "\",\""
                    << typeStr << "\",\""
                    << zb.zoneName << "\","
                    << std::fixed << std::setprecision(2) << item.height << ","
                    << zb.count << ","
                    << zb.length << ","
                    << zb.area << ","
                    << zb.wallArea << ","
                    << zb.cost << "\n";
            }
        } else {
            out << "\"" << item.id << "\",\""
                << item.name << "\",\""
                << item.category << "\",\""
                << typeStr << "\",\"Unassigned\","
                << std::fixed << std::setprecision(2) << item.height << ","
                << item.totalCount << ","
                << item.totalLength << ","
                << item.totalArea << ","
                << item.totalWallArea << ","
                << item.totalCost << "\n";
        }
    }

    return true;
}

} // namespace AlaaRep::Takeoff
