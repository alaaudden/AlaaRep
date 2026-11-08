#pragma once

#include "TakeoffModels.h"
#include <string>
#include <vector>

namespace AlaaRep::Takeoff {

struct ZoneBreakdown {
    std::string zoneId;
    std::string zoneName;
    std::string zoneCode;
    int count = 0;
    double length = 0.0;
    double area = 0.0;
    double wallArea = 0.0; // Vertical wall surface area (length * height)
    double cost = 0.0;
};

struct QtoItem {
    std::string id;
    std::string name;
    std::string category;
    MeasurementType measurementType = MeasurementType::Area;
    double height = 0.0;
    int totalCount = 0;
    double totalLength = 0.0;
    double totalArea = 0.0;
    double totalWallArea = 0.0; // Vertical wall area across all zones
    double unitCost = 0.0;
    double totalCost = 0.0;
    std::vector<ZoneBreakdown> zoneBreakdowns;
};

struct CppQuantityReport {
    std::string documentTitle;
    int grandTotalCount = 0;
    double grandTotalLength = 0.0;
    double grandTotalArea = 0.0;
    double grandTotalWallArea = 0.0;
    double grandTotalCost = 0.0;
    std::vector<QtoItem> items;
};

class TakeoffEngine {
public:
    static double CalculatePolygonArea(const std::vector<Point2D>& poly);
    static double CalculatePolylineLength(const std::vector<Point2D>& points);
    static bool IsPointInPolygon(const Point2D& pt, const std::vector<Point2D>& poly);
    static double CalculatePolylineLengthInZone(const std::vector<Point2D>& line, const std::vector<Point2D>& zonePoly);

    static CppQuantityReport CalculateReport(const DrawingDocument& doc);
    static bool ExportToCsv(const CppQuantityReport& report, const std::string& outputPath);
};

} // namespace AlaaRep::Takeoff
