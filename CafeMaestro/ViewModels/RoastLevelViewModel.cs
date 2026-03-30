using CommunityToolkit.Mvvm.ComponentModel;
using CafeMaestro.Models;

namespace CafeMaestro.ViewModels;

public partial class RoastLevelViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private double _minWeightLossPercentage;

    [ObservableProperty]
    private double _maxWeightLossPercentage;

    public string DisplayRange => $"{MinWeightLossPercentage:F1}% - {MaxWeightLossPercentage:F1}% weight loss";

    partial void OnMinWeightLossPercentageChanged(double value)
    {
        OnPropertyChanged(nameof(DisplayRange));
    }

    partial void OnMaxWeightLossPercentageChanged(double value)
    {
        OnPropertyChanged(nameof(DisplayRange));
    }

    public static RoastLevelViewModel FromModel(RoastLevelData model)
    {
        return new RoastLevelViewModel
        {
            Id = model.Id,
            Name = model.Name,
            MinWeightLossPercentage = model.MinWeightLossPercentage,
            MaxWeightLossPercentage = model.MaxWeightLossPercentage
        };
    }

    public RoastLevelData ToModel()
    {
        return new RoastLevelData
        {
            Id = Id,
            Name = Name,
            MinWeightLossPercentage = MinWeightLossPercentage,
            MaxWeightLossPercentage = MaxWeightLossPercentage
        };
    }
}
