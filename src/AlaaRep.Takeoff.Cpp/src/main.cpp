#include "TakeoffModels.h"
#include "TakeoffParsers.h"
#include "TakeoffEngine.h"
#include "GLRenderer.h"
#include <imgui.h>
#include <imgui_impl_glfw.h>
#include <imgui_impl_opengl3.h>
#include <iostream>
#include <iomanip>
#include <cstring>

using namespace AlaaRep::Takeoff;

int main(int argc, char** argv) {
    std::cout << "====================================================\n";
    std::cout << " AlaaRep Takeoff Studio - High-Performance C++ / OpenGL\n";
    std::cout << "====================================================\n";

    DrawingDocument doc;
    TakeoffParsers::PopulateSampleLayout(doc);

    std::string csvExportPath;
    std::string imagePath;
    std::string pdfPath;
    std::string dxfPath;
    std::string bluebeamPath;
    bool headless = false;

    // Direct object creation options
    std::string addWallCoords;
    double wallHeight = 3.0;
    std::string addZoneCoords;
    std::string zoneName = "Custom Zone";
    std::string zoneCode = "ZN-CUST";

    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if (arg == "--export-csv" && i + 1 < argc) {
            csvExportPath = argv[++i];
        } else if (arg == "--image" && i + 1 < argc) {
            imagePath = argv[++i];
        } else if (arg == "--pdf" && i + 1 < argc) {
            pdfPath = argv[++i];
        } else if (arg == "--dxf" && i + 1 < argc) {
            dxfPath = argv[++i];
        } else if (arg == "--bluebeam" && i + 1 < argc) {
            bluebeamPath = argv[++i];
        } else if (arg == "--headless") {
            headless = true;
        } else if (arg == "--wall-height" && i + 1 < argc) {
            wallHeight = std::stod(argv[++i]);
        } else if (arg == "--add-wall" && i + 1 < argc) {
            addWallCoords = argv[++i];
        } else if (arg == "--add-zone" && i + 1 < argc) {
            addZoneCoords = argv[++i];
        } else if (arg == "--zone-name" && i + 1 < argc) {
            zoneName = argv[++i];
        } else if (arg == "--zone-code" && i + 1 < argc) {
            zoneCode = argv[++i];
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

    // 1b. Load and Rasterize PDF drawing if specified
    if (!pdfPath.empty()) {
        std::cout << "[Takeoff C++] Rasterizing large architectural PDF drawing (zero-lag): " << pdfPath << std::endl;
        PlanImageData pdfImg;
        if (TakeoffParsers::RenderPdfToImage(pdfPath, 150, pdfImg)) {
            doc.backgroundImage = pdfImg;
            doc.width = pdfImg.width;
            doc.height = pdfImg.height;
            std::cout << "[Takeoff C++] Successfully rasterized PDF page (" << pdfImg.width << " x " << pdfImg.height << " px).\n";
        }
    }

    // 1c. Load CAD DXF drawing if specified
    if (!dxfPath.empty()) {
        std::cout << "[Takeoff C++] Parsing CAD DXF drawing: " << dxfPath << std::endl;
        if (TakeoffParsers::ReadDxf(dxfPath, doc)) {
            std::cout << "[Takeoff C++] Successfully imported " << doc.entities.size() << " CAD entities.\n";
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

    // Helper to parse coordinate strings: "x1,y1;x2,y2;..."
    auto parseCoords = [](const std::string& str) {
        std::vector<Point2D> pts;
        std::stringstream ss(str);
        std::string pair;
        while (std::getline(ss, pair, ';')) {
            size_t comma = pair.find(',');
            if (comma != std::string::npos) {
                double x = std::stod(pair.substr(0, comma));
                double y = std::stod(pair.substr(comma + 1));
                pts.emplace_back(x, y);
            }
        }
        return pts;
    };

    if (!addWallCoords.empty()) {
        TakeoffObject customWall;
        customWall.id = "wall-user-" + std::to_string(doc.takeoffObjects.size() + 1);
        customWall.name = "Custom Wall Partition";
        customWall.category = "Partitions";
        customWall.measurementType = MeasurementType::Length;
        customWall.height = wallHeight;
        customWall.points = parseCoords(addWallCoords);
        customWall.setBimProperty("UnitCost", "55.00");
        customWall.setBimProperty("Family", "Wall:Interior Drywall");
        doc.takeoffObjects.push_back(customWall);
        std::cout << "[Takeoff C++] Added custom wall with height " << wallHeight << "m (" << customWall.points.size() << " vertices).\n";
    }

    if (!addZoneCoords.empty()) {
        Zone customZone;
        customZone.id = "zone-user-" + std::to_string(doc.zones.size() + 1);
        customZone.name = zoneName;
        customZone.code = zoneCode;
        customZone.boundary = parseCoords(addZoneCoords);
        doc.zones.push_back(customZone);
        std::cout << "[Takeoff C++] Added custom zone: " << zoneName << " (" << customZone.boundary.size() << " vertices).\n";
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

        // Setup interactive GLFW input callbacks
        static GLRenderer* s_renderer = &renderer;
        static DrawingDocument* s_doc = &doc;
        static bool s_isPanning = false;
        static double s_lastMouseX = 0.0, s_lastMouseY = 0.0;

        glfwSetMouseButtonCallback(renderer.GetWindow(), [](GLFWwindow* w, int button, int action, int mods) {
            if (button == GLFW_MOUSE_BUTTON_RIGHT || button == GLFW_MOUSE_BUTTON_MIDDLE) {
                if (action == GLFW_PRESS) {
                    s_isPanning = true;
                    glfwGetCursorPos(w, &s_lastMouseX, &s_lastMouseY);
                } else if (action == GLFW_RELEASE) {
                    s_isPanning = false;
                }
            } else if (button == GLFW_MOUSE_BUTTON_LEFT && action == GLFW_PRESS) {
                // Don't place drawing points if user is clicking inside ImGui UI windows
                if (ImGui::GetIO().WantCaptureMouse) return;

                // Interactive shape drawing point click
                double mx, my;
                glfwGetCursorPos(w, &mx, &my);
                Point2D drawPt = s_renderer->ScreenToDrawing(mx, my);

                if (s_renderer->activeTool == "Select") {
                    // Pick object on canvas
                    int hitIdx = s_renderer->FindObjectAt(drawPt, *s_doc, 15.0);
                    s_renderer->selectedObjectIndex = hitIdx;
                    if (hitIdx >= 0) {
                        std::cout << "[Takeoff C++] Selected object: " << s_doc->takeoffObjects[hitIdx].name << "\n";
                    } else {
                        std::cout << "[Takeoff C++] Clicked empty canvas.\n";
                    }
                } else if (s_renderer->activeTool == "Length" || s_renderer->activeTool == "Area" || s_renderer->activeTool == "Zone") {
                    // Snap to existing vertices or midpoints
                    bool didSnap = false;
                    std::string snapType;
                    Point2D effectivePt = s_renderer->FindSnapPoint(drawPt, *s_doc, 15.0, didSnap, snapType);

                    s_renderer->inProgressPoints.push_back(effectivePt);
                    if (didSnap) {
                        std::cout << "[Takeoff C++] [SNAP: " << snapType << "] Added snapped vertex (" << effectivePt.x << ", " << effectivePt.y << ")\n";
                    } else {
                        std::cout << "[Takeoff C++] Added vertex (" << effectivePt.x << ", " << effectivePt.y << ") - press ENTER to complete shape.\n";
                    }
                } else if (s_renderer->activeTool == "Count") {
                    TakeoffObject cnt;
                    cnt.id = "cnt-" + std::to_string(s_doc->takeoffObjects.size() + 1);
                    cnt.name = "Count Item";
                    cnt.category = "Equipment";
                    cnt.layer = "Takeoff";
                    cnt.measurementType = MeasurementType::Count;
                    cnt.points = { drawPt };
                    s_doc->takeoffObjects.push_back(cnt);
                    std::cout << "[Takeoff C++] Added count item at (" << drawPt.x << ", " << drawPt.y << ").\n";
                }
            }
        });

        glfwSetCursorPosCallback(renderer.GetWindow(), [](GLFWwindow* w, double xpos, double ypos) {
            if (s_isPanning && s_renderer) {
                double dx = xpos - s_lastMouseX;
                double dy = ypos - s_lastMouseY;
                s_renderer->Pan(dx, dy);
                s_lastMouseX = xpos;
                s_lastMouseY = ypos;
            }
        });

        glfwSetScrollCallback(renderer.GetWindow(), [](GLFWwindow* w, double xoffset, double yoffset) {
            if (s_renderer) {
                double xpos, ypos;
                glfwGetCursorPos(w, &xpos, &ypos);
                double factor = yoffset > 0 ? 1.15 : 0.85;
                s_renderer->Zoom(factor, xpos, ypos);
            }
        });

        glfwSetKeyCallback(renderer.GetWindow(), [](GLFWwindow* w, int key, int scancode, int action, int mods) {
            if (action != GLFW_PRESS) return;

            if (key == GLFW_KEY_ESCAPE) {
                if (!s_renderer->inProgressPoints.empty()) {
                    s_renderer->inProgressPoints.clear();
                    std::cout << "[Takeoff C++] In-progress drawing canceled.\n";
                } else {
                    glfwSetWindowShouldClose(w, GLFW_TRUE);
                }
            } else if (key == GLFW_KEY_L) {
                s_renderer->activeTool = "Length";
                s_renderer->inProgressPoints.clear();
                std::cout << "[Takeoff C++] Active Tool: Length / Wall (Click to add points, ENTER to finish)\n";
            } else if (key == GLFW_KEY_A) {
                s_renderer->activeTool = "Area";
                s_renderer->inProgressPoints.clear();
                std::cout << "[Takeoff C++] Active Tool: Area (Click to add points, ENTER to finish)\n";
            } else if (key == GLFW_KEY_Z) {
                s_renderer->activeTool = "Zone";
                s_renderer->inProgressPoints.clear();
                std::cout << "[Takeoff C++] Active Tool: Zone (Click to add boundary points, ENTER to finish)\n";
            } else if (key == GLFW_KEY_C) {
                s_renderer->activeTool = "Count";
                s_renderer->inProgressPoints.clear();
                std::cout << "[Takeoff C++] Active Tool: Count (Click points to place items)\n";
            } else if (key == GLFW_KEY_ENTER) {
                if (s_renderer->inProgressPoints.size() >= 2) {
                    if (s_renderer->activeTool == "Length") {
                        TakeoffObject wall;
                        wall.id = "wall-" + std::to_string(s_doc->takeoffObjects.size() + 1);
                        wall.name = "Wall Partition";
                        wall.category = "Partitions";
                        wall.measurementType = MeasurementType::Length;
                        wall.height = 3.0; // standard wall height
                        wall.points = s_renderer->inProgressPoints;
                        wall.setBimProperty("Family", "Wall:Interior");
                        wall.setBimProperty("UnitCost", "50.00");
                        s_doc->takeoffObjects.push_back(wall);
                        std::cout << "[Takeoff C++] Completed Wall with height 3.0m (" << wall.points.size() << " points).\n";
                    } else if (s_renderer->activeTool == "Area") {
                        TakeoffObject area;
                        area.id = "area-" + std::to_string(s_doc->takeoffObjects.size() + 1);
                        area.name = "Floor Finish";
                        area.category = "Finishes";
                        area.measurementType = MeasurementType::Area;
                        area.points = s_renderer->inProgressPoints;
                        area.setBimProperty("UnitCost", "40.00");
                        s_doc->takeoffObjects.push_back(area);
                        std::cout << "[Takeoff C++] Completed Floor Area (" << area.points.size() << " points).\n";
                    } else if (s_renderer->activeTool == "Zone") {
                        Zone zone;
                        zone.id = "zone-" + std::to_string(s_doc->zones.size() + 1);
                        zone.name = "Zone " + std::to_string(s_doc->zones.size() + 1);
                        zone.code = "ZN-" + std::to_string(s_doc->zones.size() + 1);
                        zone.boundary = s_renderer->inProgressPoints;
                        s_doc->zones.push_back(zone);
                        std::cout << "[Takeoff C++] Completed Zone: " << zone.name << ".\n";
                    }
                    s_renderer->inProgressPoints.clear();
                }
            }
        });

        // Initialize Dear ImGui context and backends
        IMGUI_CHECKVERSION();
        ImGui::CreateContext();
        ImGuiIO& io = ImGui::GetIO();
        io.ConfigFlags |= ImGuiConfigFlags_NavEnableKeyboard;
        ImGui::StyleColorsDark();

        // Customize style for modern BIM CAD dark theme
        ImGuiStyle& style = ImGui::GetStyle();
        style.WindowRounding = 6.0f;
        style.FrameRounding = 4.0f;
        style.PopupRounding = 4.0f;
        style.ScrollbarRounding = 4.0f;
        style.Colors[ImGuiCol_WindowBg] = ImVec4(0.08f, 0.11f, 0.17f, 0.94f);
        style.Colors[ImGuiCol_Header] = ImVec4(0.15f, 0.23f, 0.36f, 1.0f);
        style.Colors[ImGuiCol_HeaderHovered] = ImVec4(0.23f, 0.35f, 0.54f, 1.0f);
        style.Colors[ImGuiCol_Button] = ImVec4(0.18f, 0.28f, 0.44f, 1.0f);
        style.Colors[ImGuiCol_ButtonHovered] = ImVec4(0.24f, 0.38f, 0.60f, 1.0f);

        ImGui_ImplGlfw_InitForOpenGL(renderer.GetWindow(), true);
        ImGui_ImplOpenGL3_Init("#version 330");

        int selectedObjIndex = -1;
        int selectedZoneIndex = -1;
        char nameBuf[128] = "";
        char catBuf[128] = "";
        char layerBuf[128] = "";
        float editHeight = 3.0f;
        char newPropKey[64] = "Material";
        char newPropVal[64] = "Drywall STC 52";
        char loadFilePath[256] = "samples/architectural_plan_sample.png";
        char newCustomLayer[64] = "A-FINISH";
        char newZoneName[64] = "Zone 2 - Offices";
        char newZoneCode[32] = "ZN-02";

        // Discover and populate initial layer visibility
        auto syncLayers = [&]() {
            for (const auto& e : doc.entities) {
                if (renderer.layerVisibility.find(e.layer) == renderer.layerVisibility.end()) {
                    renderer.layerVisibility[e.layer] = true;
                }
            }
            for (const auto& o : doc.takeoffObjects) {
                if (renderer.layerVisibility.find(o.layer) == renderer.layerVisibility.end()) {
                    renderer.layerVisibility[o.layer] = true;
                }
            }
        };
        syncLayers();

        // Interactive render loop (stays open until user closes window)
        while (!renderer.ShouldClose()) {
            renderer.PollEvents();

            // Start ImGui frame
            ImGui_ImplOpenGL3_NewFrame();
            ImGui_ImplGlfw_NewFrame();
            ImGui::NewFrame();

            // 1. Top Ribbon Toolbar
            ImGui::SetNextWindowPos(ImVec2(10, 10), ImGuiCond_Always);
            ImGui::SetNextWindowSize(ImVec2(800, 70), ImGuiCond_Always);
            if (ImGui::Begin("Toolbar", nullptr, ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoSavedSettings)) {
                ImGui::TextColored(ImVec4(0.23f, 0.73f, 0.96f, 1.0f), "AlaaRep Takeoff C++ (OpenGL 3.3)");
                ImGui::SameLine();
                ImGui::Text("| Active Tool: %s", renderer.activeTool.c_str());
                ImGui::SameLine();
                ImGui::Text("| Zoom: %.2fx", renderer.GetZoom());

                if (ImGui::Button("Select (Esc)")) { renderer.activeTool = "Select"; renderer.inProgressPoints.clear(); }
                ImGui::SameLine();
                if (ImGui::Button("Count (C)")) { renderer.activeTool = "Count"; renderer.inProgressPoints.clear(); }
                ImGui::SameLine();
                if (ImGui::Button("Length/Wall (L)")) { renderer.activeTool = "Length"; renderer.inProgressPoints.clear(); }
                ImGui::SameLine();
                if (ImGui::Button("Area (A)")) { renderer.activeTool = "Area"; renderer.inProgressPoints.clear(); }
                ImGui::SameLine();
                if (ImGui::Button("Zone (Z)")) { renderer.activeTool = "Zone"; renderer.inProgressPoints.clear(); }
                ImGui::SameLine();
                if (ImGui::Button("Zoom Fit")) { renderer.ZoomFit(doc.width, doc.height); }
            }
            ImGui::End();

            // Sync canvas selection with ImGui selection
            if (renderer.selectedObjectIndex >= 0 && renderer.selectedObjectIndex < (int)doc.takeoffObjects.size() && renderer.selectedObjectIndex != selectedObjIndex) {
                selectedObjIndex = renderer.selectedObjectIndex;
                auto& o = doc.takeoffObjects[selectedObjIndex];
                std::strncpy(nameBuf, o.name.c_str(), sizeof(nameBuf));
                std::strncpy(catBuf, o.category.c_str(), sizeof(catBuf));
                std::strncpy(layerBuf, o.layer.c_str(), sizeof(layerBuf));
                editHeight = (float)o.height;
            }

            // 2. Right Sidebar: BIM Property Inspector & Hierarchy
            ImGui::SetNextWindowPos(ImVec2(900, 10), ImGuiCond_Always);
            ImGui::SetNextWindowSize(ImVec2(430, 830), ImGuiCond_Always);
            if (ImGui::Begin("BIM Studio & Quantity Takeoff", nullptr, ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoSavedSettings)) {
                if (ImGui::BeginTabBar("MainStudioTabs")) {
                    // TAB 1: BIM INSPECTOR
                    if (ImGui::BeginTabItem("Inspector")) {
                        if (ImGui::CollapsingHeader("📁 DRAWING INGESTION (PDF / CAD / RASTER)", ImGuiTreeNodeFlags_DefaultOpen)) {
                            ImGui::InputText("File Path", loadFilePath, sizeof(loadFilePath));
                            if (ImGui::Button("Load Background Image")) {
                                PlanImageData img;
                                if (TakeoffParsers::LoadPlanImage(loadFilePath, img)) {
                                    doc.backgroundImage = img;
                                    doc.width = img.width;
                                    doc.height = img.height;
                                    renderer.UpdateTexture(img);
                                    renderer.ZoomFit(doc.width, doc.height);
                                    syncLayers();
                                }
                            }
                            ImGui::SameLine();
                            if (ImGui::Button("Load PDF Plan")) {
                                PlanImageData img;
                                if (TakeoffParsers::RenderPdfToImage(loadFilePath, 150, img)) {
                                    doc.backgroundImage = img;
                                    doc.width = img.width;
                                    doc.height = img.height;
                                    renderer.UpdateTexture(img);
                                    renderer.ZoomFit(doc.width, doc.height);
                                    syncLayers();
                                }
                            }
                            ImGui::SliderFloat("Plan Opacity", (float*)&doc.backgroundImageOpacity, 0.1f, 1.0f);
                        }

                        if (ImGui::CollapsingHeader("🏢 BIM PROPERTY INSPECTOR", ImGuiTreeNodeFlags_DefaultOpen)) {
                            if (doc.takeoffObjects.empty()) {
                                ImGui::TextDisabled("No takeoff objects. Click canvas with L, A, or C tools.");
                            } else {
                                std::vector<std::string> itemNames;
                                for (size_t i = 0; i < doc.takeoffObjects.size(); ++i) {
                                    itemNames.push_back(std::to_string(i + 1) + ". " + doc.takeoffObjects[i].name);
                                }
                                std::vector<const char*> itemPtrs;
                                for (const auto& s : itemNames) itemPtrs.push_back(s.c_str());

                                int currentItem = selectedObjIndex >= 0 ? selectedObjIndex : 0;
                                if (ImGui::Combo("Select Object", &currentItem, itemPtrs.data(), (int)itemPtrs.size())) {
                                    selectedObjIndex = currentItem;
                                    renderer.selectedObjectIndex = currentItem;
                                    auto& o = doc.takeoffObjects[selectedObjIndex];
                                    std::strncpy(nameBuf, o.name.c_str(), sizeof(nameBuf));
                                    std::strncpy(catBuf, o.category.c_str(), sizeof(catBuf));
                                    std::strncpy(layerBuf, o.layer.c_str(), sizeof(layerBuf));
                                    editHeight = (float)o.height;
                                }

                                if (selectedObjIndex >= 0 && selectedObjIndex < (int)doc.takeoffObjects.size()) {
                                    auto& sel = doc.takeoffObjects[selectedObjIndex];
                                    if (ImGui::InputText("Item Name", nameBuf, sizeof(nameBuf))) sel.name = nameBuf;
                                    if (ImGui::InputText("Category", catBuf, sizeof(catBuf))) sel.category = catBuf;
                                    if (ImGui::InputText("Layer", layerBuf, sizeof(layerBuf))) {
                                        sel.layer = layerBuf;
                                        syncLayers();
                                    }

                                    if (sel.measurementType == MeasurementType::Length) {
                                        if (ImGui::InputFloat("Wall Height (m)", &editHeight, 0.1f, 1.0f, "%.2f")) {
                                            sel.height = editHeight;
                                            sel.setBimProperty("Height", std::to_string(editHeight));
                                        }
                                        double len = TakeoffEngine::CalculatePolylineLength(sel.points) * doc.calibration.getUnitsPerPixel();
                                        double wallArea = len * sel.height;
                                        ImGui::TextColored(ImVec4(0.96f, 0.62f, 0.04f, 1.0f), "Wall Area: %.2f m² (Length: %.2f m)", wallArea, len);
                                    } else if (sel.measurementType == MeasurementType::Area) {
                                        double area = TakeoffEngine::CalculatePolygonArea(sel.points) * std::pow(doc.calibration.getUnitsPerPixel(), 2);
                                        ImGui::TextColored(ImVec4(0.06f, 0.72f, 0.50f, 1.0f), "Floor Area: %.2f m²", area);
                                    } else {
                                        ImGui::TextColored(ImVec4(0.23f, 0.51f, 0.96f, 1.0f), "Count: %zu pcs", sel.points.size());
                                    }

                                    ImGui::Separator();
                                    ImGui::Text("BIM / IFC Key-Value Attributes:");
                                    for (auto& [k, v] : sel.bimProperties) {
                                        ImGui::BulletText("%s: %s", k.c_str(), v.c_str());
                                    }

                                    ImGui::InputText("Attribute Key", newPropKey, sizeof(newPropKey));
                                    ImGui::InputText("Attribute Value", newPropVal, sizeof(newPropVal));
                                    if (ImGui::Button("+ Add / Update Attribute")) {
                                        sel.setBimProperty(newPropKey, newPropVal);
                                    }

                                    ImGui::SameLine();
                                    if (ImGui::Button("Delete Item")) {
                                        doc.takeoffObjects.erase(doc.takeoffObjects.begin() + selectedObjIndex);
                                        selectedObjIndex = -1;
                                        renderer.selectedObjectIndex = -1;
                                    }
                                }
                            }
                        }
                        ImGui::EndTabItem();
                    }

                    // TAB 2: BIM HIERARCHY TREE
                    if (ImGui::BeginTabItem("BIM Tree")) {
                        ImGui::TextColored(ImVec4(0.58f, 0.64f, 0.72f, 1.0f), "BUILDING & DISCIPLINE HIERARCHY");
                        if (ImGui::TreeNodeEx("Main Tower Building (BLD-01)", ImGuiTreeNodeFlags_DefaultOpen)) {
                            if (ImGui::TreeNodeEx("Level 01 - Ground Floor (LVL-01)", ImGuiTreeNodeFlags_DefaultOpen)) {
                                if (ImGui::TreeNodeEx("Architectural Works (ARCH)", ImGuiTreeNodeFlags_DefaultOpen)) {
                                    for (size_t i = 0; i < doc.takeoffObjects.size(); ++i) {
                                        if (doc.takeoffObjects[i].category != "Fire Protection" && doc.takeoffObjects[i].category != "Equipment") {
                                            bool isSel = (selectedObjIndex == (int)i);
                                            if (ImGui::Selectable(doc.takeoffObjects[i].name.c_str(), isSel)) {
                                                selectedObjIndex = (int)i;
                                                renderer.selectedObjectIndex = (int)i;
                                            }
                                        }
                                    }
                                    ImGui::TreePop();
                                }
                                if (ImGui::TreeNodeEx("Mechanical & Fire (MEP)", ImGuiTreeNodeFlags_DefaultOpen)) {
                                    for (size_t i = 0; i < doc.takeoffObjects.size(); ++i) {
                                        if (doc.takeoffObjects[i].category == "Fire Protection" || doc.takeoffObjects[i].category == "Equipment") {
                                            bool isSel = (selectedObjIndex == (int)i);
                                            if (ImGui::Selectable(doc.takeoffObjects[i].name.c_str(), isSel)) {
                                                selectedObjIndex = (int)i;
                                                renderer.selectedObjectIndex = (int)i;
                                            }
                                        }
                                    }
                                    ImGui::TreePop();
                                }
                                ImGui::TreePop();
                            }
                            ImGui::TreePop();
                        }
                        ImGui::EndTabItem();
                    }

                    // TAB 3: LAYERS MANAGER
                    if (ImGui::BeginTabItem("Layers")) {
                        ImGui::TextColored(ImVec4(0.58f, 0.64f, 0.72f, 1.0f), "DRAWING & TAKEOFF LAYERS");
                        if (ImGui::Button("Show All")) {
                            for (auto& [l, v] : renderer.layerVisibility) v = true;
                        }
                        ImGui::SameLine();
                        if (ImGui::Button("Hide All")) {
                            for (auto& [l, v] : renderer.layerVisibility) v = false;
                        }

                        ImGui::Separator();
                        for (auto& [layerName, isVis] : renderer.layerVisibility) {
                            ImGui::Checkbox(layerName.c_str(), &isVis);
                        }

                        ImGui::Separator();
                        ImGui::InputText("New Layer Name", newCustomLayer, sizeof(newCustomLayer));
                        if (ImGui::Button("+ Add Layer")) {
                            renderer.layerVisibility[newCustomLayer] = true;
                        }
                        ImGui::SameLine();
                        if (ImGui::Button("Assign to Selected") && selectedObjIndex >= 0 && selectedObjIndex < (int)doc.takeoffObjects.size()) {
                            doc.takeoffObjects[selectedObjIndex].layer = newCustomLayer;
                            renderer.layerVisibility[newCustomLayer] = true;
                        }
                        ImGui::EndTabItem();
                    }

                    // TAB 4: SPECIFIC ZONES
                    if (ImGui::BeginTabItem("Zones")) {
                        ImGui::TextColored(ImVec4(0.58f, 0.64f, 0.72f, 1.0f), "DEFINED LAYOUT ZONES");
                        for (size_t i = 0; i < doc.zones.size(); ++i) {
                            bool isSel = (selectedZoneIndex == (int)i);
                            std::string label = "[" + doc.zones[i].code + "] " + doc.zones[i].name;
                            if (ImGui::Selectable(label.c_str(), isSel)) {
                                selectedZoneIndex = (int)i;
                                std::strncpy(newZoneName, doc.zones[i].name.c_str(), sizeof(newZoneName));
                                std::strncpy(newZoneCode, doc.zones[i].code.c_str(), sizeof(newZoneCode));
                            }
                        }

                        if (selectedZoneIndex >= 0 && selectedZoneIndex < (int)doc.zones.size()) {
                            ImGui::Separator();
                            ImGui::Text("Edit Selected Zone:");
                            ImGui::InputText("Zone Name", newZoneName, sizeof(newZoneName));
                            ImGui::InputText("Zone Code", newZoneCode, sizeof(newZoneCode));
                            if (ImGui::Button("Save Zone")) {
                                doc.zones[selectedZoneIndex].name = newZoneName;
                                doc.zones[selectedZoneIndex].code = newZoneCode;
                            }
                            ImGui::SameLine();
                            if (ImGui::Button("Delete Zone")) {
                                doc.zones.erase(doc.zones.begin() + selectedZoneIndex);
                                selectedZoneIndex = -1;
                            }
                        }
                        ImGui::EndTabItem();
                    }

                    // TAB 5: QTO REPORT TABLE
                    if (ImGui::BeginTabItem("QTO Report")) {
                        auto liveReport = TakeoffEngine::CalculateReport(doc);
                        ImGui::TextColored(ImVec4(0.23f, 0.73f, 0.96f, 1.0f), "Length: %.2fm | Wall Area: %.2fm²", liveReport.grandTotalLength, liveReport.grandTotalWallArea);
                        ImGui::TextColored(ImVec4(0.06f, 0.72f, 0.50f, 1.0f), "Floor Area: %.2fm² | Total: $%.2f", liveReport.grandTotalArea, liveReport.grandTotalCost);

                        if (ImGui::Button("Export QTO to CSV")) {
                            TakeoffEngine::ExportToCsv(liveReport, "takeoff_qto_export.csv");
                            std::cout << "[Takeoff C++] Exported live report to takeoff_qto_export.csv\n";
                        }

                        if (ImGui::BeginTable("QtoReportTable", 4, ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg | ImGuiTableFlags_ScrollY, ImVec2(0, 300))) {
                            ImGui::TableSetupColumn("Item");
                            ImGui::TableSetupColumn("Zone");
                            ImGui::TableSetupColumn("Qty / Wall Area");
                            ImGui::TableSetupColumn("Cost");
                            ImGui::TableHeadersRow();

                            for (const auto& item : liveReport.items) {
                                if (!item.zoneBreakdowns.empty()) {
                                    for (const auto& zb : item.zoneBreakdowns) {
                                        ImGui::TableNextRow();
                                        ImGui::TableSetColumnIndex(0); ImGui::Text("%s", item.name.c_str());
                                        ImGui::TableSetColumnIndex(1); ImGui::Text("%s", zb.zoneName.c_str());
                                        ImGui::TableSetColumnIndex(2);
                                        if (item.measurementType == MeasurementType::Length) {
                                            ImGui::Text("%.1fm (%.1fm²)", zb.length, zb.wallArea);
                                        } else if (item.measurementType == MeasurementType::Area) {
                                            ImGui::Text("%.1f m²", zb.area);
                                        } else {
                                            ImGui::Text("%d pcs", zb.count);
                                        }
                                        ImGui::TableSetColumnIndex(3); ImGui::Text("$%.2f", zb.cost);
                                    }
                                } else {
                                    ImGui::TableNextRow();
                                    ImGui::TableSetColumnIndex(0); ImGui::Text("%s", item.name.c_str());
                                    ImGui::TableSetColumnIndex(1); ImGui::Text("Unassigned");
                                    ImGui::TableSetColumnIndex(2);
                                    if (item.measurementType == MeasurementType::Length) {
                                        ImGui::Text("%.1fm (%.1fm²)", item.totalLength, item.totalWallArea);
                                    } else {
                                        ImGui::Text("%.1f m²", item.totalArea);
                                    }
                                    ImGui::TableSetColumnIndex(3); ImGui::Text("$%.2f", item.totalCost);
                                }
                            }
                            ImGui::EndTable();
                        }
                        ImGui::EndTabItem();
                    }

                    ImGui::EndTabBar();
                }
            }
            ImGui::End();

            // Render OpenGL Viewport scene
            renderer.Render(doc);

            // Render Dear ImGui draw data over OpenGL viewport
            ImGui::Render();
            ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());

            renderer.SwapBuffers();
        }

        // Cleanup ImGui
        ImGui_ImplOpenGL3_Shutdown();
        ImGui_ImplGlfw_Shutdown();
        ImGui::DestroyContext();
    } else {
        std::cout << "[Takeoff C++] Note: Display window could not open (headless/remote environment). CLI/computation functionality 100% active.\n";
    }

    return 0;
}
