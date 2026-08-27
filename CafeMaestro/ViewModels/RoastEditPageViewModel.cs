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
    [ObservableProperty] public partial string BeanError { get; set; } = string.Empty;
    [ObservableProperty] public partial string TemperatureError { get; set; } = string.Empty;
    [ObservableProperty] public partial string BatchWeightError { get; set; } = string.Empty;
    [ObservableProperty] public partial string FinalWeightError { get; set; } = string.Empty;
    [ObservableProperty] public partial string RoastTimeError { get; set; } = string.Empty;
    [ObservableProperty] public partial string FirstCrackError { get; set; } = string.Empty;
    [ObservableProperty] public partial string FocusField { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public string FinalWeightDisplay => string.IsNullOrWhiteSpace(FinalWeightText)
        ? "—"
        : $"{FinalWeightText} g out";

    public RoastEditPageViewModel(
        IRoastDataService roastDataService,
        IBeanDataService beanDataService,
        INavigationService navigationService,
        IAlertService alertService)
    {
        _roastDataService = roastDataService;
        _beanDataService = beanDataService;
        _navigationService = navigationService;
        _alertService = alertService;
    }

    partial void OnSelectedBeanChanged(BeanData? value) => BeanError = string.Empty;

    partial void OnTemperatureTextChanged(string value) => TemperatureError = string.Empty;

    partial void OnBatchWeightTextChanged(string value) => BatchWeightError = string.Empty;

    partial void OnFinalWeightTextChanged(string value)
    {
        FinalWeightError = string.Empty;
        OnPropertyChanged(nameof(FinalWeightDisplay));
    }

    partial void OnRoastTimeTextChanged(string value) => RoastTimeError = string.Empty;

    partial void OnFirstCrackTimeTextChanged(string value) => FirstCrackError = string.Empty;

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

            SelectedBean = RoastProjection.ResolveBean(_roast, AvailableBeans);
            TemperatureText = _roast.Temperature.ToString("0.#", CultureInfo.CurrentCulture);
            BatchWeightText = _roast.BatchWeight.ToString("0.#", CultureInfo.CurrentCulture);
            FinalWeightText = _roast.FinalWeight?.ToString("0.#", CultureInfo.CurrentCulture) ?? string.Empty;
            RoastTimeText = _roast.FormattedTime;
            FirstCrackTimeText = _roast.FirstCrackSeconds.HasValue ? _roast.FirstCrackTime : string.Empty;
            Notes = _roast.Notes;
            ClearValidationErrors();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_roast is null)
        {
            await _alertService.ShowAlertAsync(
                "Invalid roast",
                "The selected roast could not be loaded.",
                "OK");
            return;
        }

        if (!TryValidate(
                out double temperature,
                out double batchWeight,
                out double? finalWeight,
                out int roastMinutes,
                out int roastSeconds,
                out int? firstCrackMinutes,
                out int? firstCrackSeconds))
        {
            return;
        }

        IsBusy = true;
        try
        {
            RoastData updated = CopyForEdit(_roast);
            if (SelectedBean is not null &&
                (!_roast.BeanId.HasValue || _roast.BeanId.Value != SelectedBean.Id))
            {
                // Keep the recorded name/snapshot stable when the picker resolved the same
                // bean ID. Only an explicit different-bean selection should rewrite provenance.
                updated.BeanId = SelectedBean.Id;
                updated.BeanType = SelectedBean.DisplayName;
                updated.BeanDisplaySnapshot = SelectedBean.DisplayName;
            }
            updated.Temperature = temperature;
            updated.BatchWeight = batchWeight;
            updated.FinalWeight = finalWeight;
            updated.RoastMinutes = roastMinutes;
            updated.RoastSeconds = roastSeconds;
            updated.FirstCrackMinutes = firstCrackMinutes;
            updated.FirstCrackSeconds = firstCrackSeconds;
            updated.Notes = Notes.Trim();

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
        (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
         double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) &&
        double.IsFinite(value);

    private bool TryValidate(
        out double temperature,
        out double batchWeight,
        out double? finalWeight,
        out int roastMinutes,
        out int roastSeconds,
        out int? firstCrackMinutes,
        out int? firstCrackSeconds)
    {
        temperature = 0;
        batchWeight = 0;
        finalWeight = null;
        roastMinutes = 0;
        roastSeconds = 0;
        firstCrackMinutes = null;
        firstCrackSeconds = null;

        // A deleted bean or an ambiguous legacy name intentionally has no picker selection.
        // Keep the copied identity in that case so editing another field never erases provenance.
        BeanError = string.Empty;

        bool temperatureValid = TryParseNumber(TemperatureText, out temperature) &&
                                temperature > 0 && temperature <= 500;
        TemperatureError = temperatureValid
            ? string.Empty
            : "Enter a temperature between 0 and 500 °C.";

        bool batchWeightValid = TryParseNumber(BatchWeightText, out batchWeight) && batchWeight > 0;
        BatchWeightError = batchWeightValid
            ? string.Empty
            : "Enter a batch weight greater than 0 g.";

        double parsedFinalWeight = 0;
        bool finalWeightValid = string.IsNullOrWhiteSpace(FinalWeightText) ||
                                (TryParseNumber(FinalWeightText, out parsedFinalWeight) &&
                                 parsedFinalWeight > 0);
        if (finalWeightValid && !string.IsNullOrWhiteSpace(FinalWeightText))
        {
            finalWeight = parsedFinalWeight;
        }

        if (!finalWeightValid)
        {
            FinalWeightError = "Enter a final weight greater than 0 g, or leave it blank.";
        }
        else if (finalWeight.HasValue && batchWeightValid && finalWeight.Value > batchWeight)
        {
            FinalWeightError =
                $"Final weight is above {batchWeight:0.#} g loaded — correct the batch weight if that is the mistake.";
        }
        else
        {
            FinalWeightError = string.Empty;
        }

        bool roastTimeValid = TryParseTime(RoastTimeText, out roastMinutes, out roastSeconds);
        RoastTimeError = roastTimeValid ? string.Empty : "Enter roast time as mm:ss.";

        bool firstCrackValid = TryParseOptionalTime(
            FirstCrackTimeText, out firstCrackMinutes, out firstCrackSeconds);
        bool firstCrackWithinRoast = roastTimeValid && firstCrackValid &&
            (!firstCrackMinutes.HasValue ||
             (firstCrackMinutes.Value * 60 + firstCrackSeconds.GetValueOrDefault()) <=
             (roastMinutes * 60 + roastSeconds));
        if (!firstCrackValid)
        {
            FirstCrackError = "Enter First Crack as mm:ss, or leave it blank.";
        }
        else if (roastTimeValid && !firstCrackWithinRoast)
        {
            FirstCrackError = "First Crack must be within the total roast time.";
        }
        else
        {
            FirstCrackError = string.Empty;
        }

        bool valid = string.IsNullOrWhiteSpace(BeanError) && temperatureValid && batchWeightValid &&
                     finalWeightValid && (!finalWeight.HasValue || finalWeight.Value <= batchWeight) &&
                     roastTimeValid && firstCrackValid && firstCrackWithinRoast;
        FocusField = valid
            ? string.Empty
            : FirstInvalidField();
        return valid;
    }

    private string FirstInvalidField() =>
        !string.IsNullOrWhiteSpace(BeanError) ? nameof(SelectedBean) :
        !string.IsNullOrWhiteSpace(TemperatureError) ? nameof(TemperatureText) :
        !string.IsNullOrWhiteSpace(BatchWeightError) ? nameof(BatchWeightText) :
        !string.IsNullOrWhiteSpace(FinalWeightError) ? nameof(FinalWeightText) :
        !string.IsNullOrWhiteSpace(RoastTimeError) ? nameof(RoastTimeText) :
        nameof(FirstCrackTimeText);

    private void ClearValidationErrors()
    {
        BeanError = string.Empty;
        TemperatureError = string.Empty;
        BatchWeightError = string.Empty;
        FinalWeightError = string.Empty;
        RoastTimeError = string.Empty;
        FirstCrackError = string.Empty;
        FocusField = string.Empty;
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
