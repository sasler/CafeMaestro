using System.Collections.ObjectModel;
using System.Globalization;
using CafeMaestro.Models;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

[QueryProperty(nameof(EditRoastId), "EditRoastId")]
public partial class RoastEditPageViewModel : ObservableObject
{
    private readonly IRoastDataService _roastDataService;
    private readonly IBeanDataService _beanDataService;
    private readonly IRoastLevelService _roastLevelService;
    private readonly INavigationService _navigationService;
    private readonly IAlertService _alertService;
    private RoastData? _roast;

    [ObservableProperty] public partial string EditRoastId { get; set; } = string.Empty;
    [ObservableProperty] public partial ObservableCollection<BeanData> AvailableBeans { get; set; } = [];
    [ObservableProperty] public partial BeanData? SelectedBean { get; set; }
    [ObservableProperty] public partial string TemperatureText { get; set; } = string.Empty;
    [ObservableProperty] public partial string BatchWeightText { get; set; } = string.Empty;
    [ObservableProperty] public partial string FinalWeightText { get; set; } = string.Empty;
    [ObservableProperty] public partial string RoastTimeText { get; set; } = string.Empty;
    [ObservableProperty] public partial string FirstCrackTimeText { get; set; } = string.Empty;
    [ObservableProperty] public partial string Notes { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public RoastEditPageViewModel(
        IRoastDataService roastDataService,
        IBeanDataService beanDataService,
        IRoastLevelService roastLevelService,
        INavigationService navigationService,
        IAlertService alertService)
    {
        _roastDataService = roastDataService;
        _beanDataService = beanDataService;
        _roastLevelService = roastLevelService;
        _navigationService = navigationService;
        _alertService = alertService;
    }

    public async Task OnAppearingAsync()
    {
        if (!Guid.TryParse(EditRoastId, out Guid roastId))
        {
            await _alertService.ShowAlertAsync("Edit roast", "The selected roast could not be identified.", "OK");
            await _navigationService.GoBackAsync();
            return;
        }

        IsBusy = true;
        try
        {
            AvailableBeans = new ObservableCollection<BeanData>(await _beanDataService.GetSortedAvailableBeansAsync());
            _roast = await _roastDataService.GetRoastLogByIdAsync(roastId);
            if (_roast is null)
            {
                await _alertService.ShowAlertAsync("Edit roast", "That roast no longer exists.", "OK");
                await _navigationService.GoBackAsync();
                return;
            }

            SelectedBean = AvailableBeans.FirstOrDefault(bean => bean.Id == _roast.BeanId)
                ?? AvailableBeans.FirstOrDefault(bean => bean.DisplayName == _roast.BeanType);
            TemperatureText = _roast.Temperature.ToString("0.#", CultureInfo.CurrentCulture);
            BatchWeightText = _roast.BatchWeight.ToString("0.#", CultureInfo.CurrentCulture);
            FinalWeightText = _roast.FinalWeight?.ToString("0.#", CultureInfo.CurrentCulture) ?? string.Empty;
            RoastTimeText = _roast.FormattedTime;
            FirstCrackTimeText = _roast.FirstCrackSeconds.HasValue ? _roast.FirstCrackTime : string.Empty;
            Notes = _roast.Notes;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_roast is null || SelectedBean is null ||
            !TryParseNumber(TemperatureText, out double temperature) || temperature is <= 0 or > 500 ||
            !TryParseNumber(BatchWeightText, out double batchWeight) || batchWeight <= 0 ||
            !TryParseOptionalNumber(FinalWeightText, out double? finalWeight) || finalWeight > batchWeight ||
            !TryParseTime(RoastTimeText, out int roastMinutes, out int roastSeconds) ||
            !TryParseOptionalTime(FirstCrackTimeText, out int? firstCrackMinutes, out int? firstCrackSeconds) ||
            ((firstCrackMinutes * 60) + firstCrackSeconds) > ((roastMinutes * 60) + roastSeconds))
        {
            await _alertService.ShowAlertAsync(
                "Invalid roast",
                "Check the bean, temperature, weights, and mm:ss times before saving.",
                "OK");
            return;
        }

        IsBusy = true;
        try
        {
            RoastData updated = CopyForEdit(_roast);
            updated.BeanId = SelectedBean.Id;
            updated.BeanType = SelectedBean.DisplayName;
            updated.BeanDisplaySnapshot = SelectedBean.DisplayName;
            updated.Temperature = temperature;
            updated.BatchWeight = batchWeight;
            updated.FinalWeight = finalWeight;
            updated.RoastMinutes = roastMinutes;
            updated.RoastSeconds = roastSeconds;
            updated.FirstCrackMinutes = firstCrackMinutes;
            updated.FirstCrackSeconds = firstCrackSeconds;
            updated.Notes = Notes.Trim();
            updated.CompletionStatus = finalWeight > 0
                ? RoastCompletionStatus.Complete
                : RoastCompletionStatus.AwaitingWeight;
            updated.RoastLevelName = finalWeight > 0
                ? await _roastLevelService.GetRoastLevelNameAsync(updated.WeightLossPercentage)
                : string.Empty;

            if (!await _roastDataService.UpdateRoastLogAsync(updated))
            {
                await _alertService.ShowAlertAsync("Edit roast", "The roast could not be saved.", "OK");
                return;
            }

            await _navigationService.GoBackAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task CancelAsync() => _navigationService.GoBackAsync();

    private static RoastData CopyForEdit(RoastData source) => new()
    {
        Id = source.Id,
        BeanType = source.BeanType,
        Temperature = source.Temperature,
        BatchWeight = source.BatchWeight,
        FinalWeight = source.FinalWeight,
        RoastMinutes = source.RoastMinutes,
        RoastSeconds = source.RoastSeconds,
        RoastDate = source.RoastDate,
        Notes = source.Notes,
        RoastLevelName = source.RoastLevelName,
        FirstCrackMinutes = source.FirstCrackMinutes,
        FirstCrackSeconds = source.FirstCrackSeconds,
        BeanId = source.BeanId,
        BeanDisplaySnapshot = source.BeanDisplaySnapshot,
        SessionId = source.SessionId,
        BatchNumber = source.BatchNumber,
        DroppedAtUtc = source.DroppedAtUtc,
        CoolingDurationSeconds = source.CoolingDurationSeconds,
        CompletionStatus = source.CompletionStatus
    };

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && double.IsFinite(value);

    private static bool TryParseOptionalNumber(string text, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!TryParseNumber(text, out double parsed) || parsed <= 0)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryParseTime(string text, out int minutes, out int seconds)
    {
        minutes = 0;
        seconds = 0;
        string[] parts = text.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.None, CultureInfo.CurrentCulture, out minutes) && minutes >= 0 &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.CurrentCulture, out seconds) && seconds is >= 0 and < 60;
    }

    private static bool TryParseOptionalTime(string text, out int? minutes, out int? seconds)
    {
        minutes = null;
        seconds = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!TryParseTime(text, out int parsedMinutes, out int parsedSeconds))
        {
            return false;
        }

        minutes = parsedMinutes;
        seconds = parsedSeconds;
        return true;
    }
}
