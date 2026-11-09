# AlaaRep Takeoff Studio - High-Performance BIM Quantity Takeoff

Comprehensive BIM quantity takeoff (QTO) solution available both as a **100% Native C++ / OpenGL Desktop Application** and as a **C# .NET 8 WPF Desktop Studio**.

---

## 🚀 1. Pure C++ / OpenGL Desktop Application (`takeoff_studio`)

Built in native C++17 with **OpenGL 3.3 Core Profile**, **GLFW**, **GLEW**, and **Poppler-CPP / STB** for 60+ FPS zero-lag rendering of massive PDF/DWG architectural plans.

### Quick Start (Build & Run C++ App)
```bash
# Build and run directly using the root script:
./run_cpp_app.sh

# Run with large drawing image and Bluebeam markups:
./run_cpp_app.sh --image samples/architectural_plan_sample.png --bluebeam samples/sample_bluebeam_markups.csv

# Run automated C++ unit tests:
./run_cpp_app.sh --test

# Export QTO report to CSV (supports headless / CI environments):
./run_cpp_app.sh --headless --export-csv qto_report.csv
```

---

## 🖥️ 2. C# .NET 8 WPF Desktop Studio (`AlaaRep.Takeoff.Wpf`)

Built with .NET 8 and WPF, utilizing `DrawingVisual` DirectX hardware acceleration and native C++ P/Invoke geometry acceleration (`libtakeoff_spatial.so`).

### Run C# Desktop Studio
```bash
# Build solution
dotnet build AlaaRep.Takeoff.slnx

# Run test suite
dotnet test AlaaRep.Takeoff.slnx

# Run WPF Application
dotnet run --project src/AlaaRep.Takeoff.Wpf/AlaaRep.Takeoff.Wpf.csproj
```
