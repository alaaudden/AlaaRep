#include "TakeoffParsers.h"
#include <iostream>
#include <fstream>
#include <sstream>
#include <cstring>

#define STB_IMAGE_IMPLEMENTATION
#include <stb/stb_image.h>

#include <poppler-document.h>
#include <poppler-page.h>
#include <poppler-page-renderer.h>
#include <poppler-image.h>

namespace AlaaRep::Takeoff {

bool TakeoffParsers::LoadPlanImage(const std::string& filePath, PlanImageData& outImage) {
    int w = 0, h = 0, ch = 0;
    stbi_set_flip_vertically_on_load(0);
    unsigned char* data = stbi_load(filePath.c_str(), &w, &h, &ch, 4); // Force RGBA
    if (!data) {
        std::cerr << "[TakeoffParsers] Failed to load image: " << filePath << std::endl;
        outImage.isValid = false;
        return false;
    }

    outImage.width = w;
    outImage.height = h;
    outImage.channels = 4;
    outImage.pixels.assign(data, data + (w * h * 4));
    stbi_image_free(data);
    outImage.isValid = true;
    return true;
}

bool TakeoffParsers::RenderPdfToImage(const std::string& pdfFilePath, int dpi, PlanImageData& outImage) {
    auto doc = poppler::document::load_from_file(pdfFilePath);
    if (!doc || doc->pages() == 0) {
        std::cerr << "[TakeoffParsers] Failed to load PDF or PDF has 0 pages: " << pdfFilePath << std::endl;
        outImage.isValid = false;
        return false;
    }

    auto page = doc->create_page(0); // Render first page
    if (!page) {
        delete doc;
        return false;
    }

    poppler::page_renderer renderer;
    renderer.set_render_hint(poppler::page_renderer::antialiasing, true);
    renderer.set_render_hint(poppler::page_renderer::text_antialiasing, true);

    int renderDpi = (dpi > 0) ? dpi : 150;
    auto img = renderer.render_page(page, renderDpi, renderDpi);

    delete page;
    delete doc;

    if (!img.is_valid()) {
        std::cerr << "[TakeoffParsers] Poppler failed to render PDF page.\n";
        outImage.isValid = false;
        return false;
    }

    int w = img.width();
    int h = img.height();
    outImage.width = w;
    outImage.height = h;
    outImage.channels = 4;
    outImage.pixels.resize(w * h * 4);

    const char* srcData = img.const_data();
    int bytesPerLine = img.bytes_per_row();

    // Convert poppler image format (ARGB / RGB) to standard RGBA
    for (int y = 0; y < h; ++y) {
        const uint32_t* srcRow = reinterpret_cast<const uint32_t*>(srcData + y * bytesPerLine);
        for (int x = 0; x < w; ++x) {
            uint32_t pixel = srcRow[x];
            // Poppler format is typically format_argb32 (B, G, R, A in little-endian)
            uint8_t b = (pixel & 0x000000FF);
            uint8_t g = (pixel & 0x0000FF00) >> 8;
            uint8_t r = (pixel & 0x00FF0000) >> 16;
            uint8_t a = (pixel & 0xFF000000) >> 24;

            int dstIdx = (y * w + x) * 4;
            outImage.pixels[dstIdx + 0] = r;
            outImage.pixels[dstIdx + 1] = g;
            outImage.pixels[dstIdx + 2] = b;
            outImage.pixels[dstIdx + 3] = (a == 0 ? 255 : a); // Ensure opaque background
        }
    }

    outImage.isValid = true;
    return true;
}

static std::vector<std::string> SplitCsvLine(const std::string& line) {
    std::vector<std::string> tokens;
    std::string token;
    bool inQuotes = false;
    for (char c : line) {
        if (c == '"') {
            inQuotes = !inQuotes;
        } else if (c == ',' && !inQuotes) {
            tokens.push_back(token);
            token.clear();
        } else {
            token += c;
        }
    }
    tokens.push_back(token);
    return tokens;
}

bool TakeoffParsers::ReadBluebeamCsv(const std::string& csvContentOrPath, std::vector<TakeoffObject>& outObjects) {
    std::string content = csvContentOrPath;
    std::ifstream file(csvContentOrPath);
    if (file.is_open()) {
        std::stringstream ss;
        ss << file.rdbuf();
        content = ss.str();
    }

    std::stringstream stream(content);
    std::string line;
    if (!std::getline(stream, line)) return false;

    auto headers = SplitCsvLine(line);
    std::map<std::string, int> colMap;
    for (size_t i = 0; i < headers.size(); ++i) {
        colMap[headers[i]] = (int)i;
    }

    int idCount = 0;
    while (std::getline(stream, line)) {
        if (line.empty()) continue;
        auto cols = SplitCsvLine(line);
        if (cols.empty()) continue;

        TakeoffObject obj;
        obj.id = "bb-" + std::to_string(++idCount);

        std::string subject = colMap.count("Subject") && colMap["Subject"] < (int)cols.size() ? cols[colMap["Subject"]] : "";
        std::string label = colMap.count("Label") && colMap["Label"] < (int)cols.size() ? cols[colMap["Label"]] : "";
        std::string zone = colMap.count("Zone") && colMap["Zone"] < (int)cols.size() ? cols[colMap["Zone"]] : "";
        std::string heightStr = colMap.count("Height") && colMap["Height"] < (int)cols.size() ? cols[colMap["Height"]] : "";

        obj.name = !label.empty() ? label : (!subject.empty() ? subject : "Bluebeam Item " + std::to_string(idCount));
        obj.category = !subject.empty() ? subject : "Takeoff";
        obj.zoneId = zone;

        if (!heightStr.empty()) {
            try { obj.height = std::stod(heightStr); } catch (...) {}
        }

        // Parse measurement type
        if (colMap.count("Area") && colMap["Area"] < (int)cols.size() && !cols[colMap["Area"]].empty()) {
            obj.measurementType = MeasurementType::Area;
            obj.colorHex = "#10B981";
            obj.r = 0.06f; obj.g = 0.72f; obj.b = 0.50f;
        } else if (colMap.count("Length") && colMap["Length"] < (int)cols.size() && !cols[colMap["Length"]].empty()) {
            obj.measurementType = MeasurementType::Length;
            obj.colorHex = "#EF4444";
            obj.r = 0.94f; obj.g = 0.27f; obj.b = 0.27f;
            if (obj.height <= 0.0) obj.height = 3.0; // default wall height
        } else {
            obj.measurementType = MeasurementType::Count;
            obj.colorHex = "#3B82F6";
            obj.r = 0.23f; obj.g = 0.51f; obj.b = 0.96f;
        }

        // Store BIM properties
        for (const auto& [colName, idx] : colMap) {
            if (idx < (int)cols.size() && !cols[idx].empty()) {
                obj.setBimProperty(colName, cols[idx]);
            }
        }

        outObjects.push_back(obj);
    }

    return !outObjects.empty();
}

bool TakeoffParsers::ReadDxf(const std::string& dxfFilePath, DrawingDocument& outDoc, double maxEntities) {
    // 1. Validate if this is a binary DWG file mistakenly supplied or opened
    std::ifstream checkFile(dxfFilePath, std::ios::binary);
    if (!checkFile.is_open()) return false;
    char magic[6] = {0};
    checkFile.read(magic, 5);
    checkFile.close();

    // Check for AutoCAD binary DWG headers: AC1015, AC1018, AC1021, AC1024, AC1027, AC1032, etc.
    if (std::strncmp(magic, "AC10", 4) == 0) {
        std::cerr << "[TakeoffParsers] Warning: '" << dxfFilePath
                  << "' is a binary AutoCAD DWG file, not an ASCII DXF stream. "
                  << "Binary DWG files cannot be parsed directly as ASCII text without converting to DXF or pre-rendering to image. "
                  << "Skipping text parser to prevent freezing or hanging.\n";
        return false;
    }

    std::ifstream file(dxfFilePath);
    if (!file.is_open()) return false;

    outDoc.fileName = dxfFilePath;
    outDoc.fileType = "DXF";
    size_t entityCount = 0;

    std::string code, value;
    bool inEntities = false;
    std::string currentEntity;
    std::string currentLayer = "0";

    double x1 = 0, y1 = 0, x2 = 0, y2 = 0;
    std::vector<Point2D> polyPoints;

    while (std::getline(file, code)) {
        if (!std::getline(file, value)) break;

        // Trim whitespace
        while (!code.empty() && (code.back() == '\r' || code.back() == ' ')) code.pop_back();
        while (!value.empty() && (value.back() == '\r' || value.back() == ' ')) value.pop_back();

        if (code == "2" && value == "ENTITIES") {
            inEntities = true;
            continue;
        }
        if (code == "0" && value == "ENDSEC" && inEntities) {
            // Save last active entity before ending section
            if (currentEntity == "LINE") {
                DrawingEntity ent;
                ent.type = DrawingEntityType::Line;
                ent.layer = currentLayer;
                ent.points = { Point2D(x1, y1), Point2D(x2, y2) };
                outDoc.entities.push_back(ent);
                entityCount++;
            } else if (currentEntity == "LWPOLYLINE" && polyPoints.size() >= 2) {
                DrawingEntity ent;
                ent.type = DrawingEntityType::Polyline;
                ent.layer = currentLayer;
                ent.points = polyPoints;
                outDoc.entities.push_back(ent);
                entityCount++;
            }
            inEntities = false;
            break;
        }

        if (inEntities) {
            if (code == "0") {
                // Save previous entity if completed
                if (currentEntity == "LINE") {
                    DrawingEntity ent;
                    ent.type = DrawingEntityType::Line;
                    ent.layer = currentLayer;
                    ent.points = { Point2D(x1, y1), Point2D(x2, y2) };
                    outDoc.entities.push_back(ent);
                    entityCount++;
                } else if (currentEntity == "LWPOLYLINE" && polyPoints.size() >= 2) {
                    DrawingEntity ent;
                    ent.type = DrawingEntityType::Polyline;
                    ent.layer = currentLayer;
                    ent.points = polyPoints;
                    outDoc.entities.push_back(ent);
                    entityCount++;
                }

                if (entityCount >= maxEntities) {
                    break;
                }

                currentEntity = value;
                currentLayer = "0";
                x1 = y1 = x2 = y2 = 0;
                polyPoints.clear();
            } else if (code == "8") {
                currentLayer = value;
            } else if (code == "10") {
                try { x1 = std::stod(value); } catch (...) {}
            } else if (code == "20") {
                try { y1 = std::stod(value); } catch (...) {}
                if (currentEntity == "LWPOLYLINE") {
                    polyPoints.push_back(Point2D(x1, y1));
                }
            } else if (code == "11") {
                try { x2 = std::stod(value); } catch (...) {}
            } else if (code == "21") {
                try { y2 = std::stod(value); } catch (...) {}
            }
        }
    }

    return true;
}

void TakeoffParsers::PopulateSampleLayout(DrawingDocument& doc) {
    doc.fileName = "Sample Architectural Floor Plan (C++ Engine)";
    doc.fileType = "C++ Native Vector Layout";
    doc.width = 1400.0;
    doc.height = 900.0;
    doc.calibration.scaleLabel = "1:100 (Metric)";
    doc.calibration.calibrationPixelDistance = 100.0;
    doc.calibration.calibrationRealDistance = 5.0; // 1px = 0.05m
    doc.calibration.linearUnit = "m";
    doc.calibration.areaUnit = "m²";

    // Zones
    Zone z1;
    z1.id = "z-101";
    z1.name = "Executive Office (Room 101)";
    z1.code = "ZN-101";
    z1.boundary = { Point2D(100, 100), Point2D(500, 100), Point2D(500, 400), Point2D(100, 400) };
    doc.zones.push_back(z1);

    Zone z2;
    z2.id = "z-102";
    z2.name = "Conference Hall (Zone C)";
    z2.code = "ZN-103";
    z2.boundary = { Point2D(500, 100), Point2D(1100, 100), Point2D(1100, 450), Point2D(500, 450) };
    doc.zones.push_back(z2);

    // Wall object with Height = 3.2m
    TakeoffObject wall;
    wall.id = "wall-1";
    wall.name = "Drywall Partition STC 52";
    wall.category = "Partitions";
    wall.layer = "A-WALL-INTR";
    wall.measurementType = MeasurementType::Length;
    wall.height = 3.2; // 3.2m wall height
    wall.points = { Point2D(150, 400), Point2D(450, 400), Point2D(750, 450), Point2D(1050, 450) };
    wall.setBimProperty("Family", "Wall:Basic Interior");
    wall.setBimProperty("Omniclass", "23-13 25 11");
    wall.setBimProperty("UnitCost", "65.00");
    doc.takeoffObjects.push_back(wall);

    // Flooring object (Area)
    TakeoffObject floor;
    floor.id = "floor-1";
    floor.name = "Engineered Oak Flooring";
    floor.category = "Finishes";
    floor.layer = "A-FLOR-WOOD";
    floor.measurementType = MeasurementType::Area;
    floor.points = { Point2D(120, 120), Point2D(480, 120), Point2D(480, 380), Point2D(120, 380) };
    floor.setBimProperty("Material", "Oak");
    floor.setBimProperty("UnitCost", "45.00");
    doc.takeoffObjects.push_back(floor);

    // Sprinklers (Count)
    TakeoffObject sprinklers;
    sprinklers.id = "sprink-1";
    sprinklers.name = "Pendant Sprinkler Head";
    sprinklers.category = "Fire Protection";
    sprinklers.measurementType = MeasurementType::Count;
    sprinklers.points = {
        Point2D(200, 200), Point2D(350, 250), Point2D(650, 220), Point2D(900, 220)
    };
    sprinklers.setBimProperty("UnitCost", "120.00");
    doc.takeoffObjects.push_back(sprinklers);
}

} // namespace AlaaRep::Takeoff
