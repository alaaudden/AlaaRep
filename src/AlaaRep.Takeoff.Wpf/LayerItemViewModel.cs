using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AlaaRep.Takeoff.Wpf;

public class LayerItemViewModel : INotifyPropertyChanged
{
    private bool _isVisible = true;
    private string _name = string.Empty;
    private int _count;
    private string _layerType = "CAD"; // "CAD" or "Takeoff"
    private string _colorHex = "#3B82F6";

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; OnPropertyChanged(); }
    }

    public int Count
    {
        get => _count;
        set { _count = value; OnPropertyChanged(); }
    }

    public string LayerType
    {
        get => _layerType;
        set { _layerType = value; OnPropertyChanged(); }
    }

    public string ColorHex
    {
        get => _colorHex;
        set { _colorHex = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
