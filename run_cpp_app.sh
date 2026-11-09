#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CPP_DIR="$SCRIPT_DIR/src/AlaaRep.Takeoff.Cpp"

echo "=== Building AlaaRep Takeoff Studio (Pure C++ / OpenGL 3.3) ==="
mkdir -p "$CPP_DIR/build"
cmake -B "$CPP_DIR/build" "$CPP_DIR" -DCMAKE_BUILD_TYPE=Release
cmake --build "$CPP_DIR/build" --config Release -j$(nproc)

echo "=== Build Complete! Executable located at: $CPP_DIR/build/takeoff_studio ==="

if [ "$1" == "--test" ]; then
    echo "Running C++ unit tests..."
    "$CPP_DIR/build/takeoff_cpp_tests"
    exit 0
fi

# Pass all CLI arguments to the C++ executable
exec "$CPP_DIR/build/takeoff_studio" "$@"
