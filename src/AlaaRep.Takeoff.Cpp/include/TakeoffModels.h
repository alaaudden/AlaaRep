#pragma once

#include <string>
#include <vector>
#include <map>
#include <cmath>
#include <memory>

namespace AlaaRep::Takeoff {

struct Point2D {
    double x = 0.0;
    double y = 0.0;

    Point2D() = default;
    Point2D(double px, double py) : x(px), y(py) {}

    double distanceTo(const Point2D& other) const {
        double dx = other.x - x;
        double dy = other.y - y;
        return std::sqrt(dx * dx + dy * dy);
    }
};

enum class DrawingEntityType {
    Line,
    Polyline,
    Polygon,
    Circle,
    Text
};

struct DrawingEntity {
    DrawingEntityType type = DrawingEntityType::Line;
    std::string layer = "0";
    std::string colorHex = "#94A3B8";
    float r = 0.58f, g = 0.64f, b = 0.72f;
    double strokeWidth = 1.5;
    bool isClosed = false;
    double radius = 0.0;
    std::string text;
    double textSize = 12.0;
    std::vector<Point2D> points;
};

struct Zone {
    std::string id;
    std::string name;
    std::string code;
    std::string colorHex = "#10B981";
    float r = 0.06f, g = 0.72f, b = 0.50f;
    double opacity = 0.25;
    std::vector<Point2D> boundary;
};

enum class MeasurementType {
    Count,
    Length,
    Area,
    Perimeter
};

struct TakeoffObject {
    std::string id;
    std::string name;
    std::string category = "General";
    std::string layer = "General";
    MeasurementType measurementType = MeasurementType::Area;

    // Wall Height property (used for vertical wall surface area)
    double height = 0.0;

    std::string zoneId;
    std::string building;
    std::string floor;
    std::string discipline;

    std::string colorHex = "#3B82F6";
    float r = 0.23f, g = 0.51f, b = 0.96f;
    double opacity = 0.4;
    double strokeWidth = 2.0;

    std::vector<Point2D> points;
    std::map<std::string, std::string> bimProperties;

    void setBimProperty(const std::string& key, const std::string& val) {
        bimProperties[key] = val;
    }

    std::string getBimProperty(const std::string& key) const {
        auto it = bimProperties.find(key);
        return it != bimProperties.end() ? it->second : "";
    }
};

struct ScaleCalibration {
    std::string scaleLabel = "1:100 (Metric)";
    double calibrationPixelDistance = 100.0;
    double calibrationRealDistance = 5.0;
    std::string linearUnit = "m";
    std::string areaUnit = "m²";

    double getUnitsPerPixel() const {
        return calibrationPixelDistance > 0.0 ? (calibrationRealDistance / calibrationPixelDistance) : 0.05;
    }
};

struct PlanImageData {
    int width = 0;
    int height = 0;
    int channels = 0;
    std::vector<unsigned char> pixels;
    bool isValid = false;
};

struct DrawingDocument {
    std::string fileName = "Untitled Drawing";
    std::string fileType = "Raster/Vector";
    double width = 1200.0;
    double height = 800.0;

    PlanImageData backgroundImage;
    std::string backgroundImagePath;
    double backgroundImageOpacity = 1.0;

    ScaleCalibration calibration;
    std::vector<DrawingEntity> entities;
    std::vector<Zone> zones;
    std::vector<TakeoffObject> takeoffObjects;
};

} // namespace AlaaRep::Takeoff
