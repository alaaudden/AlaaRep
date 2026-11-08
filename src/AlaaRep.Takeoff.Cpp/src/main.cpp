#include "TakeoffModels.h"
#include "TakeoffParsers.h"
#include "TakeoffEngine.h"
#include "GLRenderer.h"
#include <iostream>
#include <iomanip>

using namespace AlaaRep::Takeoff;

int main(int argc, char** argv) {
    std::cout << "====================================================\n";
    std::cout << " AlaaRep Takeoff Studio - High-Performance C++ / OpenGL\n";
    std::cout << "====================================================\n";

    DrawingDocument doc;
    TakeoffParsers::PopulateSampleLayout(doc);

    std::string csvExportPath;
    std::string imagePath;
    std::string dxfPath;
    std::string bluebeamPath;
    bool headless = false;

    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if (arg == "--export-csv" && i + 1 < argc) {
            csvExportPath = argv[++i];
        } else if (arg == "--image" && i + 1 < argc) {
            imagePath = argv[++i];
        } else if (arg == "--dxf" && i + 1 < argc) {
            dxfPath = argv[++i];
        } else if (arg == "--bluebeam" && i + 1 < argc) {
            bluebeamPath = argv[++i];
        } else if (arg == "--headless") {
            headless = true;
        }
    }

    // 1. Load Image Background if specified
    if (!imagePath.empty()) {
        std::cout << "[Takeoff C++] Loading large background drawing image: " << imagePath << std::endl;
        PlanImageData img;
        if (TakeoffParsers::LoadPlanImage(imagePath, img)) {
            doc.backgroundImage = img;
            doc.width = img.width;
            doc.height = img.height;
            std::cout << "[Takeoff C++] Successfully loaded image (" << img.width << " x " << img.height << " px, 4 channels RGBA).\n";
        }
    }

    // 2. Load Bluebeam Markups if specified
    if (!bluebeamPath.empty()) {
        std::cout << "[Takeoff C++] Importing Bluebeam Revu markups: " << bluebeamPath << std::endl;
        std::vector<TakeoffObject> bbObjects;
        if (TakeoffParsers::ReadBluebeamCsv(bluebeamPath, bbObjects)) {
            doc.takeoffObjects.insert(doc.takeoffObjects.end(), bbObjects.begin(), bbObjects.end());
            std::cout << "[Takeoff C++] Successfully imported " << bbObjects.size() << " Bluebeam items.\n";
        }
    }

    // 3. Compute QTO with Wall Height and Zone Breakdown
    auto report = TakeoffEngine::CalculateReport(doc);
    std::cout << "\n--- QUANTITY TAKEOFF (QTO) SUMMARY ---\n";
    std::cout << "Total Count:     " << report.grandTotalCount << " items\n";
    std::cout << "Total Length:    " << std::fixed << std::setprecision(2) << report.grandTotalLength << " " << doc.calibration.linearUnit << "\n";
    std::cout << "Floor Area:      " << std::fixed << std::setprecision(2) << report.grandTotalArea << " " << doc.calibration.areaUnit << "\n";
    std::cout << "Wall Area:       " << std::fixed << std::setprecision(2) << report.grandTotalWallArea << " " << doc.calibration.areaUnit << "\n";
    std::cout << "Total Cost:      $" << std::fixed << std::setprecision(2) << report.grandTotalCost << "\n";
    std::cout << "--------------------------------------\n\n";

    for (const auto& item : report.items) {
        std::cout << "• " << item.name << " (" << item.category << "): ";
        if (item.measurementType == MeasurementType::Count) {
            std::cout << item.totalCount << " pcs";
        } else if (item.measurementType == MeasurementType::Length) {
            std::cout << item.totalLength << " m (Wall Area: " << item.totalWallArea << " m²)";
        } else {
            std::cout << item.totalArea << " m²";
        }
        std::cout << " -> Cost: $" << item.totalCost << "\n";

        for (const auto& zb : item.zoneBreakdowns) {
            std::cout << "    └── [" << zb.zoneCode << " " << zb.zoneName << "] ";
            if (item.measurementType == MeasurementType::Length) {
                std::cout << zb.length << "m | Wall Area: " << zb.wallArea << " m²";
            } else if (item.measurementType == MeasurementType::Area) {
                std::cout << zb.area << " m²";
            } else {
                std::cout << zb.count << " pcs";
            }
            std::cout << "\n";
        }
    }

    if (!csvExportPath.empty()) {
        TakeoffEngine::ExportToCsv(report, csvExportPath);
        std::cout << "\n[Takeoff C++] Report exported to CSV: " << csvExportPath << std::endl;
    }

    if (headless) {
        std::cout << "[Takeoff C++] Headless calculation execution complete.\n";
        return 0;
    }

    // 4. Run Interactive OpenGL Window if display server is present
    GLRenderer renderer;
    if (renderer.Initialize(1350, 850, "AlaaRep Takeoff Studio - C++ OpenGL 60+ FPS Viewport")) {
        if (doc.backgroundImage.isValid) {
            renderer.UpdateTexture(doc.backgroundImage);
        }
        renderer.ZoomFit(doc.width, doc.height);

        std::cout << "[Takeoff C++] OpenGL Hardware accelerated viewport running at 60+ FPS.\n";
        std::cout << "[Takeoff C++] Controls: Mouse Drag to Pan, Scroll to Zoom, Close window to exit.\n";

        // Render loop
        int frame = 0;
        while (!renderer.ShouldClose() && frame < 300) { // In automated runs, cap or run until close
            renderer.PollEvents();
            renderer.Render(doc);
            renderer.SwapBuffers();
            frame++;
        }
    } else {
        std::cout << "[Takeoff C++] Note: Display window could not open (headless/remote environment). CLI/computation functionality 100% active.\n";
    }

    return 0;
}
