#pragma once

#include "TakeoffModels.h"
#include <GL/glew.h>
#include <GLFW/glfw3.h>
#include <string>

namespace AlaaRep::Takeoff {

class GLRenderer {
public:
    GLRenderer();
    ~GLRenderer();

    bool Initialize(int width, int height, const std::string& windowTitle);
    bool ShouldClose() const;
    void PollEvents();
    void SwapBuffers();

    void UpdateTexture(const PlanImageData& image);
    void Render(const DrawingDocument& doc);

    // Pan & Zoom controls
    void Pan(double dx, double dy);
    void Zoom(double factor, double mouseX, double mouseY);
    void ZoomFit(double docW, double docH);

    Point2D ScreenToDrawing(double screenX, double screenY) const;
    Point2D DrawingToScreen(double drawX, double drawY) const;

    // Interactive shape drawing points
    std::vector<Point2D> inProgressPoints;
    std::string activeTool = "Select"; // Select, Length, Area, Zone, Count
    std::map<std::string, bool> layerVisibility;

    // Object picking & snapping
    int FindObjectAt(Point2D pt, const DrawingDocument& doc, double tolerance = 15.0) const;
    Point2D FindSnapPoint(Point2D pt, const DrawingDocument& doc, double tolerance, bool& outDidSnap, std::string& outSnapType) const;

    GLFWwindow* GetWindow() const { return m_window; }
    double GetZoom() const { return m_zoom; }

    int selectedObjectIndex = -1;

private:
    void InitShaders();
    void DrawBackgroundTexture(double w, double h, double opacity);
    void DrawVectorEntities(const std::vector<DrawingEntity>& entities);
    void DrawZones(const std::vector<Zone>& zones);
    void DrawTakeoffObjects(const std::vector<TakeoffObject>& objects);
    void DrawGrid();

    GLFWwindow* m_window = nullptr;
    int m_windowWidth = 1350;
    int m_windowHeight = 850;

    double m_panX = 50.0;
    double m_panY = 50.0;
    double m_zoom = 1.0;

    GLuint m_textureId = 0;
    bool m_hasTexture = false;
    int m_texWidth = 0;
    int m_texHeight = 0;

    GLuint m_shaderProgram = 0;
    GLuint m_texShaderProgram = 0;
    GLuint m_vbo = 0;
    GLuint m_vao = 0;

    // Static CAD Entity GPU VBO Cache (AutoCAD Display List / Retained VBO acceleration)
    GLuint m_staticCadVao = 0;
    GLuint m_staticCadVbo = 0;
    size_t m_staticCadVertexCount = 0;
    size_t m_cachedCadEntityCount = 0;

public:
    void BuildStaticCadCache(const std::vector<DrawingEntity>& entities);
    void InvalidateCadCache();
};

} // namespace AlaaRep::Takeoff
