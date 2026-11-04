namespace AlaaRep.Takeoff.Core.Models;

public enum UnitSystem
{
    Metric,
    Imperial
}

public class ScaleCalibration
{
    public UnitSystem UnitSystem { get; set; } = UnitSystem.Metric;
    public string ScaleLabel { get; set; } = "1:100 (Metric)";
    public double CalibrationPixelDistance { get; set; } = 100.0;
    public double CalibrationRealDistance { get; set; } = 5.0;
    public string LinearUnit { get; set; } = "m";
    public string AreaUnit { get; set; } = "m²";

    public double UnitsPerCoordinate => CalibrationPixelDistance > 0
        ? CalibrationRealDistance / CalibrationPixelDistance
        : 1.0;
}
