cmake_minimum_required(VERSION 3.16)
project(takeoff_spatial VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include_directories(include)

add_library(takeoff_spatial SHARED
    src/spatial_engine.cpp
)

set_target_properties(takeoff_spatial PROPERTIES
    OUTPUT_NAME "takeoff_spatial"
    PREFIX "lib"
)

# Optimize for max speed
if(MSVC)
    target_compile_options(takeoff_spatial PRIVATE /O2 /fp:fast)
else()
    target_compile_options(takeoff_spatial PRIVATE -O3 -ffast-math -fPIC)
endif()
