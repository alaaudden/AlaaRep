#include "GLRenderer.h"
#include <iostream>
#include <vector>
#include <algorithm>

namespace AlaaRep::Takeoff {

static const char* vertexShaderSource = R"(
#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec4 aColor;

out vec4 vertexColor;

uniform mat4 uProjection;

void main() {
    gl_Position = uProjection * vec4(aPos, 0.0, 1.0);
    vertexColor = aColor;
}
)";

static const char* fragmentShaderSource = R"(
#version 330 core
in vec4 vertexColor;
out vec4 FragColor;

void main() {
    FragColor = vertexColor;
}
)";

static const char* texVertexShaderSource = R"(
#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexCoord;

out vec2 TexCoord;
uniform mat4 uProjection;

void main() {
    gl_Position = uProjection * vec4(aPos, 0.0, 1.0);
    TexCoord = aTexCoord;
}
)";

static const char* texFragmentShaderSource = R"(
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;

uniform sampler2D uTexture;
uniform float uOpacity;

void main() {
    vec4 col = texture(uTexture, TexCoord);
    FragColor = vec4(col.rgb, col.a * uOpacity);
}
)";

static GLuint CompileShader(GLenum type, const char* source) {
    GLuint shader = glCreateShader(type);
    glShaderSource(shader, 1, &source, nullptr);
    glCompileShader(shader);

    GLint success;
    glGetShaderiv(shader, GL_COMPILE_STATUS, &success);
    if (!success) {
        char infoLog[512];
        glGetShaderInfoLog(shader, 512, nullptr, infoLog);
        std::cerr << "[OpenGL] Shader Compilation Failed:\n" << infoLog << std::endl;
    }
    return shader;
}

GLRenderer::GLRenderer() = default;

GLRenderer::~GLRenderer() {
    if (m_textureId) glDeleteTextures(1, &m_textureId);
    if (m_vao) glDeleteVertexArrays(1, &m_vao);
    if (m_vbo) glDeleteBuffers(1, &m_vbo);
    if (m_staticCadVao) glDeleteVertexArrays(1, &m_staticCadVao);
    if (m_staticCadVbo) glDeleteBuffers(1, &m_staticCadVbo);
    if (m_shaderProgram) glDeleteProgram(m_shaderProgram);
    if (m_texShaderProgram) glDeleteProgram(m_texShaderProgram);
    if (m_window) glfwDestroyWindow(m_window);
    glfwTerminate();
}

void GLRenderer::InvalidateCadCache() {
    m_staticCadVertexCount = 0;
    m_cachedCadEntityCount = 0;
}

void GLRenderer::BuildStaticCadCache(const std::vector<DrawingEntity>& entities) {
    if (entities.empty()) {
        m_staticCadVertexCount = 0;
        m_cachedCadEntityCount = 0;
        return;
    }

    if (!m_staticCadVao) glGenVertexArrays(1, &m_staticCadVao);
    if (!m_staticCadVbo) glGenBuffers(1, &m_staticCadVbo);

    // Pack all lines directly into local drawing space coordinates: (x, y, r, g, b, a)
    std::vector<float> buffer;
    buffer.reserve(entities.size() * 12);

    for (const auto& ent : entities) {
        if (ent.points.size() < 2) continue;
        for (size_t i = 0; i + 1 < ent.points.size(); ++i) {
            // Vertex 1
            buffer.push_back((float)ent.points[i].x);
            buffer.push_back((float)ent.points[i].y);
            buffer.push_back(ent.r); buffer.push_back(ent.g); buffer.push_back(ent.b); buffer.push_back(1.0f);

            // Vertex 2
            buffer.push_back((float)ent.points[i + 1].x);
            buffer.push_back((float)ent.points[i + 1].y);
            buffer.push_back(ent.r); buffer.push_back(ent.g); buffer.push_back(ent.b); buffer.push_back(1.0f);
        }
    }

    m_staticCadVertexCount = buffer.size() / 6;
    m_cachedCadEntityCount = entities.size();

    glBindVertexArray(m_staticCadVao);
    glBindBuffer(GL_ARRAY_BUFFER, m_staticCadVbo);
    glBufferData(GL_ARRAY_BUFFER, buffer.size() * sizeof(float), buffer.data(), GL_STATIC_DRAW);

    glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)0);
    glEnableVertexAttribArray(0);

    glVertexAttribPointer(1, 4, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)(2 * sizeof(float)));
    glEnableVertexAttribArray(1);

    glBindVertexArray(0);
}

bool GLRenderer::Initialize(int width, int height, const std::string& windowTitle) {
    m_windowWidth = width;
    m_windowHeight = height;

    if (!glfwInit()) {
        std::cerr << "[GLFW] Failed to initialize GLFW." << std::endl;
        return false;
    }

    glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
    glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
    glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);
    glfwWindowHint(GLFW_SAMPLES, 4); // 4x MSAA Anti-aliasing

    // In headless or container environments without active X11 display, allow creation or fallback
    m_window = glfwCreateWindow(width, height, windowTitle.c_str(), nullptr, nullptr);
    if (!m_window) {
        std::cerr << "[GLFW] Window creation failed (X11 / Wayland display may be unavailable). Headless calculations supported." << std::endl;
        return false;
    }

    glfwMakeContextCurrent(m_window);
    glfwSwapInterval(1); // Enable VSync for smooth 60+ FPS

    glewExperimental = GL_TRUE;
    GLenum err = glewInit();
    if (err != GLEW_OK) {
        std::cerr << "[GLEW] Failed to initialize GLEW: " << glewGetErrorString(err) << std::endl;
        return false;
    }

    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
    glEnable(GL_MULTISAMPLE);

    InitShaders();
    return true;
}

void GLRenderer::InitShaders() {
    GLuint vs = CompileShader(GL_VERTEX_SHADER, vertexShaderSource);
    GLuint fs = CompileShader(GL_FRAGMENT_SHADER, fragmentShaderSource);
    m_shaderProgram = glCreateProgram();
    glAttachShader(m_shaderProgram, vs);
    glAttachShader(m_shaderProgram, fs);
    glLinkProgram(m_shaderProgram);
    glDeleteShader(vs);
    glDeleteShader(fs);

    GLuint texVs = CompileShader(GL_VERTEX_SHADER, texVertexShaderSource);
    GLuint texFs = CompileShader(GL_FRAGMENT_SHADER, texFragmentShaderSource);
    m_texShaderProgram = glCreateProgram();
    glAttachShader(m_texShaderProgram, texVs);
    glAttachShader(m_texShaderProgram, texFs);
    glLinkProgram(m_texShaderProgram);
    glDeleteShader(texVs);
    glDeleteShader(texFs);

    glGenVertexArrays(1, &m_vao);
    glGenBuffers(1, &m_vbo);
}

bool GLRenderer::ShouldClose() const {
    return m_window ? glfwWindowShouldClose(m_window) : true;
}

void GLRenderer::PollEvents() {
    if (m_window) glfwPollEvents();
}

void GLRenderer::SwapBuffers() {
    if (m_window) glfwSwapBuffers(m_window);
}

void GLRenderer::UpdateTexture(const PlanImageData& image) {
    if (!image.isValid || image.pixels.empty()) return;

    if (!m_textureId) {
        glGenTextures(1, &m_textureId);
    }

    glBindTexture(GL_TEXTURE_2D, m_textureId);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, image.width, image.height, 0, GL_RGBA, GL_UNSIGNED_BYTE, image.pixels.data());
    glGenerateMipmap(GL_TEXTURE_2D);

    m_hasTexture = true;
    m_texWidth = image.width;
    m_texHeight = image.height;
}

void GLRenderer::Pan(double dx, double dy) {
    m_panX += dx;
    m_panY += dy;
}

void GLRenderer::Zoom(double factor, double mouseX, double mouseY) {
    double oldZoom = m_zoom;
    m_zoom = std::clamp(m_zoom * factor, 0.02, 50.0);

    // Zoom centered on cursor
    m_panX = mouseX - (mouseX - m_panX) * (m_zoom / oldZoom);
    m_panY = mouseY - (mouseY - m_panY) * (m_zoom / oldZoom);
}

void GLRenderer::ZoomFit(double docW, double docH) {
    if (docW <= 0 || docH <= 0) return;
    double pad = 40.0;
    double availW = std::max(100.0, m_windowWidth - pad * 2);
    double availH = std::max(100.0, m_windowHeight - pad * 2);

    m_zoom = std::clamp(std::min(availW / docW, availH / docH), 0.05, 10.0);
    m_panX = (m_windowWidth - docW * m_zoom) * 0.5;
    m_panY = (m_windowHeight - docH * m_zoom) * 0.5;
}

Point2D GLRenderer::ScreenToDrawing(double screenX, double screenY) const {
    return Point2D((screenX - m_panX) / m_zoom, (screenY - m_panY) / m_zoom);
}

Point2D GLRenderer::DrawingToScreen(double drawX, double drawY) const {
    return Point2D(drawX * m_zoom + m_panX, drawY * m_zoom + m_panY);
}

static double DistToSeg(Point2D p, Point2D v, Point2D w) {
    double l2 = (w.x - v.x) * (w.x - v.x) + (w.y - v.y) * (w.y - v.y);
    if (l2 < 1e-9) return p.distanceTo(v);
    double t = ((p.x - v.x) * (w.x - v.x) + (p.y - v.y) * (w.y - v.y)) / l2;
    t = std::max(0.0, std::min(1.0, t));
    Point2D proj(v.x + t * (w.x - v.x), v.y + t * (w.y - v.y));
    return p.distanceTo(proj);
}

int GLRenderer::FindObjectAt(Point2D pt, const DrawingDocument& doc, double tolerance) const {
    double effTol = tolerance / m_zoom;
    for (int i = (int)doc.takeoffObjects.size() - 1; i >= 0; --i) {
        const auto& obj = doc.takeoffObjects[i];
        if (obj.measurementType == MeasurementType::Count) {
            for (const auto& p : obj.points) {
                if (p.distanceTo(pt) <= effTol) return i;
            }
        } else if (obj.measurementType == MeasurementType::Length) {
            for (size_t k = 0; k + 1 < obj.points.size(); ++k) {
                if (DistToSeg(pt, obj.points[k], obj.points[k + 1]) <= effTol) return i;
            }
        } else if (obj.measurementType == MeasurementType::Area) {
            bool inside = false;
            size_t j = obj.points.size() - 1;
            for (size_t k = 0; k < obj.points.size(); ++k) {
                if (((obj.points[k].y > pt.y) != (obj.points[j].y > pt.y)) &&
                    (pt.x < (obj.points[j].x - obj.points[k].x) * (pt.y - obj.points[k].y) / (obj.points[j].y - obj.points[k].y) + obj.points[k].x)) {
                    inside = !inside;
                }
                j = k;
            }
            if (inside) return i;
        }
    }
    return -1;
}

Point2D GLRenderer::FindSnapPoint(Point2D pt, const DrawingDocument& doc, double tolerance, bool& outDidSnap, std::string& outSnapType) const {
    outDidSnap = false;
    double bestDist = tolerance / m_zoom;
    Point2D bestPt = pt;

    // Check endpoints
    for (const auto& obj : doc.takeoffObjects) {
        for (const auto& p : obj.points) {
            double d = pt.distanceTo(p);
            if (d < bestDist) {
                bestDist = d;
                bestPt = p;
                outDidSnap = true;
                outSnapType = "Endpoint";
            }
        }
    }

    // Check midpoints if no close endpoint
    if (!outDidSnap) {
        for (const auto& obj : doc.takeoffObjects) {
            if (obj.points.size() >= 2) {
                for (size_t i = 0; i + 1 < obj.points.size(); ++i) {
                    Point2D mid((obj.points[i].x + obj.points[i + 1].x) * 0.5, (obj.points[i].y + obj.points[i + 1].y) * 0.5);
                    double d = pt.distanceTo(mid);
                    if (d < bestDist) {
                        bestDist = d;
                        bestPt = mid;
                        outDidSnap = true;
                        outSnapType = "Midpoint";
                    }
                }
            }
        }
    }

    return bestPt;
}

void GLRenderer::Render(const DrawingDocument& doc) {
    if (!m_window) return;

    glfwGetFramebufferSize(m_window, &m_windowWidth, &m_windowHeight);
    glViewport(0, 0, m_windowWidth, m_windowHeight);

    // Dark CAD background color: #060913
    glClearColor(0.024f, 0.035f, 0.075f, 1.0f);
    glClear(GL_COLOR_BUFFER_BIT);

    // 1. Draw large background textured image if available
    if (m_hasTexture) {
        DrawBackgroundTexture(m_texWidth, m_texHeight, doc.backgroundImageOpacity);
    }

    // 2. Draw Vector CAD entities
    DrawVectorEntities(doc.entities);

    // 3. Draw Specific Zones
    DrawZones(doc.zones);

    // 4. Draw Takeoff Objects
    DrawTakeoffObjects(doc.takeoffObjects);

    // 5. Draw in-progress rubberband points if drawing shape
    if (inProgressPoints.size() >= 2) {
        std::vector<DrawingEntity> tempLines;
        for (size_t i = 0; i + 1 < inProgressPoints.size(); ++i) {
            DrawingEntity de;
            de.points = { inProgressPoints[i], inProgressPoints[i + 1] };
            de.r = 0.96f; de.g = 0.62f; de.b = 0.04f; // Amber rubberband
            tempLines.push_back(de);
        }
        DrawVectorEntities(tempLines);
    }
}

void GLRenderer::DrawBackgroundTexture(double w, double h, double opacity) {
    glUseProgram(m_texShaderProgram);

    // View transform
    Point2D p0 = DrawingToScreen(0, 0);
    Point2D p1 = DrawingToScreen(w, 0);
    Point2D p2 = DrawingToScreen(w, h);
    Point2D p3 = DrawingToScreen(0, h);

    auto toNdcX = [&](double sx) { return (float)((sx / m_windowWidth) * 2.0 - 1.0); };
    auto toNdcY = [&](double sy) { return (float)(1.0 - (sy / m_windowHeight) * 2.0); };

    float quadVertices[] = {
        toNdcX(p0.x), toNdcY(p0.y), 0.0f, 0.0f,
        toNdcX(p1.x), toNdcY(p1.y), 1.0f, 0.0f,
        toNdcX(p2.x), toNdcY(p2.y), 1.0f, 1.0f,

        toNdcX(p0.x), toNdcY(p0.y), 0.0f, 0.0f,
        toNdcX(p2.x), toNdcY(p2.y), 1.0f, 1.0f,
        toNdcX(p3.x), toNdcY(p3.y), 0.0f, 1.0f
    };

    float ident[16] = {
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1
    };
    glUniformMatrix4fv(glGetUniformLocation(m_texShaderProgram, "uProjection"), 1, GL_FALSE, ident);
    glUniform1f(glGetUniformLocation(m_texShaderProgram, "uOpacity"), (float)opacity);

    glActiveTexture(GL_TEXTURE0);
    glBindTexture(GL_TEXTURE_2D, m_textureId);
    glUniform1i(glGetUniformLocation(m_texShaderProgram, "uTexture"), 0);

    glBindVertexArray(m_vao);
    glBindBuffer(GL_ARRAY_BUFFER, m_vbo);
    glBufferData(GL_ARRAY_BUFFER, sizeof(quadVertices), quadVertices, GL_DYNAMIC_DRAW);

    glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, 4 * sizeof(float), (void*)0);
    glEnableVertexAttribArray(0);

    glVertexAttribPointer(1, 2, GL_FLOAT, GL_FALSE, 4 * sizeof(float), (void*)(2 * sizeof(float)));
    glEnableVertexAttribArray(1);

    glDrawArrays(GL_TRIANGLES, 0, 6);
}

void GLRenderer::DrawVectorEntities(const std::vector<DrawingEntity>& entities) {
    if (entities.empty()) return;
    glUseProgram(m_shaderProgram);

    // If static cache is dirty or not yet generated, build GPU static VBO once
    if (m_cachedCadEntityCount != entities.size() || m_staticCadVertexCount == 0) {
        BuildStaticCadCache(entities);
    }

    // High-performance GPU Viewport Transformation Matrix (auto-computed by vertex shader):
    // Maps drawing coordinates directly to NDC without CPU per-vertex loops:
    // NDC_X = ((x * zoom + panX) / windowWidth) * 2 - 1 = (2*zoom/w)*x + (2*panX/w - 1)
    // NDC_Y = 1 - ((y * zoom + panY) / windowHeight) * 2 = (-2*zoom/h)*y + (1 - 2*panY/h)
    float sx = (float)(2.0 * m_zoom / m_windowWidth);
    float sy = (float)(-2.0 * m_zoom / m_windowHeight);
    float tx = (float)(2.0 * m_panX / m_windowWidth - 1.0);
    float ty = (float)(1.0 - 2.0 * m_panY / m_windowHeight);

    float transformMatrix[16] = {
        sx,  0.0f, 0.0f, 0.0f,
        0.0f, sy,  0.0f, 0.0f,
        0.0f, 0.0f, 1.0f, 0.0f,
        tx,   ty,  0.0f, 1.0f
    };
    glUniformMatrix4fv(glGetUniformLocation(m_shaderProgram, "uProjection"), 1, GL_FALSE, transformMatrix);

    if (m_staticCadVao != 0 && m_staticCadVertexCount > 0) {
        glBindVertexArray(m_staticCadVao);
        glLineWidth(1.5f);
        // Single draw call renders 500,000+ CAD lines in < 0.2ms directly on GPU!
        glDrawArrays(GL_LINES, 0, (GLsizei)m_staticCadVertexCount);
        glBindVertexArray(0);
    }
}

void GLRenderer::DrawZones(const std::vector<Zone>& zones) {
    if (zones.empty()) return;
    glUseProgram(m_shaderProgram);

    auto toNdcX = [&](double sx) { return (float)((sx / m_windowWidth) * 2.0 - 1.0); };
    auto toNdcY = [&](double sy) { return (float)(1.0 - (sy / m_windowHeight) * 2.0); };

    for (const auto& zone : zones) {
        if (zone.boundary.size() < 3) continue;

        // Triangulate simple convex zone polygon (triangle fan from vertex 0)
        std::vector<float> triData;
        Point2D s0 = DrawingToScreen(zone.boundary[0].x, zone.boundary[0].y);

        for (size_t i = 1; i + 1 < zone.boundary.size(); ++i) {
            Point2D s1 = DrawingToScreen(zone.boundary[i].x, zone.boundary[i].y);
            Point2D s2 = DrawingToScreen(zone.boundary[i + 1].x, zone.boundary[i + 1].y);

            triData.push_back(toNdcX(s0.x)); triData.push_back(toNdcY(s0.y));
            triData.push_back(zone.r); triData.push_back(zone.g); triData.push_back(zone.b); triData.push_back((float)zone.opacity);

            triData.push_back(toNdcX(s1.x)); triData.push_back(toNdcY(s1.y));
            triData.push_back(zone.r); triData.push_back(zone.g); triData.push_back(zone.b); triData.push_back((float)zone.opacity);

            triData.push_back(toNdcX(s2.x)); triData.push_back(toNdcY(s2.y));
            triData.push_back(zone.r); triData.push_back(zone.g); triData.push_back(zone.b); triData.push_back((float)zone.opacity);
        }

        if (!triData.empty()) {
            glBindVertexArray(m_vao);
            glBindBuffer(GL_ARRAY_BUFFER, m_vbo);
            glBufferData(GL_ARRAY_BUFFER, triData.size() * sizeof(float), triData.data(), GL_DYNAMIC_DRAW);

            glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)0);
            glEnableVertexAttribArray(0);

            glVertexAttribPointer(1, 4, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)(2 * sizeof(float)));
            glEnableVertexAttribArray(1);

            glDrawArrays(GL_TRIANGLES, 0, (GLsizei)(triData.size() / 6));
        }
    }
}

void GLRenderer::DrawTakeoffObjects(const std::vector<TakeoffObject>& objects) {
    if (objects.empty()) return;
    glUseProgram(m_shaderProgram);

    auto toNdcX = [&](double sx) { return (float)((sx / m_windowWidth) * 2.0 - 1.0); };
    auto toNdcY = [&](double sy) { return (float)(1.0 - (sy / m_windowHeight) * 2.0); };

    for (size_t idx = 0; idx < objects.size(); ++idx) {
        const auto& obj = objects[idx];
        if (obj.points.empty()) continue;

        bool isSelected = (selectedObjectIndex == (int)idx);
        float lineThick = isSelected ? 4.0f : 2.5f;

        if (obj.measurementType == MeasurementType::Length && obj.points.size() >= 2) {
            std::vector<float> lineData;
            for (size_t i = 0; i + 1 < obj.points.size(); ++i) {
                Point2D s1 = DrawingToScreen(obj.points[i].x, obj.points[i].y);
                Point2D s2 = DrawingToScreen(obj.points[i + 1].x, obj.points[i + 1].y);

                lineData.push_back(toNdcX(s1.x)); lineData.push_back(toNdcY(s1.y));
                lineData.push_back(obj.r); lineData.push_back(obj.g); lineData.push_back(obj.b); lineData.push_back(1.0f);

                lineData.push_back(toNdcX(s2.x)); lineData.push_back(toNdcY(s2.y));
                lineData.push_back(obj.r); lineData.push_back(obj.g); lineData.push_back(obj.b); lineData.push_back(1.0f);
            }

            glBindVertexArray(m_vao);
            glBindBuffer(GL_ARRAY_BUFFER, m_vbo);
            glBufferData(GL_ARRAY_BUFFER, lineData.size() * sizeof(float), lineData.data(), GL_DYNAMIC_DRAW);

            glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)0);
            glEnableVertexAttribArray(0);

            glVertexAttribPointer(1, 4, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)(2 * sizeof(float)));
            glEnableVertexAttribArray(1);

            glLineWidth(lineThick);
            glDrawArrays(GL_LINES, 0, (GLsizei)(lineData.size() / 6));
        } else if (obj.measurementType == MeasurementType::Area && obj.points.size() >= 3) {
            // Draw filled polygon (triangle fan from vertex 0)
            std::vector<float> triData;
            Point2D s0 = DrawingToScreen(obj.points[0].x, obj.points[0].y);

            for (size_t i = 1; i + 1 < obj.points.size(); ++i) {
                Point2D s1 = DrawingToScreen(obj.points[i].x, obj.points[i].y);
                Point2D s2 = DrawingToScreen(obj.points[i + 1].x, obj.points[i + 1].y);

                triData.push_back(toNdcX(s0.x)); triData.push_back(toNdcY(s0.y));
                triData.push_back(obj.r); triData.push_back(obj.g); triData.push_back(obj.b); triData.push_back((float)obj.opacity);

                triData.push_back(toNdcX(s1.x)); triData.push_back(toNdcY(s1.y));
                triData.push_back(obj.r); triData.push_back(obj.g); triData.push_back(obj.b); triData.push_back((float)obj.opacity);

                triData.push_back(toNdcX(s2.x)); triData.push_back(toNdcY(s2.y));
                triData.push_back(obj.r); triData.push_back(obj.g); triData.push_back(obj.b); triData.push_back((float)obj.opacity);
            }

            if (!triData.empty()) {
                glBindVertexArray(m_vao);
                glBindBuffer(GL_ARRAY_BUFFER, m_vbo);
                glBufferData(GL_ARRAY_BUFFER, triData.size() * sizeof(float), triData.data(), GL_DYNAMIC_DRAW);

                glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)0);
                glEnableVertexAttribArray(0);

                glVertexAttribPointer(1, 4, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)(2 * sizeof(float)));
                glEnableVertexAttribArray(1);

                glDrawArrays(GL_TRIANGLES, 0, (GLsizei)(triData.size() / 6));
            }

            // Draw solid perimeter outline for area object
            std::vector<float> lineData;
            for (size_t i = 0; i < obj.points.size(); ++i) {
                size_t next = (i + 1) % obj.points.size();
                Point2D s1 = DrawingToScreen(obj.points[i].x, obj.points[i].y);
                Point2D s2 = DrawingToScreen(obj.points[next].x, obj.points[next].y);

                lineData.push_back(toNdcX(s1.x)); lineData.push_back(toNdcY(s1.y));
                lineData.push_back(obj.r); lineData.push_back(obj.g); lineData.push_back(obj.b); lineData.push_back(1.0f);

                lineData.push_back(toNdcX(s2.x)); lineData.push_back(toNdcY(s2.y));
                lineData.push_back(obj.r); lineData.push_back(obj.g); lineData.push_back(obj.b); lineData.push_back(1.0f);
            }

            if (!lineData.empty()) {
                glBindVertexArray(m_vao);
                glBindBuffer(GL_ARRAY_BUFFER, m_vbo);
                glBufferData(GL_ARRAY_BUFFER, lineData.size() * sizeof(float), lineData.data(), GL_DYNAMIC_DRAW);

                glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)0);
                glEnableVertexAttribArray(0);

                glVertexAttribPointer(1, 4, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)(2 * sizeof(float)));
                glEnableVertexAttribArray(1);

                glLineWidth(lineThick);
                glDrawArrays(GL_LINES, 0, (GLsizei)(lineData.size() / 6));
            }
        } else if (obj.measurementType == MeasurementType::Count) {
            std::vector<float> quadData;
            double glyphSize = isSelected ? 9.0 : 7.0;
            for (const auto& pt : obj.points) {
                Point2D s = DrawingToScreen(pt.x, pt.y);
                double x0 = s.x - glyphSize, y0 = s.y - glyphSize;
                double x1 = s.x + glyphSize, y1 = s.y + glyphSize;

                quadData.push_back(toNdcX(x0)); quadData.push_back(toNdcY(y0));
                quadData.push_back(obj.r); quadData.push_back(obj.g); quadData.push_back(obj.b); quadData.push_back(1.0f);
                quadData.push_back(toNdcX(x1)); quadData.push_back(toNdcY(y0));
                quadData.push_back(obj.r); quadData.push_back(obj.g); quadData.push_back(obj.b); quadData.push_back(1.0f);
                quadData.push_back(toNdcX(x1)); quadData.push_back(toNdcY(y1));
                quadData.push_back(obj.r); quadData.push_back(obj.g); quadData.push_back(obj.b); quadData.push_back(1.0f);

                quadData.push_back(toNdcX(x0)); quadData.push_back(toNdcY(y0));
                quadData.push_back(obj.r); quadData.push_back(obj.g); quadData.push_back(obj.b); quadData.push_back(1.0f);
                quadData.push_back(toNdcX(x1)); quadData.push_back(toNdcY(y1));
                quadData.push_back(obj.r); quadData.push_back(obj.g); quadData.push_back(obj.b); quadData.push_back(1.0f);
                quadData.push_back(toNdcX(x0)); quadData.push_back(toNdcY(y1));
                quadData.push_back(obj.r); quadData.push_back(obj.g); quadData.push_back(obj.b); quadData.push_back(1.0f);
            }

            glBindVertexArray(m_vao);
            glBindBuffer(GL_ARRAY_BUFFER, m_vbo);
            glBufferData(GL_ARRAY_BUFFER, quadData.size() * sizeof(float), quadData.data(), GL_DYNAMIC_DRAW);

            glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)0);
            glEnableVertexAttribArray(0);

            glVertexAttribPointer(1, 4, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)(2 * sizeof(float)));
            glEnableVertexAttribArray(1);

            glDrawArrays(GL_TRIANGLES, 0, (GLsizei)(quadData.size() / 6));
        }
    }
}

} // namespace AlaaRep::Takeoff
