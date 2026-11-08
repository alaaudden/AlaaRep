#pragma once

#include "TakeoffModels.h"
#include <string>

namespace AlaaRep::Takeoff {

class TakeoffParsers {
public:
    static bool LoadPlanImage(const std::string& filePath, PlanImageData& outImage);
    static bool ReadBluebeamCsv(const std::string& csvContentOrPath, std::vector<TakeoffObject>& outObjects);
    static bool ReadDxf(const std::string& dxfFilePath, DrawingDocument& outDoc);
    static void PopulateSampleLayout(DrawingDocument& outDoc);
};

} // namespace AlaaRep::Takeoff
