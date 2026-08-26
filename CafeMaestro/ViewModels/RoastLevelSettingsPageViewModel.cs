using System.Collections.ObjectModel;
using System.Globalization;
using CafeMaestro.Models;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

/// <summary>
/// Roast-level editing in its own destination. The validation, ordering and service calls are
/// the ones the previous settings page shipped with; only the surface around them changed.
/// </summary>
public partial class RoastLevelSettingsPageViewModel : ObservableObject
{
    private readonly IRoastLevelService _roastLevelService;
    private readonly IAlertService _alertService;
    private RoastLevelViewModel? _currentEditRoastLevel;
    private bool _isNewRoastLevel;

    public RoastLevelSettingsPageViewModel(
        IRoastLevelService roastLevelService,
        IAlertService alertService)
    {
        _roastLevelService = roastLevelService ??
                             throw new ArgumentNullException(nameof(roastLevelService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
    }

    [ObservableProperty]
    public partial ObservableCollection<RoastLevelViewModel> RoastLevels { get; set; } = [];

    [ObservableProperty]
    public partial bool IsEditRoastLevelPopupVisible { get; set; }

    [ObservableProperty]
    public partial string EditPopupTitle { get; set; } = "Edit Roast Level";

    [ObservableProperty]
    public partial string RoastLevelName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MinWeightLossText { get; set; } = "0.0";

    [ObservableProperty]
    public partial string MaxWeightLossText { get; set; } = "0.0";

    public bool HasRoastLevels => RoastLevels.Count > 0;

    public bool HasNoRoastLevels => RoastLevels.Count == 0;

    public string RoastLevelSummary => DescribeCount(RoastLevels.Count);

    public Task OnAppearingAsync() => LoadRoastLevelsAsync();

    /// <summary>Summary text shared with the Settings index row.</summary>
    public static string DescribeCount(int count) =>
        count == 1 ? "1 configured" : $"{count} configured";

    [RelayCommand]
    private void EditRoastLevel(RoastLevelViewModel roastLevel)
    {
        _currentEditRoastLevel = new RoastLevelViewModel
        {
            Id = roastLevel.Id,
            Name = roastLevel.Name,
            MinWeightLossPercentage = roastLevel.MinWeightLossPercentage,
            MaxWeightLossPercentage = roastLevel.MaxWeightLossPercentage
        };
        _isNewRoastLevel = false;
        EditPopupTitle = "Edit Roast Level";
        RoastLevelName = _currentEditRoastLevel.Name;
        MinWeightLossText = _currentEditRoastLevel.MinWeightLossPercentage.ToString(
            "F1",
            CultureInfo.InvariantCulture);
        MaxWeightLossText = _currentEditRoastLevel.MaxWeightLossPercentage.ToString(
            "F1",
            CultureInfo.InvariantCulture);
        IsEditRoastLevelPopupVisible = true;
    }

    [RelayCommand]
    private async Task DeleteRoastLevelAsync(RoastLevelViewModel roastLevel)
    {
        bool confirmed = await _alertService.ShowConfirmationAsync(
            "Delete Roast Level",
            $"Delete “{roastLevel.Name}”?",
            "Delete",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        bool success = await _roastLevelService.DeleteRoastLevelAsync(roastLevel.Id);
        if (!success)
        {
            await _alertService.ShowAlertAsync(
                "Delete Failed",
                "CafeMaestro could not delete the roast level.",
                "OK");
            return;
        }

        await LoadRoastLevelsAsync();
    }

    [RelayCommand]
    private void AddRoastLevel()
    {
        _currentEditRoastLevel = new RoastLevelViewModel
        {
            Id = Guid.NewGuid()
        };
        _isNewRoastLevel = true;
        EditPopupTitle = "Add Roast Level";
        RoastLevelName = string.Empty;
        MinWeightLossText = "0.0";
        MaxWeightLossText = "0.0";
        IsEditRoastLevelPopupVisible = true;
    }

    [RelayCommand]
    private async Task SaveRoastLevelAsync()
    {
        if (_currentEditRoastLevel is null)
        {
            IsEditRoastLevelPopupVisible = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(RoastLevelName) ||
            !double.TryParse(
                MinWeightLossText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double minimum) ||
            !double.TryParse(
                MaxWeightLossText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double maximum) ||
            minimum < 0 ||
            maximum <= minimum)
        {
            await _alertService.ShowAlertAsync(
                "Invalid Roast Level",
                "Enter a name and a valid range where the maximum is greater than the minimum.",
                "OK");
            return;
        }

        _currentEditRoastLevel.Name = RoastLevelName.Trim();
        _currentEditRoastLevel.MinWeightLossPercentage = minimum;
        _currentEditRoastLevel.MaxWeightLossPercentage = maximum;
        bool success = _isNewRoastLevel
            ? await _roastLevelService.AddRoastLevelAsync(_currentEditRoastLevel.ToModel())
            : await _roastLevelService.UpdateRoastLevelAsync(_currentEditRoastLevel.ToModel());

        if (!success)
        {
            await _alertService.ShowAlertAsync(
                "Save Failed",
                "CafeMaestro could not save the roast level.",
                "OK");
            return;
        }

        IsEditRoastLevelPopupVisible = false;
        await LoadRoastLevelsAsync();
    }

    [RelayCommand]
    private void CancelRoastLevel()
    {
        IsEditRoastLevelPopupVisible = false;
    }

    [RelayCommand]
    private async Task ResetRoastLevelsToDefaultsAsync()
    {
        bool confirmed = await _alertService.ShowConfirmationAsync(
            "Reset Roast Levels",
            "Restore the default roast levels and replace the current custom list?",
            "Reset",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        bool success = await _roastLevelService.SaveRoastLevelsAsync(
            AppDataFactory.CreateDefault().RoastLevels);
        if (!success)
        {
            await _alertService.ShowAlertAsync(
                "Reset Failed",
                "CafeMaestro could not restore the default roast levels.",
                "OK");
            return;
        }

        await LoadRoastLevelsAsync();
    }

    private async Task LoadRoastLevelsAsync()
    {
        List<RoastLevelData> roastLevels = await _roastLevelService.GetRoastLevelsAsync();
        RoastLevels = new ObservableCollection<RoastLevelViewModel>(
            roastLevels
                .OrderBy(level => level.MinWeightLossPercentage)
                .Select(RoastLevelViewModel.FromModel));
    }

    partial void OnRoastLevelsChanged(ObservableCollection<RoastLevelViewModel> value)
    {
        OnPropertyChanged(nameof(HasRoastLevels));
        OnPropertyChanged(nameof(HasNoRoastLevels));
        OnPropertyChanged(nameof(RoastLevelSummary));
    }
}
