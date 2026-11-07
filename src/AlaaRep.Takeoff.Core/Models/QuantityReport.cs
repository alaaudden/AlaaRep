namespace AlaaRep.Takeoff.Core.Models;

public class TakeoffZoneBreakdown
{
    public string ZoneId { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public string ZoneCode { get; set; } = string.Empty;

    public int Count { get; set; }
    public double Length { get; set; }
    public double Area { get; set; }
    public double WallArea { get; set; } // Vertical wall surface area for linear walls
    public double Perimeter { get; set; }
    public double Cost { get; set; }
}

public class TakeoffItemSummary
{
    public string ObjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public TakeoffMeasurementType MeasurementType { get; set; }
    public Dictionary<string, string> BimProperties { get; set; } = new();

    public double Height { get; set; } // Wall Height for linear elements
    public int TotalCount { get; set; }
    public double TotalLength { get; set; }
    public double TotalArea { get; set; }
    public double TotalWallArea { get; set; } // Vertical wall surface area (TotalLength * Height)
    public double TotalPerimeter { get; set; }
    public double UnitCost { get; set; }
    public double TotalCost { get; set; }

    public List<TakeoffZoneBreakdown> ZoneBreakdowns { get; set; } = new();
}

public class ZoneQuantitySummary
{
    public string ZoneId { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public string ZoneCode { get; set; } = string.Empty;
    public double ZoneArea { get; set; }
    public double ZonePerimeter { get; set; }

    public int TotalObjectCount { get; set; }
    public double TotalLength { get; set; }
    public double TotalTakeoffArea { get; set; }
    public double TotalWallArea { get; set; } // Total vertical wall area within zone
    public double TotalCost { get; set; }

    public List<TakeoffItemSummary> Items { get; set; } = new();
}

public class QuantityReport
{
    public string DocumentTitle { get; set; } = "Takeoff Quantity Report";
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public ScaleCalibration Calibration { get; set; } = new();

    public int TotalItemCount { get; set; }
    public double GrandTotalLength { get; set; }
    public double GrandTotalArea { get; set; }
    public double GrandTotalWallArea { get; set; } // Total vertical wall area across all walls
    public double GrandTotalCost { get; set; }

    public List<TakeoffItemSummary> Items { get; set; } = new();
    public List<ZoneQuantitySummary> Zones { get; set; } = new();
    public Dictionary<string, double> QuantitiesByCategory { get; set; } = new();
    public Dictionary<string, double> QuantitiesByBuilding { get; set; } = new();
    public Dictionary<string, double> QuantitiesByFloor { get; set; } = new();
    public Dictionary<string, double> QuantitiesByDiscipline { get; set; } = new();
}
