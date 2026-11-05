using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AlaaRep.Takeoff.Core.Models;

namespace AlaaRep.Takeoff.Wpf;

public partial class CalibrationDialog : Window
{
    public double PixelDistance { get; }
    public double RealDistance { get; private set; } = 5.0;
    public string Unit { get; private set; } = "m";
    public string AreaUnit { get; private set; } = "m²";
    public string ScaleLabel { get; private set; } = "Calibrated";

    public CalibrationDialog(double pixelDistance, ScaleCalibration currentCalibration)
    {
        InitializeComponent();
        PixelDistance = pixelDistance;
        TxtPixelDist.Text = $"{pixelDistance:F2} px";
        TxtRealDist.Text = currentCalibration.CalibrationRealDistance > 0
            ? currentCalibration.CalibrationRealDistance.ToString("G", CultureInfo.InvariantCulture)
            : "5.0";

        SelectUnit(currentCalibration.LinearUnit);
        TxtRealDist.TextChanged += (s, e) => UpdatePreview();
        CbUnit.SelectionChanged += (s, e) => UpdatePreview();
        UpdatePreview();
    }

    private void SelectUnit(string unit)
    {
        foreach (ComboBoxItem item in CbUnit.Items)
        {
            if (item.Tag?.ToString() == unit)
            {
                CbUnit.SelectedItem = item;
                break;
            }
        }
    }

    private void UpdatePreview()
    {
        if (TxtPreview == null) return;

        if (double.TryParse(TxtRealDist.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double realDist) && realDist > 0)
        {
            var unitTag = (CbUnit.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "m";
            double unitsPerPx = realDist / PixelDistance;
            TxtPreview.Text = $"{PixelDistance:F1} px = {realDist:F2} {unitTag} (1 px = {unitsPerPx:F4} {unitTag})";
        }
        else
        {
            TxtPreview.Text = "Please enter a valid positive distance.";
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(TxtRealDist.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double realDist) || realDist <= 0)
        {
            MessageBox.Show("Please enter a valid positive number for the real distance.", "Invalid Distance", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RealDistance = realDist;
        Unit = (CbUnit.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "m";
        AreaUnit = Unit switch
        {
            "mm" => "mm²",
            "cm" => "cm²",
            "ft" => "sq ft",
            "in" => "sq in",
            _ => "m²"
        };
        ScaleLabel = $"Calibrated ({PixelDistance:F0}px = {RealDistance:F1}{Unit})";

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
