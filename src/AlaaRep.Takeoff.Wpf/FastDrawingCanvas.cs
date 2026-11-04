using System.Windows;
using System.Windows.Media;

namespace AlaaRep.Takeoff.Wpf;

/// <summary>
/// High-performance visual canvas that renders vector drawing elements directly into a DrawingVisual.
/// Replaces slow FrameworkElement tree allocation, achieving 60+ FPS hardware acceleration.
/// </summary>
public class FastDrawingCanvas : FrameworkElement
{
    private readonly DrawingVisual _visual = new();

    public FastDrawingCanvas()
    {
        AddVisualChild(_visual);
        AddLogicalChild(_visual);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _visual;

    public DrawingContext RenderOpen() => _visual.RenderOpen();
}
