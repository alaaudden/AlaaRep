using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AlaaRep.Xer.Entities;

namespace AlaaRep.Xer.Wpf;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        if (parameter?.ToString() == "Inverse") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility vis && vis == Visibility.Visible;
    }
}

public class NodeColorConverter : IValueConverter
{
    private static readonly SolidColorBrush WbsBrush = new(Color.FromRgb(33, 37, 41)); // Dark / Black
    private static readonly SolidColorBrush CriticalBrush = new(Color.FromRgb(220, 53, 69)); // Red #DC3545
    private static readonly SolidColorBrush NormalBrush = new(Color.FromRgb(13, 110, 253)); // Blue #0D6EFD
    private static readonly SolidColorBrush MilestoneBrush = new(Color.FromRgb(255, 193, 7)); // Gold #FFC107

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ScheduleItemNode node)
        {
            if (node.IsWbs) return WbsBrush;
            if (node.IsMilestone) return MilestoneBrush;
            if (node.IsCritical) return CriticalBrush;
            return NormalBrush;
        }
        return NormalBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
