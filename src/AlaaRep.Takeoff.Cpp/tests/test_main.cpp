#include "TakeoffModels.h"
#include "TakeoffEngine.h"
#include "TakeoffParsers.h"
#include <iostream>
#include <fstream>
#include <cassert>

using namespace AlaaRep::Takeoff;

void Verify_CppPolygonArea_And_WallArea() {
    DrawingDocument doc;
    doc.calibration.calibrationPixelDistance = 100.0;
    doc.calibration.calibrationRealDistance = 10.0; // 1px = 0.1m

    Zone z1;
    z1.id = "z-1";
    z1.name = "Zone 1";
    z1.code = "Z1";
    z1.boundary = { Point2D(0, 0), Point2D(100, 0), Point2D(100, 100), Point2D(0, 100) };
    doc.zones.push_back(z1);

    // Linear wall of 150px (15.0m). 100px (10.0m) in Zone 1.
    // Wall height = 3.5m.
    // Expected wall area: Total = 15m * 3.5 = 52.5 m², Zone 1 = 10m * 3.5 = 35.0 m².
    TakeoffObject wall;
    wall.id = "w1";
    wall.name = "Exterior Wall";
    wall.measurementType = MeasurementType::Length;
    wall.height = 3.5;
    wall.points = { Point2D(0, 50), Point2D(150, 50) };
    wall.setBimProperty("UnitCost", "40.00");
    doc.takeoffObjects.push_back(wall);

    auto report = TakeoffEngine::CalculateReport(doc);

    assert(std::abs(report.grandTotalLength - 15.0) < 1e-3);
    assert(std::abs(report.grandTotalWallArea - 52.5) < 1e-3);
    assert(report.items.size() == 1);
    assert(report.items[0].zoneBreakdowns.size() == 1);
    assert(std::abs(report.items[0].zoneBreakdowns[0].wallArea - 35.0) < 1e-3);
    assert(std::abs(report.items[0].zoneBreakdowns[0].length - 10.0) < 1e-3);

    std::cout << "[PASS] Verify_CppPolygonArea_And_WallArea\n";
}

void Verify_CppBluebeamAndImageLoading() {
    std::string csv = "Subject,Page Label,Label,Measurement,Area,Length,Height,Material,UnitCost,Zone\n"
                      "Partition,P1,Interior Wall,50 m,,50,3.00,Gypsum,55.00,Z1\n"
                      "Flooring,P1,Tile Floor,200 sq m,200,,,Ceramic,45.00,Z1\n";

    std::vector<TakeoffObject> markups;
    bool parsed = TakeoffParsers::ReadBluebeamCsv(csv, markups);

    assert(parsed);
    assert(markups.size() == 2);
    assert(markups[0].name == "Interior Wall");
    assert(markups[0].measurementType == MeasurementType::Length);
    assert(std::abs(markups[0].height - 3.0) < 1e-3);
    assert(markups[0].getBimProperty("Material") == "Gypsum");

    assert(markups[1].name == "Tile Floor");
    assert(markups[1].measurementType == MeasurementType::Area);

    // Test raster image loader with real sample image
    PlanImageData img;
    bool imgLoaded = TakeoffParsers::LoadPlanImage("samples/architectural_plan_sample.png", img);
    assert(imgLoaded);
    assert(img.width == 1909);
    assert(img.height == 1005);
    assert(img.channels == 4);
    assert(!img.pixels.empty());

    std::cout << "[PASS] Verify_CppBluebeamAndImageLoading\n";
}

void Verify_LargeDxf_Optimization_And_Chunking() {
    DrawingDocument doc;
    // Generate synthetic DXF with 5,000 line segments
    std::string testDxfPath = "/tmp/synthetic_large.dxf";
    {
        std::ofstream dxf(testDxfPath);
        dxf << "0\nSECTION\n2\nENTITIES\n";
        for (int i = 0; i < 5000; ++i) {
            dxf << "0\nLINE\n8\nWALLS\n10\n" << (i * 2) << "\n20\n" << (i * 3)
                << "\n11\n" << (i * 2 + 10) << "\n21\n" << (i * 3 + 10) << "\n";
        }
        dxf << "0\nENDSEC\n0\nEOF\n";
    }

    bool readOk = TakeoffParsers::ReadDxf(testDxfPath, doc, 10000);
    assert(readOk);
    assert(doc.entities.size() == 5000);
    assert(doc.entities[0].points.size() == 2);
    assert(doc.entities[0].layer == "WALLS");

    std::cout << "[PASS] Verify_LargeDxf_Optimization_And_Chunking (5000 CAD entities parsed)\n";
}

int main() {
    std::cout << "Running AlaaRep Takeoff C++ Core Unit Tests...\n";
    Verify_CppPolygonArea_And_WallArea();
    Verify_CppBluebeamAndImageLoading();
    Verify_LargeDxf_Optimization_And_Chunking();
    std::cout << "All C++ Tests Passed Successfully!\n";
    return 0;
}
