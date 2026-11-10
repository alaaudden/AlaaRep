using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AlaaRep.Xer.Entities;

public class TaskRelationshipItem : INotifyPropertyChanged
{
    private string _relationshipId = string.Empty;
    private string _relatedTaskId = string.Empty;
    private string _relatedTaskCode = string.Empty;
    private string _relatedTaskName = string.Empty;
    private string _type = "PR_FS";
    private double _lagDays;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Direction { get; set; } = "Predecessor"; // "Predecessor" or "Successor"

    public string RelationshipId
    {
        get => _relationshipId;
        set { _relationshipId = value; OnPropertyChanged(); }
    }

    public string RelatedTaskId
    {
        get => _relatedTaskId;
        set { _relatedTaskId = value; OnPropertyChanged(); }
    }

    public string RelatedTaskCode
    {
        get => _relatedTaskCode;
        set { _relatedTaskCode = value; OnPropertyChanged(); }
    }

    public string RelatedTaskName
    {
        get => _relatedTaskName;
        set { _relatedTaskName = value; OnPropertyChanged(); }
    }

    public string Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypeDisplayName)); }
    }

    public string TypeDisplayName
    {
        get => _type switch
        {
            "PR_FS" => "Finish to Start (FS)",
            "PR_SS" => "Start to Start (SS)",
            "PR_FF" => "Finish to Finish (FF)",
            "PR_SF" => "Start to Finish (SF)",
            _ => _type
        };
        set
        {
            _type = value switch
            {
                "Finish to Start (FS)" => "PR_FS",
                "Start to Start (SS)" => "PR_SS",
                "Finish to Finish (FF)" => "PR_FF",
                "Start to Finish (SF)" => "PR_SF",
                _ => value
            };
            OnPropertyChanged();
            OnPropertyChanged(nameof(Type));
        }
    }

    public double LagDays
    {
        get => _lagDays;
        set { _lagDays = value; OnPropertyChanged(); }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
