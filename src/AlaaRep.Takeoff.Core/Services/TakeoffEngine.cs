using AlaaRep.Takeoff.Core.Models;

namespace AlaaRep.Takeoff.Core.Services;

public class TakeoffEngine
{
    public QuantityReport CalculateReport(DrawingDocument document)
    {
        var report = new QuantityReport
        {
            DocumentTitle = string.IsNullOrEmpty(document.FileName) ? "Takeoff Quantity Report" : $"Takeoff Report - {document.FileName}",
            Calibration = document.Calibration,
            GeneratedAtUtc = DateTime.UtcNow
        };

        double scale = document.Calibration.UnitsPerCoordinate;
        double areaScale = scale * scale;

        var zoneSummaryMap = new Dictionary<string, ZoneQuantitySummary>();
        foreach (var zone in document.Zones)
        {
            double zoneArea = SpatialCalculator.CalculatePolygonArea(zone.Boundary) * areaScale;
            double zonePerim = SpatialCalculator.CalculatePerimeter(zone.Boundary) * scale;

            var zSummary = new ZoneQuantitySummary
            {
                ZoneId = zone.Id,
                ZoneName = zone.Name,
                ZoneCode = zone.Code,
                ZoneArea = Math.Round(zoneArea, 3),
                ZonePerimeter = Math.Round(zonePerim, 3)
            };
            zoneSummaryMap[zone.Id] = zSummary;
            report.Zones.Add(zSummary);
        }

        foreach (var obj in document.TakeoffObjects)
        {
            var itemSummary = new TakeoffItemSummary
            {
                ObjectId = obj.Id,
                Name = obj.Name,
                Category = obj.Category,
                MeasurementType = obj.MeasurementType,
                BimProperties = new Dictionary<string, string>(obj.BimProperties)
            };

            double unitCost = 0.0;
            if (obj.BimProperties.TryGetValue("UnitCost", out var unitCostStr) ||
                obj.BimProperties.TryGetValue("Cost", out unitCostStr) ||
                obj.BimProperties.TryGetValue("Rate", out unitCostStr))
            {
                double.TryParse(unitCostStr.Replace("$", "").Replace("€", "").Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out unitCost);
            }
            itemSummary.UnitCost = unitCost;

            switch (obj.MeasurementType)
            {
                case TakeoffMeasurementType.Count:
                    itemSummary.TotalCount = Math.Max(1, obj.GeometryPoints.Count);
                    itemSummary.TotalCost = itemSummary.TotalCount * unitCost;
                    break;

                case TakeoffMeasurementType.Length:
                    itemSummary.TotalLength = Math.Round(SpatialCalculator.CalculatePolylineLength(obj.GeometryPoints) * scale, 3);
                    itemSummary.TotalCost = Math.Round(itemSummary.TotalLength * unitCost, 2);
                    break;

                case TakeoffMeasurementType.Area:
                    itemSummary.TotalArea = Math.Round(SpatialCalculator.CalculatePolygonArea(obj.GeometryPoints) * areaScale, 3);
                    itemSummary.TotalPerimeter = Math.Round(SpatialCalculator.CalculatePerimeter(obj.GeometryPoints) * scale, 3);
                    itemSummary.TotalCost = Math.Round(itemSummary.TotalArea * unitCost, 2);
                    break;

                case TakeoffMeasurementType.Perimeter:
                    itemSummary.TotalPerimeter = Math.Round(SpatialCalculator.CalculatePerimeter(obj.GeometryPoints) * scale, 3);
                    itemSummary.TotalCost = Math.Round(itemSummary.TotalPerimeter * unitCost, 2);
                    break;
            }

            CalculateZoneBreakdowns(obj, itemSummary, document.Zones, scale, areaScale, unitCost, zoneSummaryMap);

            report.Items.Add(itemSummary);

            report.TotalItemCount += itemSummary.TotalCount;
            report.GrandTotalLength += itemSummary.TotalLength;
            report.GrandTotalArea += itemSummary.TotalArea;
            report.GrandTotalCost += itemSummary.TotalCost;

            string cat = string.IsNullOrWhiteSpace(obj.Category) ? "Uncategorized" : obj.Category;
            double catVal = obj.MeasurementType switch
            {
                TakeoffMeasurementType.Count => itemSummary.TotalCount,
                TakeoffMeasurementType.Length => itemSummary.TotalLength,
                _ => itemSummary.TotalArea
            };
            report.QuantitiesByCategory[cat] = report.QuantitiesByCategory.GetValueOrDefault(cat) + catVal;

            // Hierarchy Rollups (Building, Floor, Discipline)
            if (!string.IsNullOrWhiteSpace(obj.Building))
            {
                report.QuantitiesByBuilding[obj.Building] = report.QuantitiesByBuilding.GetValueOrDefault(obj.Building) + itemSummary.TotalCost;
            }
            if (!string.IsNullOrWhiteSpace(obj.Floor))
            {
                report.QuantitiesByFloor[obj.Floor] = report.QuantitiesByFloor.GetValueOrDefault(obj.Floor) + itemSummary.TotalCost;
            }
            if (!string.IsNullOrWhiteSpace(obj.Discipline))
            {
                report.QuantitiesByDiscipline[obj.Discipline] = report.QuantitiesByDiscipline.GetValueOrDefault(obj.Discipline) + itemSummary.TotalCost;
            }
        }

        report.GrandTotalLength = Math.Round(report.GrandTotalLength, 3);
        report.GrandTotalArea = Math.Round(report.GrandTotalArea, 3);
        report.GrandTotalCost = Math.Round(report.GrandTotalCost, 2);

        return report;
    }

    private void CalculateZoneBreakdowns(
        TakeoffObject obj,
        TakeoffItemSummary itemSummary,
        List<Zone> zones,
        double scale,
        double areaScale,
        double unitCost,
        Dictionary<string, ZoneQuantitySummary> zoneSummaryMap)
    {
        if (!string.IsNullOrEmpty(obj.ZoneId) && zones.Any(z => z.Id == obj.ZoneId || z.Name == obj.ZoneId))
        {
            var zone = zones.First(z => z.Id == obj.ZoneId || z.Name == obj.ZoneId);
            var breakdown = new TakeoffZoneBreakdown
            {
                ZoneId = zone.Id,
                ZoneName = zone.Name,
                ZoneCode = zone.Code,
                Count = itemSummary.TotalCount,
                Length = itemSummary.TotalLength,
                Area = itemSummary.TotalArea,
                Perimeter = itemSummary.TotalPerimeter,
                Cost = itemSummary.TotalCost
            };
            itemSummary.ZoneBreakdowns.Add(breakdown);

            if (zoneSummaryMap.TryGetValue(zone.Id, out var zs))
            {
                zs.TotalObjectCount += breakdown.Count;
                zs.TotalLength += breakdown.Length;
                zs.TotalTakeoffArea += breakdown.Area;
                zs.TotalCost += breakdown.Cost;
                zs.Items.Add(itemSummary);
            }
            return;
        }

        foreach (var zone in zones)
        {
            if (zone.Boundary.Count < 3) continue;

            var breakdown = new TakeoffZoneBreakdown
            {
                ZoneId = zone.Id,
                ZoneName = zone.Name,
                ZoneCode = zone.Code
            };

            switch (obj.MeasurementType)
            {
                case TakeoffMeasurementType.Count:
                    int countInside = obj.GeometryPoints.Count(pt => SpatialCalculator.IsPointInPolygon(pt, zone.Boundary));
                    if (countInside > 0)
                    {
                        breakdown.Count = countInside;
                        breakdown.Cost = countInside * unitCost;
                        itemSummary.ZoneBreakdowns.Add(breakdown);
                    }
                    break;

                case TakeoffMeasurementType.Length:
                    double lenInside = SpatialCalculator.CalculatePolylineLengthInZone(obj.GeometryPoints, zone.Boundary) * scale;
                    if (lenInside > 1e-4)
                    {
                        breakdown.Length = Math.Round(lenInside, 3);
                        breakdown.Cost = Math.Round(lenInside * unitCost, 2);
                        itemSummary.ZoneBreakdowns.Add(breakdown);
                    }
                    break;

                case TakeoffMeasurementType.Area:
                    double areaInside = SpatialCalculator.CalculatePolygonAreaInZone(obj.GeometryPoints, zone.Boundary) * areaScale;
                    if (areaInside > 1e-4)
                    {
                        breakdown.Area = Math.Round(areaInside, 3);
                        breakdown.Cost = Math.Round(areaInside * unitCost, 2);
                        itemSummary.ZoneBreakdowns.Add(breakdown);
                    }
                    break;

                case TakeoffMeasurementType.Perimeter:
                    double perimInside = SpatialCalculator.CalculatePolylineLengthInZone(obj.GeometryPoints, zone.Boundary) * scale;
                    if (perimInside > 1e-4)
                    {
                        breakdown.Perimeter = Math.Round(perimInside, 3);
                        breakdown.Cost = Math.Round(perimInside * unitCost, 2);
                        itemSummary.ZoneBreakdowns.Add(breakdown);
                    }
                    break;
            }

            if (breakdown.Count > 0 || breakdown.Length > 0 || breakdown.Area > 0 || breakdown.Perimeter > 0)
            {
                if (zoneSummaryMap.TryGetValue(zone.Id, out var zs))
                {
                    zs.TotalObjectCount += breakdown.Count;
                    zs.TotalLength += breakdown.Length;
                    zs.TotalTakeoffArea += breakdown.Area;
                    zs.TotalCost += breakdown.Cost;
                    zs.Items.Add(itemSummary);
                }
            }
        }
    }
}
