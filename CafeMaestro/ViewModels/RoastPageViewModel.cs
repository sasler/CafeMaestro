using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;

namespace CafeMaestro.ViewModels;

[QueryProperty(nameof(EditRoastId), "EditRoastId")]
[QueryProperty(nameof(NewRoast), "NewRoast")]
public partial class RoastPageViewModel : ObservableObject, IQueryAttributable
{
    private readonly ITimerService _timerService;
    private readonly IRoastDataService _roastDataService;
    private readonly IBeanDataService _beanService;
    private readonly IAppDataService _appDataService;
    private readonly IPreferencesService _preferencesService;
    private readonly IRoastLevelService _roastLevelService;
    private readonly INavigationService _navigationService;
    private readonly IAlertService _alertService;

    private RoastData? _roastToEdit;
    private RoastData? _previousRoast;
    private Guid _editRoastGuid;
    private bool _forceNewRoast;
    private bool _editLoadPending;
    private bool _dataFilePathInitialized;
    private bool _suppressBeanSelectionChanged;

    [ObservableProperty]
    public partial string PageTitle { get; set; } = "Roast Coffee";

    [ObservableProperty]
    public partial ObservableCollection<BeanData> AvailableBeans { get; set; } = new();

    [ObservableProperty]
    public partial BeanData? SelectedBean { get; set; }

    [ObservableProperty]
    public partial string BeanPickerTitle { get; set; } = "Loading beans...";

    [ObservableProperty]
    public partial string TimerDisplay { get; set; } = "00:00";

    [ObservableProperty]
    public partial bool CanStartTimer { get; set; } = true;

    [ObservableProperty]
    public partial bool CanPauseTimer { get; set; }

    [ObservableProperty]
    public partial bool CanStopTimer { get; set; }

    [ObservableProperty]
    public partial bool IsTimerRunning { get; set; }

    [ObservableProperty]
    public partial bool IsTimeEntryEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool CanMarkFirstCrack { get; set; }

    [ObservableProperty]
    public partial string FirstCrackLabel { get; set; } = "First Crack: Not marked";

    [ObservableProperty]
    public partial int? FirstCrackMinutes { get; set; }

    [ObservableProperty]
    public partial int? FirstCrackSeconds { get; set; }

    [ObservableProperty]
    public partial string BatchWeightText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FinalWeightText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TemperatureText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LossPercentLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasPreviousRoast { get; set; }

    [ObservableProperty]
    public partial string PreviousRoastSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PreviousRoastDetails { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BatchWeightWarningText { get; set; } = "Insufficient beans available!";

    [ObservableProperty]
    public partial bool IsBatchWeightWarningVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEditMode { get; set; }

    [ObservableProperty]
    public partial string EditRoastId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewRoast { get; set; } = string.Empty;

    public RoastPageViewModel(
        ITimerService timerService,
        IRoastDataService roastDataService,
        IBeanDataService beanService,
        IAppDataService appDataService,
        IPreferencesService preferencesService,
        IRoastLevelService roastLevelService,
        INavigationService navigationService,
        IAlertService alertService)
    {
        _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
        _roastDataService = roastDataService ?? throw new ArgumentNullException(nameof(roastDataService));
        _beanService = beanService ?? throw new ArgumentNullException(nameof(beanService));
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _roastLevelService = roastLevelService ?? throw new ArgumentNullException(nameof(roastLevelService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));

        _timerService.TimeUpdated += OnTimeUpdated;
    }

    partial void OnSelectedBeanChanged(BeanData? value)
    {
        if (_suppressBeanSelectionChanged)
        {
            return;
        }

        _ = HandleSelectedBeanChangedAsync(value);
    }

    partial void OnBatchWeightTextChanged(string value)
    {
        ValidateBatchWeight();
        _ = UpdateLossPercentAsync();
    }

    partial void OnFinalWeightTextChanged(string value)
    {
        _ = UpdateLossPercentAsync();
    }

    partial void OnEditRoastIdChanged(string value)
    {
        if (!Guid.TryParse(value, out Guid parsedId))
        {
            return;
        }

        _editRoastGuid = parsedId;
        _editLoadPending = true;
        _forceNewRoast = false;
        IsEditMode = true;
        PageTitle = "Edit Roast";
    }

    partial void OnNewRoastChanged(string value)
    {
        if (!bool.TryParse(value, out bool isNew) || !isNew)
        {
            return;
        }

        _forceNewRoast = true;
        _editLoadPending = false;
        _editRoastGuid = Guid.Empty;
        EditRoastId = string.Empty;
        ResetPageForNewRoast();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("NewRoast", out object? newRoast))
        {
            NewRoast = newRoast?.ToString() ?? string.Empty;
        }

        if (query.TryGetValue("EditRoastId", out object? editId))
        {
            EditRoastId = editId?.ToString() ?? string.Empty;
        }
    }

    public async Task OnAppearingAsync()
    {
        if (!_dataFilePathInitialized)
        {
            await InitializeDataFilePathAsync();
            _dataFilePathInitialized = true;
        }

        if (_forceNewRoast || !IsEditMode)
        {
            ResetPageForNewRoast();
        }

        await LoadAvailableBeansAsync();

        if (_editLoadPending || (IsEditMode && _roastToEdit is null && _editRoastGuid != Guid.Empty))
        {
            await LoadRoastDataForEditAsync();
        }

        ValidateBatchWeight();
        _forceNewRoast = false;
    }

    public void OnDisappearing()
    {
        _timerService.TimeUpdated -= OnTimeUpdated;
        _timerService.Stop();
        ApplyStoppedTimerState(canStop: false);
        _roastToEdit = null;
        _editRoastGuid = Guid.Empty;
        _editLoadPending = false;
        IsEditMode = false;
    }

    public void SetManualTimerDisplay(string value)
    {
        if (TryParseTimeText(value, out int minutes, out int seconds))
        {
            TimerDisplay = FormatTime(minutes, seconds);
            return;
        }

        TimerDisplay = "00:00";
    }

    public Task HandleBackNavigationAsync()
    {
        return CancelAsync();
    }

    [RelayCommand]
    private Task StartTimerAsync()
    {
        if (!CanStartTimer)
        {
            return Task.CompletedTask;
        }

        _timerService.Start();
        IsTimerRunning = true;
        CanStartTimer = false;
        CanPauseTimer = true;
        CanStopTimer = true;
        IsTimeEntryEnabled = false;
        UpdateFirstCrackButtonState();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task PauseTimerAsync()
    {
        _timerService.Stop();
        ApplyStoppedTimerState(canStop: true);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task StopTimerAsync()
    {
        _timerService.Stop();
        ApplyStoppedTimerState(canStop: false);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ResetTimerAsync()
    {
        _timerService.Reset();
        TimerDisplay = "00:00";
        ApplyStoppedTimerState(canStop: false);
        ResetFirstCrackTracking();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task MarkFirstCrackAsync()
    {
        try
        {
            TimeSpan currentTime = _timerService.GetElapsedTime();
            FirstCrackMinutes = currentTime.Minutes;
            FirstCrackSeconds = currentTime.Seconds;
            FirstCrackLabel = $"First Crack: {FirstCrackMinutes:D2}:{FirstCrackSeconds:D2}";
            UpdateFirstCrackButtonState();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error marking first crack: {ex.Message}");
            ResetFirstCrackTracking();
            await _alertService.ShowAlertAsync("Error", "Failed to mark First Crack. Please try again.", "OK");
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        RoastFormInput? input = await ValidateInputsAsync();
        if (input is null)
        {
            return;
        }

        try
        {
            if (IsEditMode && _roastToEdit is not null)
            {
                RoastData updatedRoast = new()
                {
                    Id = _roastToEdit.Id,
                    RoastDate = _roastToEdit.RoastDate,
                    BeanType = SelectedBean?.DisplayName ?? _roastToEdit.BeanType,
                    Temperature = input.Temperature,
                    BatchWeight = input.BatchWeight,
                    FinalWeight = input.FinalWeight,
                    RoastMinutes = input.RoastMinutes,
                    RoastSeconds = input.RoastSeconds,
                    Notes = Notes,
                    FirstCrackMinutes = FirstCrackMinutes,
                    FirstCrackSeconds = FirstCrackSeconds
                };

                bool updated = await _roastDataService.UpdateRoastLogAsync(updatedRoast);
                if (updated)
                {
                    await _alertService.ShowAlertAsync("Success", "Roast data updated successfully!", "OK");
                    await _navigationService.GoToAsync(Routes.RoastLog);
                    return;
                }

                await _alertService.ShowAlertAsync("Error", "Failed to update roast data. Please try again.", "OK");
                return;
            }

            RoastData roastData = new()
            {
                Id = Guid.NewGuid(),
                BeanType = SelectedBean?.DisplayName ?? "Unknown",
                Temperature = input.Temperature,
                BatchWeight = input.BatchWeight,
                FinalWeight = input.FinalWeight,
                RoastMinutes = input.RoastMinutes,
                RoastSeconds = input.RoastSeconds,
                RoastDate = DateTime.Now,
                Notes = Notes,
                FirstCrackMinutes = FirstCrackMinutes,
                FirstCrackSeconds = FirstCrackSeconds
            };

            if (SelectedBean is not null)
            {
                double batchWeightKg = input.BatchWeight / 1000.0;
                await _beanService.UpdateBeanQuantityAsync(SelectedBean.Id, batchWeightKg);
            }

            bool saved = await _roastDataService.SaveRoastDataAsync(roastData);
            if (saved)
            {
                await _alertService.ShowAlertAsync("Success", "Roast data saved successfully!", "OK");
                await LoadAvailableBeansAsync();
                ClearForm();
                return;
            }

            await _alertService.ShowAlertAsync("Error", "Failed to save roast data. Please try again.", "OK");
        }
        catch (Exception ex)
        {
            await _alertService.ShowAlertAsync("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private Task CancelAsync()
    {
        return _navigationService.GoToAsync(IsEditMode ? Routes.RoastLog : Routes.Main);
    }

    private void OnTimeUpdated(TimeSpan elapsedTime)
    {
        TimerDisplay = FormatTime((int)elapsedTime.TotalMinutes, elapsedTime.Seconds);
    }

    private async Task InitializeDataFilePathAsync()
    {
        try
        {
            bool isFirstRun = await _preferencesService.IsFirstRunAsync();
            string? savedFilePath = await _preferencesService.GetAppDataFilePathAsync();

            if (!string.IsNullOrEmpty(savedFilePath))
            {
                if (File.Exists(savedFilePath))
                {
                    await _appDataService.SetCustomFilePathAsync(savedFilePath);
                    await _roastDataService.InitializeFromPreferencesAsync(_preferencesService);
                    await _beanService.InitializeFromPreferencesAsync(_preferencesService);
                }
                else
                {
                    await _alertService.ShowAlertAsync(
                        "Data File Not Found",
                        $"The previously used data file could not be found: {savedFilePath}\n\nUsing default location instead.",
                        "OK");

                    await _appDataService.ResetToDefaultPathAsync();
                    await _preferencesService.ClearAppDataFilePathAsync();
                }
            }
            else if (isFirstRun)
            {
                await _navigationService.GoToAsync(Routes.Settings);
                await _alertService.ShowAlertAsync(
                    "Welcome to CafeMaestro",
                    "Please select or create a data file location to store your coffee roasting data.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await _alertService.ShowAlertAsync("Error", $"Failed to initialize data file: {ex.Message}", "OK");
        }
    }

    private async Task LoadAvailableBeansAsync()
    {
        try
        {
            List<BeanData> beans = await _beanService.GetSortedAvailableBeansAsync();
            AvailableBeans = new ObservableCollection<BeanData>(beans);

            if (beans.Count > 0)
            {
                BeanPickerTitle = "Select Bean Type";

                if (!IsEditMode)
                {
                    SelectedBean = beans[0];
                }
            }
            else
            {
                BeanPickerTitle = "No beans available";
                SelectedBean = null;
                ClearPreviousRoastDisplay();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading beans: {ex.Message}");
            AvailableBeans = new ObservableCollection<BeanData>();
            BeanPickerTitle = "Error loading beans";
            SelectedBean = null;
            ClearPreviousRoastDisplay();
            await _alertService.ShowAlertAsync("Error", "Failed to load beans. Please try again.", "OK");
        }
    }

    private async Task HandleSelectedBeanChangedAsync(BeanData? bean)
    {
        if (bean is null)
        {
            ClearPreviousRoastDisplay();
            ValidateBatchWeight();
            return;
        }

        await LoadPreviousRoastDataAsync(bean.DisplayName);
        ValidateBatchWeight();
    }

    private async Task LoadPreviousRoastDataAsync(string beanType)
    {
        try
        {
            _previousRoast = await _roastDataService.GetLastRoastForBeanTypeAsync(beanType);
            UpdatePreviousRoastDisplay();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading previous roast: {ex.Message}");
            _previousRoast = null;
            UpdatePreviousRoastDisplay();
        }
    }

    private void UpdatePreviousRoastDisplay()
    {
        HasPreviousRoast = _previousRoast is not null;

        if (_previousRoast is null)
        {
            PreviousRoastSummary = string.Empty;
            PreviousRoastDetails = string.Empty;
            return;
        }

        string dateText = _previousRoast.RoastDate.ToString("MM/dd/yyyy");
        PreviousRoastSummary = $"{dateText}: {_previousRoast.RoastLevelName} roast";

        StringBuilder details = new();
        details.Append($"Batch: {_previousRoast.BatchWeight:F1}g | ");
        details.Append($"Temp: {_previousRoast.Temperature}°C | ");
        details.Append($"Time: {_previousRoast.FormattedTime} | ");
        details.Append($"Loss: {_previousRoast.WeightLossPercentage:F1}%");

        if (_previousRoast.FirstCrackSeconds.HasValue)
        {
            details.Append($" | First Crack: {_previousRoast.FirstCrackTime}");
        }

        PreviousRoastDetails = details.ToString();
        PrefillFieldsFromPreviousRoast();
    }

    private void PrefillFieldsFromPreviousRoast()
    {
        if (IsEditMode || _previousRoast is null)
        {
            return;
        }

        TemperatureText = _previousRoast.Temperature.ToString("F0");
        BatchWeightText = _previousRoast.BatchWeight.ToString("F1");
    }

    private void ClearPreviousRoastDisplay()
    {
        _previousRoast = null;
        HasPreviousRoast = false;
        PreviousRoastSummary = string.Empty;
        PreviousRoastDetails = string.Empty;
    }

    private void ValidateBatchWeight()
    {
        if (SelectedBean is null || string.IsNullOrWhiteSpace(BatchWeightText))
        {
            IsBatchWeightWarningVisible = false;
            CanStartTimer = !IsTimerRunning;
            return;
        }

        if (double.TryParse(BatchWeightText, out double batchWeight) && batchWeight > 0)
        {
            double availableBeans = SelectedBean.RemainingQuantity * 1000.0;

            if (batchWeight > availableBeans)
            {
                BatchWeightWarningText = $"Insufficient beans available! (only {availableBeans:F1} g remaining)";
                IsBatchWeightWarningVisible = true;
                CanStartTimer = false;
                return;
            }
        }

        IsBatchWeightWarningVisible = false;
        CanStartTimer = !IsTimerRunning;
    }

    private async Task UpdateLossPercentAsync()
    {
        if (!double.TryParse(BatchWeightText, out double batchWeight) ||
            !double.TryParse(FinalWeightText, out double finalWeight) ||
            batchWeight <= 0 ||
            finalWeight <= 0 ||
            finalWeight > batchWeight)
        {
            LossPercentLabel = string.Empty;
            return;
        }

        double lossWeight = batchWeight - finalWeight;
        double lossPercentage = (lossWeight / batchWeight) * 100;

        try
        {
            string roastLevel = await _roastLevelService.GetRoastLevelNameAsync(lossPercentage);
            LossPercentLabel = $"Weight loss {lossPercentage:F1}% ({roastLevel} roast)";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting roast level: {ex.Message}");
            LossPercentLabel = $"Weight loss {lossPercentage:F1}% (Unknown roast)";
        }
    }

    private async Task<RoastFormInput?> ValidateInputsAsync()
    {
        if (SelectedBean is null)
        {
            await _alertService.ShowAlertAsync("Validation Error", "Please select a bean type or add beans to your inventory.", "OK");
            return null;
        }

        if (!double.TryParse(BatchWeightText, out double batchWeight) || batchWeight <= 0)
        {
            await _alertService.ShowAlertAsync("Validation Error", "Please enter a valid batch weight in grams.", "OK");
            return null;
        }

        double batchWeightKg = batchWeight / 1000.0;
        if (batchWeightKg > SelectedBean.RemainingQuantity)
        {
            await _alertService.ShowAlertAsync(
                "Validation Error",
                $"Not enough beans available. You have {SelectedBean.RemainingQuantity:F2}kg remaining, but need {batchWeightKg:F2}kg.",
                "OK");
            return null;
        }

        if (!double.TryParse(FinalWeightText, out double finalWeight) || finalWeight <= 0)
        {
            await _alertService.ShowAlertAsync("Validation Error", "Please enter a valid final weight in grams.", "OK");
            return null;
        }

        if (finalWeight > batchWeight)
        {
            await _alertService.ShowAlertAsync("Validation Error", "Final weight cannot be greater than batch weight.", "OK");
            return null;
        }

        if (!double.TryParse(TemperatureText, out double temperature) || temperature <= 0)
        {
            await _alertService.ShowAlertAsync("Validation Error", "Please enter a valid temperature in Celsius.", "OK");
            return null;
        }

        if (!TryParseTimeText(TimerDisplay, out int roastMinutes, out int roastSeconds))
        {
            await _alertService.ShowAlertAsync("Validation Error", "Please enter a valid roast time.", "OK");
            return null;
        }

        if (roastMinutes == 0 && roastSeconds == 0)
        {
            await _alertService.ShowAlertAsync("Validation Error", "Roasting time must be greater than 0.", "OK");
            return null;
        }

        RoastData roastData = new()
        {
            BeanType = SelectedBean.DisplayName,
            BatchWeight = batchWeight,
            FinalWeight = finalWeight,
            Temperature = temperature,
            RoastMinutes = roastMinutes,
            RoastSeconds = roastSeconds,
            Notes = Notes,
            FirstCrackMinutes = FirstCrackMinutes,
            FirstCrackSeconds = FirstCrackSeconds
        };

        List<string> errors = roastData.Validate();
        if (errors.Count > 0)
        {
            await _alertService.ShowAlertAsync("Validation Error", errors[0], "OK");
            return null;
        }

        return new RoastFormInput(batchWeight, finalWeight, temperature, roastMinutes, roastSeconds);
    }

    private async Task LoadRoastDataForEditAsync()
    {
        try
        {
            if (_editRoastGuid == Guid.Empty)
            {
                return;
            }

            _roastToEdit = await _roastDataService.GetRoastLogByIdAsync(_editRoastGuid);
            if (_roastToEdit is null)
            {
                await _alertService.ShowAlertAsync("Error", "Failed to find the roast data for editing", "OK");
                return;
            }

            PageTitle = "Edit Roast";
            IsEditMode = true;
            _editLoadPending = false;
            ClearPreviousRoastDisplay();

            TimerDisplay = FormatTime(_roastToEdit.RoastMinutes, _roastToEdit.RoastSeconds);
            BatchWeightText = _roastToEdit.BatchWeight.ToString("F1");
            FinalWeightText = _roastToEdit.FinalWeight.ToString("F1");
            TemperatureText = _roastToEdit.Temperature.ToString("F0");
            Notes = _roastToEdit.Notes;

            if (_roastToEdit.FirstCrackSeconds.HasValue)
            {
                FirstCrackMinutes = _roastToEdit.FirstCrackMinutes;
                FirstCrackSeconds = _roastToEdit.FirstCrackSeconds;
                FirstCrackLabel = $"First Crack: {FirstCrackMinutes:D2}:{FirstCrackSeconds:D2}";
            }
            else
            {
                ResetFirstCrackTracking();
            }

            await UpdateLossPercentAsync();
            await SelectBeanAsync(_roastToEdit.BeanType);
            ValidateBatchWeight();
            UpdateFirstCrackButtonState();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading roast data for editing: {ex.Message}");
            await _alertService.ShowAlertAsync("Error", "Failed to load roast data for editing", "OK");
        }
    }

    private async Task SelectBeanAsync(string beanType)
    {
        if (string.IsNullOrWhiteSpace(beanType) || AvailableBeans.Count == 0)
        {
            return;
        }

        BeanData? bean =
            AvailableBeans.FirstOrDefault(item => string.Equals(item.DisplayName, beanType, StringComparison.Ordinal))
            ?? AvailableBeans.FirstOrDefault(item => string.Equals(item.DisplayName, beanType, StringComparison.OrdinalIgnoreCase))
            ?? AvailableBeans.FirstOrDefault(item =>
                item.DisplayName.Contains(beanType, StringComparison.OrdinalIgnoreCase) ||
                beanType.Contains(item.DisplayName, StringComparison.OrdinalIgnoreCase));

        if (bean is null)
        {
            await Task.CompletedTask;
            return;
        }

        _suppressBeanSelectionChanged = true;
        SelectedBean = bean;
        _suppressBeanSelectionChanged = false;
    }

    private void ResetPageForNewRoast()
    {
        IsEditMode = false;
        _roastToEdit = null;
        _editRoastGuid = Guid.Empty;
        PageTitle = "Roast Coffee";
        ClearForm();
        ClearPreviousRoastDisplay();
    }

    private void ClearForm()
    {
        _timerService.Reset();
        TimerDisplay = "00:00";
        BatchWeightText = string.Empty;
        FinalWeightText = string.Empty;
        TemperatureText = string.Empty;
        Notes = string.Empty;
        LossPercentLabel = string.Empty;
        ApplyStoppedTimerState(canStop: false);
        ResetFirstCrackTracking();
    }

    private void ResetFirstCrackTracking()
    {
        FirstCrackMinutes = null;
        FirstCrackSeconds = null;
        FirstCrackLabel = "First Crack: Not marked";
        UpdateFirstCrackButtonState();
    }

    private void UpdateFirstCrackButtonState()
    {
        CanMarkFirstCrack = IsTimerRunning && !FirstCrackSeconds.HasValue;
    }

    private void ApplyStoppedTimerState(bool canStop)
    {
        IsTimerRunning = false;
        CanStartTimer = !IsBatchWeightWarningVisible;
        CanPauseTimer = false;
        CanStopTimer = canStop;
        IsTimeEntryEnabled = true;
        UpdateFirstCrackButtonState();
    }

    private static bool TryParseTimeText(string timeText, out int minutes, out int seconds)
    {
        minutes = 0;
        seconds = 0;

        string[] parts = (timeText ?? string.Empty).Trim().Split(':');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out minutes) ||
            !int.TryParse(parts[1], out seconds))
        {
            return false;
        }

        if (seconds >= 60)
        {
            minutes += seconds / 60;
            seconds %= 60;
        }

        return true;
    }

    private static string FormatTime(int minutes, int seconds)
    {
        if (seconds >= 60)
        {
            minutes += seconds / 60;
            seconds %= 60;
        }

        return $"{minutes:D2}:{seconds:D2}";
    }

    private sealed record RoastFormInput(
        double BatchWeight,
        double FinalWeight,
        double Temperature,
        int RoastMinutes,
        int RoastSeconds);
}
