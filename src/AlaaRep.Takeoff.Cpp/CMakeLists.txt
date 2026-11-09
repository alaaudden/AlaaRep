cmake_minimum_required(VERSION 3.16)
project(takeoff_studio VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include_directories(
    include
    imgui
    /usr/include
    /usr/include/stb
)

find_package(OpenGL REQUIRED)
find_package(GLEW REQUIRED)
find_package(glfw3 REQUIRED)
find_package(PkgConfig REQUIRED)
pkg_check_modules(POPPLER REQUIRED poppler-cpp)

# Common library for engine and parsers
add_library(takeoff_cpp_core STATIC
    src/TakeoffParsers.cpp
    src/TakeoffEngine.cpp
    src/GLRenderer.cpp
    imgui/imgui.cpp
    imgui/imgui_draw.cpp
    imgui/imgui_tables.cpp
    imgui/imgui_widgets.cpp
    imgui/imgui_impl_glfw.cpp
    imgui/imgui_impl_opengl3.cpp
)

target_include_directories(takeoff_cpp_core PUBLIC
    ${POPPLER_INCLUDE_DIRS}
)

target_link_libraries(takeoff_cpp_core PUBLIC
    OpenGL::GL
    GLEW::GLEW
    glfw
    ${POPPLER_LIBRARIES}
)

# Optimize for max speed
if(MSVC)
    target_compile_options(takeoff_cpp_core PRIVATE /O2 /fp:fast)
else()
    target_compile_options(takeoff_cpp_core PRIVATE -O3 -ffast-math)
endif()

# Main desktop executable
add_executable(takeoff_studio
    src/main.cpp
)

target_link_libraries(takeoff_studio PRIVATE
    takeoff_cpp_core
)

# Automated C++ test executable
add_executable(takeoff_cpp_tests
    tests/test_main.cpp
)

target_link_libraries(takeoff_cpp_tests PRIVATE
    takeoff_cpp_core
)
