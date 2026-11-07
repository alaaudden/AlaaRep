using System.Windows;
using System.Windows.Media;

namespace AlaaRep.Takeoff.Wpf;

/// <summary>
/// High-performance visual canvas that renders vector drawing elements directly into a DrawingVisual.
/// Replaces slow FrameworkElement tree allocation, achieving 60+ FPS hardware acceleration.
/// Supports GPU caching, low-latency rendering, and configurable hardware optimization modes.
/// </summary>
public class FastDrawingCanvas : FrameworkElement
{
    private readonly DrawingVisual _visual = new();
    private bool _gpuAccelerationEnabled = true;

    public FastDrawingCanvas()
    {
        AddVisualChild(_visual);
        AddLogicalChild(_visual);

        ApplyHardwareAccelerationOptions();
    }

    /// <summary>
    /// Gets or sets whether GPU hardware acceleration and BitmapCache mode are enabled.
    /// </summary>
    public bool GpuAccelerationEnabled
    {
        get => _gpuAccelerationEnabled;
        set
        {
            if (_gpuAccelerationEnabled != value)
            {
                _gpuAccelerationEnabled = value;
                ApplyHardwareAccelerationOptions();
            }
        }
    }

    private void ApplyHardwareAccelerationOptions()
    {
        if (_gpuAccelerationEnabled)
        {
            // Configure rendering hints for maximum GPU throughput and crisp lines
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            RenderOptions.SetClearTypeHint(this, ClearTypeHint.Auto);

            // Enable GPU bitmap caching
            CacheMode = new BitmapCache
            {
                EnableClearType = false,
                RenderAtScale = 1.0,
                SnapsToDevicePixels = true
            };
        }
        else
        {
            // High fidelity / standard software or CPU-safe vector mode
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);

            CacheMode = null;
        }
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _visual;

    public DrawingContext RenderOpen() => _visual.RenderOpen();
}
