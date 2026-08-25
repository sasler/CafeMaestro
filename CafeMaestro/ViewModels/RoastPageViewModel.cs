using System.Collections.ObjectModel;
using System.Globalization;
using CafeMaestro.Drawing;
using CafeMaestro.Models;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;

namespace CafeMaestro.ViewModels;

public partial class RoastPageViewModel : ObservableObject
{
    private readonly IRoastSessionService _sessionService;
    private readonly IRoastQueryService _queryService;
    private readonly IBeanDataService _beanService;
    private readonly IOverlayService _overlayService;
    private readonly IDisplayWakeService _displayWakeService;
    private readonly IRoastRecoveryAdapter _recoveryAdapter;
    private readonly IClock _clock;

    private RoastSessionSnapshot? _snapshot;
    private ActiveRoastSnapshot? _activeRoast;
    private double _elapsedAtSnapshot;
    private DateTimeOffset _snapshotAtUtc;
    private bool _subscribed;
    private Func<Task>? _retryAction;

    [ObservableProperty]
    public partial RoastPresentationState PresentationState { get; set; } = RoastPresentationState.Setup;

    [ObservableProperty]
    public partial ObservableCollection<BeanData> AvailableBeans { get; set; } = [];

    [ObservableProperty]
    public partial BeanData? SelectedBean { get; set; }

    [ObservableProperty]
    public partial string TemperatureText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BatchWeightText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TemperatureError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BatchWeightError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InventoryWarning { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PreviousResultTitle { get; set; } = "NO PREVIOUS ROAST";

    [ObservableProperty]
    public partial string PreviousResultTime { get; set; } = "—";

    [ObservableProperty]
    public partial string PreviousResultDetails { get; set; } = "Choose a bean to load its last completed result.";

    [ObservableProperty]
    public partial string NewerAwaitingWeightNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ElapsedDisplay { get; set; } = "00:00";

    [ObservableProperty]
    public partial double ElapsedSweep { get; set; }

    [ObservableProperty]
    public partial bool IsPaused { get; set; }

    [ObservableProperty]
    public partial string ActiveBatchLabel { get; set; } = "BATCH 1";

    [ObservableProperty]
    public partial string ActiveBeanName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActiveSetupSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFirstCrackVisible { get; set; }

    [ObservableProperty]
    public partial bool IsFirstCrackMarked { get; set; }

    [ObservableProperty]
    public partial string FirstCrackDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DevelopmentDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DtrDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<RoastChannelPresentation> Channels { get; set; } = [];

    [ObservableProperty]
    public partial string PrimaryActionText { get; set; } = "SET UP BATCH 2";

    [ObservableProperty]
    public partial string SecondaryActionText { get; set; } = "DONE FOR NOW";

    [ObservableProperty]
    public partial string RecoveryTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecoveryElapsedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RecoveryRequiresCorrectedTime { get; set; }

    [ObservableProperty]
    public partial string RecoveryEndTimeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecoveryCorrectedElapsedText { get; set; } = string.Empty;

    public bool IsSetupVisible => PresentationState == RoastPresentationState.Setup;
    public bool IsActiveVisible => PresentationState == RoastPresentationState.Active;
    public bool IsHandoffVisible => PresentationState == RoastPresentationState.Handoff;
    public bool IsRecoveryVisible => PresentationState == RoastPresentationState.Recovery;
    public bool IsPersistenceErrorVisible => PresentationState == RoastPresentationState.PersistenceError;
    public bool CanStart => !IsBusy && SelectedBean is not null && ValidateSetup(showErrors: false);
    public string PauseResumeText => IsPaused ? "RESUME" : "PAUSE";
    public string ActiveStateLabel => IsPaused ? "PAUSED" : "ROASTING";
    public bool HasInventoryWarning => !string.IsNullOrWhiteSpace(InventoryWarning);
    public bool CanMarkFirstCrack => IsFirstCrackVisible;
    public bool CanKeepRoastingAfterRecovery => !RecoveryRequiresCorrectedTime;

    public RoastPageViewModel(
        IRoastSessionService sessionService,
        IRoastQueryService queryService,
        IBeanDataService beanService,
        IOverlayService overlayService,
        IDisplayWakeService displayWakeService,
        IRoastRecoveryAdapter recoveryAdapter,
        IClock clock)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _beanService = beanService ?? throw new ArgumentNullException(nameof(beanService));
        _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
        _displayWakeService = displayWakeService ?? throw new ArgumentNullException(nameof(displayWakeService));
        _recoveryAdapter = recoveryAdapter ?? throw new ArgumentNullException(nameof(recoveryAdapter));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    partial void OnPresentationStateChanged(RoastPresentationState value)
    {
        OnPropertyChanged(nameof(IsSetupVisible));
        OnPropertyChanged(nameof(IsActiveVisible));
        OnPropertyChanged(nameof(IsHandoffVisible));
        OnPropertyChanged(nameof(IsRecoveryVisible));
        OnPropertyChanged(nameof(IsPersistenceErrorVisible));
    }

    partial void OnSelectedBeanChanged(BeanData? value)
    {
        OnPropertyChanged(nameof(CanStart));
        StartCommand.NotifyCanExecuteChanged();
        _ = SelectBeanAsync(value);
    }

    partial void OnTemperatureTextChanged(string value)
    {
        TemperatureError = string.Empty;
        OnPropertyChanged(nameof(CanStart));
        StartCommand.NotifyCanExecuteChanged();
    }

    partial void OnBatchWeightTextChanged(string value)
    {
        BatchWeightError = string.Empty;
        UpdateInventoryWarning();
        OnPropertyChanged(nameof(CanStart));
        StartCommand.NotifyCanExecuteChanged();
    }

    partial void OnInventoryWarningChanged(string value) => OnPropertyChanged(nameof(HasInventoryWarning));

    partial void OnIsFirstCrackVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(CanMarkFirstCrack));
        MarkFirstCrackCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsFirstCrackMarkedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanMarkFirstCrack));
        MarkFirstCrackCommand.NotifyCanExecuteChanged();
    }

    partial void OnRecoveryRequiresCorrectedTimeChanged(bool value) =>
        OnPropertyChanged(nameof(CanKeepRoastingAfterRecovery));

    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(PauseResumeText));
        OnPropertyChanged(nameof(ActiveStateLabel));
    }

    public async Task OnAppearingAsync()
    {
        Subscribe();
        await LoadBeansAsync();
        await RefreshAsync();
    }

    public async Task OnDisappearingAsync()
    {
        Unsubscribe();
        await _displayWakeService.SetKeepScreenOnAsync(false);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        RoastSessionSnapshot snapshot = await _sessionService.GetSnapshotAsync();
        await ApplySnapshotAsync(snapshot);
    }

    [RelayCommand]
    public async Task SelectBeanAsync(BeanData? bean)
    {
        if (bean is null)
        {
            ClearSuggestion();
            return;
        }

        SelectedBean = bean;
        IsBusy = true;
        try
        {
            RoastSetupSuggestion suggestion = await _queryService.GetSetupSuggestionAsync(bean.Id);
            TemperatureText = suggestion.Temperature?.ToString("0.#", CultureInfo.CurrentCulture) ?? string.Empty;
            BatchWeightText = suggestion.BatchWeight?.ToString("0.#", CultureInfo.CurrentCulture) ?? string.Empty;
            ApplyPreviousResult(suggestion);
            UpdateInventoryWarning();
        }
        catch (Exception)
        {
            TemperatureText = string.Empty;
            BatchWeightText = string.Empty;
            PreviousResultTitle = "PREVIOUS ROAST UNAVAILABLE";
            PreviousResultTime = "—";
            PreviousResultDetails = "Enter temperature and batch weight manually, or retry history.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanStart));
            StartCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    public async Task StartAsync()
    {
        if (SelectedBean is null || !ValidateSetup(showErrors: true))
        {
            return;
        }

        IsBusy = true;
        OnPropertyChanged(nameof(CanStart));
        StartCommand.NotifyCanExecuteChanged();
        try
        {
            TransitionResult result = await _sessionService.StartAsync(new RoastSetup(
                SelectedBean.Id,
                ParseNumber(TemperatureText),
                ParseNumber(BatchWeightText)));
            await HandleTransitionAsync(result, StartAsync);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanStart));
            StartCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    public async Task PauseOrResumeAsync()
    {
        TransitionResult result = IsPaused
            ? await _sessionService.ResumeAsync()
            : await _sessionService.PauseAsync();
        await HandleTransitionAsync(result, PauseOrResumeAsync);
    }

    [RelayCommand(CanExecute = nameof(CanMarkFirstCrack))]
    public async Task MarkFirstCrackAsync()
    {
        if (!IsFirstCrackVisible)
        {
            return;
        }

        if (IsFirstCrackMarked && _activeRoast?.FirstCrackElapsedSeconds is int current)
        {
            TimeCorrectionOutcome correction = await _overlayService.ShowTimeCorrectionAsync(
                new TimeCorrectionRequest
                {
                    Title = "CORRECT FIRST CRACK",
                    Description = "Change only the First Crack event time; total roast time stays unchanged.",
                    CurrentSeconds = current,
                    MaximumSeconds = Math.Max(current, (int)Math.Floor(CurrentElapsedSeconds()))
                });
            if (correction.Seconds is int corrected)
            {
                await HandleTransitionAsync(
                    await _sessionService.CorrectFirstCrackAsync(corrected),
                    MarkFirstCrackAsync);
            }
            return;
        }

        TransitionResult result = await _sessionService.MarkFirstCrackAsync();
        await HandleTransitionAsync(result, MarkFirstCrackAsync);
    }

    [RelayCommand]
    public async Task ResetAsync()
    {
        if (!IsPaused || _activeRoast is null ||
            !await _overlayService.ConfirmResetAsync(_activeRoast.FirstCrackElapsedSeconds.HasValue))
        {
            return;
        }

        await HandleTransitionAsync(await _sessionService.ResetAsync(), ResetAsync);
    }

    [RelayCommand]
    public async Task DropAsync()
    {
        TransitionResult result = await _sessionService.DropAsync();
        await HandleTransitionAsync(result, DropAsync);
    }

    [RelayCommand]
    public async Task PrimaryHandoffActionAsync()
    {
        if (_snapshot is null)
        {
            return;
        }

        RoastWorkItem? readyOldest = _snapshot.OpenWork
            .Where(item => item.IsReadyToWeigh)
            .OrderBy(item => item.BatchNumber ?? int.MaxValue)
            .FirstOrDefault();
        if (_snapshot.NextBatchNumber >= 3 && readyOldest is not null)
        {
            await ShowWeighInAsync(readyOldest);
            return;
        }

        if (_snapshot.NextBatchNumber == 2)
        {
            await SetUpNextBatchAsync();
            return;
        }

        await FinishSessionAsync();
    }

    [RelayCommand]
    public async Task SecondaryHandoffActionAsync() => await FinishSessionAsync();

    [RelayCommand]
    public async Task AdjustDropTimeAsync()
    {
        RoastWorkItem? newest = _snapshot?.OpenWork
            .OrderByDescending(item => item.BatchNumber ?? 0)
            .FirstOrDefault();
        if (newest is null)
        {
            return;
        }

        TimeCorrectionOutcome correction = await _overlayService.ShowTimeCorrectionAsync(
            new TimeCorrectionRequest
            {
                Title = "ADJUST DROP TIME",
                Description = "Correct the recorded total roast time. Cooling will move with the drop.",
                CurrentSeconds = newest.TotalSeconds,
                MaximumSeconds = newest.TotalSeconds + Math.Max(0, (int)Math.Floor((_clock.UtcNow - newest.DroppedAtUtc).TotalSeconds))
            });
        if (correction.Seconds is not int correctedSeconds)
        {
            return;
        }

        DateTimeOffset correctedDrop = newest.DroppedAtUtc.AddSeconds(correctedSeconds - newest.TotalSeconds);
        await HandleTransitionAsync(
            await _sessionService.CorrectDropAsync(newest.RoastId, correctedDrop),
            AdjustDropTimeAsync);
    }

    [RelayCommand]
    public async Task WeighChannelAsync(RoastChannelPresentation? channel)
    {
        if (channel is null || _snapshot is null)
        {
            return;
        }

        RoastWorkItem? item = _snapshot.OpenWork.FirstOrDefault(work => work.RoastId == channel.RoastId);
        if (item?.IsReadyToWeigh == true)
        {
            await ShowWeighInAsync(item);
        }
    }

    [RelayCommand]
    public async Task RetryAsync()
    {
        if (_retryAction is not null)
        {
            await _retryAction();
        }
    }

    [RelayCommand]
    public async Task KeepRoastingAfterRecoveryAsync()
    {
        if (_activeRoast is null || !TryGetRecoveryElapsed(out double? correctedElapsed))
        {
            ErrorMessage = "Enter the actual elapsed roast time in mm:ss.";
            return;
        }

        await HandleTransitionAsync(
            await _recoveryAdapter.KeepRoastingAsync(_activeRoast, correctedElapsed),
            KeepRoastingAfterRecoveryAsync);
    }

    [RelayCommand]
    public async Task RecordRecoveryEndAsync()
    {
        if (_activeRoast is null ||
            !TryParseLocalEnd(RecoveryEndTimeText, out DateTimeOffset endedAtUtc) ||
            !TryGetRecoveryElapsed(out double? correctedElapsed))
        {
            ErrorMessage = "Enter a valid end time and corrected elapsed roast time.";
            return;
        }

        await HandleTransitionAsync(
            await _recoveryAdapter.EndedAtAsync(_activeRoast, endedAtUtc, correctedElapsed),
            RecordRecoveryEndAsync);
    }

    public void Tick()
    {
        if (_activeRoast is null || PresentationState != RoastPresentationState.Active)
        {
            UpdateChannels();
            return;
        }

        double elapsed = _elapsedAtSnapshot;
        if (_activeRoast.IsRunning)
        {
            elapsed += Math.Max(0, (_clock.UtcNow - _snapshotAtUtc).TotalSeconds);
        }

        ApplyElapsed(elapsed);
        UpdateChannels();
    }

    public async Task<bool> HandleBackNavigationAsync()
    {
        if (PresentationState is not RoastPresentationState.Active and not RoastPresentationState.Recovery)
        {
            return false;
        }

        if (PresentationState == RoastPresentationState.Recovery)
        {
            return true;
        }

        NavigationChoice choice = await _overlayService.ConfirmNavigationAsync();
        if (choice == NavigationChoice.KeepRoasting)
        {
            return true;
        }

        await DiscardAsync();
        return true;
    }

    [RelayCommand]
    private async Task DiscardAsync()
    {
        if (_activeRoast is null)
        {
            return;
        }

        DiscardOutcome outcome = await _overlayService.ShowDiscardAsync(new DiscardRequest
        {
            BeanDisplaySnapshot = _activeRoast.BeanDisplaySnapshot,
            BatchNumber = _activeRoast.BatchNumber,
            ElapsedDisplay = ElapsedDisplay
        });
        if (!outcome.ShouldDiscard)
        {
            return;
        }

        await HandleTransitionAsync(
            await _sessionService.DiscardAsync(outcome.BeansWereUsed, outcome.KeepLog),
            DiscardAsync);
    }

    private async Task LoadBeansAsync()
    {
        IReadOnlyList<BeanData> beans = await _beanService.GetSortedAvailableBeansAsync();
        AvailableBeans = new ObservableCollection<BeanData>(beans);
    }

    private async Task ApplySnapshotAsync(RoastSessionSnapshot snapshot)
    {
        _snapshot = snapshot;
        _activeRoast = snapshot.ActiveRoast;
        _snapshotAtUtc = snapshot.AsOfUtc;
        _elapsedAtSnapshot = snapshot.ActiveRoast?.ElapsedSeconds ?? 0;
        _retryAction = null;
        ErrorMessage = string.Empty;

        if (snapshot.RequiresRecovery && snapshot.ActiveRoast is not null)
        {
            ApplyRecovery(snapshot.ActiveRoast);
            await _displayWakeService.SetKeepScreenOnAsync(false);
            return;
        }

        if (snapshot.ActiveRoast is not null)
        {
            ApplyActive(snapshot.ActiveRoast);
            await _displayWakeService.SetKeepScreenOnAsync(snapshot.ActiveRoast.IsRunning);
            return;
        }

        await _displayWakeService.SetKeepScreenOnAsync(false);
        if (snapshot.HasSession && snapshot.OpenWork.Count > 0)
        {
            ApplyHandoff(snapshot);
        }
        else
        {
            PresentationState = RoastPresentationState.Setup;
        }
    }

    private void ApplyActive(ActiveRoastSnapshot active)
    {
        PresentationState = RoastPresentationState.Active;
        ActiveBatchLabel = $"BATCH {active.BatchNumber}";
        ActiveBeanName = active.BeanDisplaySnapshot;
        ActiveSetupSummary = $"{active.Temperature:0.#} °C · {active.BatchWeight:0.#} g";
        IsPaused = !active.IsRunning;
        IsFirstCrackVisible = active.FirstCrackEnabled;
        IsFirstCrackMarked = active.FirstCrackElapsedSeconds.HasValue;
        FirstCrackDisplay = active.FirstCrackElapsedSeconds is int firstCrack
            ? FormatElapsed(firstCrack)
            : "MARK 1C";
        DevelopmentDisplay = active.DevelopmentSeconds is int development
            ? FormatElapsed(development)
            : string.Empty;
        DtrDisplay = active.DevelopmentTimeRatio is double ratio ? $"{ratio:0.0}%" : string.Empty;
        ApplyElapsed(active.ElapsedSeconds);
        UpdateChannels();
    }

    private void ApplyHandoff(RoastSessionSnapshot snapshot)
    {
        PresentationState = RoastPresentationState.Handoff;
        UpdateChannels();
        RoastWorkItem? readyOldest = snapshot.OpenWork
            .Where(item => item.IsReadyToWeigh)
            .OrderBy(item => item.BatchNumber ?? int.MaxValue)
            .FirstOrDefault();
        if (snapshot.NextBatchNumber == 2)
        {
            PrimaryActionText = "SET UP BATCH 2";
            SecondaryActionText = "DONE FOR NOW";
        }
        else if (readyOldest is not null)
        {
            PrimaryActionText = $"WEIGH BATCH {readyOldest.BatchNumber}";
            SecondaryActionText = "FINISH SESSION";
        }
        else
        {
            PrimaryActionText = "FINISH SESSION";
            SecondaryActionText = "SET UP ANOTHER BATCH";
        }
    }

    private void ApplyRecovery(ActiveRoastSnapshot active)
    {
        PresentationState = RoastPresentationState.Recovery;
        RecoveryTitle = $"Batch {active.BatchNumber} is still open";
        RecoveryElapsedText = active.IsElapsedImplausible
            ? "The device clock changed. Enter the actual end time to continue safely."
            : $"{active.BeanDisplaySnapshot} has been roasting {FormatElapsed(active.ElapsedSeconds)} — still going?";
        RecoveryRequiresCorrectedTime = active.RequiresCorrectedElapsed;
        RecoveryCorrectedElapsedText = active.RequiresCorrectedElapsed
            ? FormatElapsed(active.ElapsedSeconds)
            : string.Empty;
    }

    private void UpdateChannels()
    {
        if (_snapshot is null)
        {
            Channels.Clear();
            return;
        }

        var channels = new ObservableCollection<RoastChannelPresentation>();
        foreach (RoastWorkItem item in _snapshot.OpenWork.OrderBy(work => work.BatchNumber ?? int.MaxValue))
        {
            double remaining = Math.Max(0, (item.ReadyToWeighAtUtc - _clock.UtcNow).TotalSeconds);
            bool ready = item.Status == RoastEffectiveStatus.NeedsWeight || remaining <= 0;
            channels.Add(new RoastChannelPresentation
            {
                RoastId = item.RoastId,
                BatchLabel = item.BatchNumber is int batch ? $"B{batch}" : "BATCH",
                BeanDisplaySnapshot = item.BeanDisplaySnapshot,
                StatusLabel = ready ? "READY TO WEIGH" : "COOLING",
                TimeDisplay = ready ? "WEIGH" : FormatElapsed(remaining),
                CoolingProgress = RoastInstrumentGeometry.CoolingProgress(
                    remaining,
                    Math.Max(0, (item.ReadyToWeighAtUtc - item.DroppedAtUtc).TotalSeconds)),
                IsReady = ready
            });
        }

        Channels = channels;
    }

    private async Task SetUpNextBatchAsync()
    {
        if (_snapshot is null)
        {
            return;
        }

        RoastWorkItem? source = _snapshot.OpenWork
            .OrderByDescending(item => item.BatchNumber ?? 0)
            .FirstOrDefault();
        if (source?.BeanId is not Guid beanId)
        {
            PresentationState = RoastPresentationState.Setup;
            return;
        }

        BeanData? bean = AvailableBeans.FirstOrDefault(candidate => candidate.Id == beanId);
        if (bean is null)
        {
            PresentationState = RoastPresentationState.Setup;
            return;
        }

        IReadOnlyList<RoastData> roasts = await _queryService.GetRoastsForBeanAsync(beanId);
        RoastData? dropped = roasts.FirstOrDefault(roast => roast.Id == source.RoastId);
        RoastSetupSuggestion suggestion = await _queryService.GetSetupSuggestionAsync(beanId);
        SelectedBean = bean;
        TemperatureText = (dropped?.Temperature ?? suggestion.Temperature)
            ?.ToString("0.#", CultureInfo.CurrentCulture) ?? string.Empty;
        BatchWeightText = source.BatchWeight.ToString("0.#", CultureInfo.CurrentCulture);
        ApplyPreviousResult(suggestion);
        PresentationState = RoastPresentationState.Setup;
    }

    private async Task ShowWeighInAsync(RoastWorkItem item)
    {
        IReadOnlyList<RoastWorkItem> ready = _snapshot?.OpenWork.Where(work =>
            work.IsReadyToWeigh || work.ReadyToWeighAtUtc <= _clock.UtcNow).ToList() ?? [];
        RoastWorkItem selected = item;
        if (ready.Count > 1)
        {
            IReadOnlyList<BatchChoice> choices = ready.Select(ToBatchChoice).ToList();
            BatchChoiceOutcome choice = await _overlayService.ChooseBatchAsync(choices);
            if (choice.Choice is null)
            {
                return;
            }

            selected = ready.Single(work => work.RoastId == choice.Choice.RoastId);
        }

        WeighInOutcome outcome = await _overlayService.ShowWeighInAsync(new WeighInRequest
        {
            RoastId = selected.RoastId,
            BatchNumber = selected.BatchNumber,
            BeanDisplaySnapshot = selected.BeanDisplaySnapshot,
            BatchWeight = selected.BatchWeight,
            DroppedAtUtc = selected.DroppedAtUtc,
            TotalSeconds = selected.TotalSeconds,
            HasAnotherBatchWaiting = ready.Count > 1
        });
        if (outcome.Kind != WeighInOutcomeKind.Cancelled)
        {
            await RefreshAsync();
        }
    }

    private async Task FinishSessionAsync()
    {
        if (_snapshot?.HasActiveRoast == true)
        {
            return;
        }

        await HandleTransitionAsync(await _sessionService.FinishSessionAsync(), FinishSessionAsync);
    }

    private async Task HandleTransitionAsync(TransitionResult result, Func<Task> retryAction)
    {
        if (result.Success)
        {
            await ApplySnapshotAsync(result.Snapshot);
            return;
        }

        _snapshot = result.Snapshot;
        _activeRoast = result.Snapshot.ActiveRoast;
        ErrorMessage = result.Message ?? "CafeMaestro could not save that change.";
        _retryAction = retryAction;
        PresentationState = RoastPresentationState.PersistenceError;
        await _displayWakeService.SetKeepScreenOnAsync(false);
    }

    private void ApplyPreviousResult(RoastSetupSuggestion suggestion)
    {
        RoastData? previous = suggestion.LastCompletedRoast;
        if (previous is null)
        {
            PreviousResultTitle = "NO PREVIOUS ROAST";
            PreviousResultTime = "—";
            PreviousResultDetails = "No completed result exists for this bean yet.";
        }
        else
        {
            PreviousResultTitle = "LAST ROAST";
            PreviousResultTime = previous.FormattedTime;
            PreviousResultDetails =
                $"{previous.Temperature:0.#} °C · {previous.BatchWeight:0.#} → {previous.FinalWeight:0.#} g · " +
                $"{previous.WeightLossPercentage:0.0}% · {previous.RoastLevelName}";
        }

        NewerAwaitingWeightNote = suggestion.NewerAwaitingWeightCount switch
        {
            0 => string.Empty,
            1 => "1 newer batch still needs weight",
            int count => $"{count} newer batches still need weight"
        };
    }

    private async Task ApplyPreviousResultForBeanAsync(Guid beanId) =>
        ApplyPreviousResult(await _queryService.GetSetupSuggestionAsync(beanId));

    private void ClearSuggestion()
    {
        TemperatureText = string.Empty;
        BatchWeightText = string.Empty;
        PreviousResultTitle = "NO PREVIOUS ROAST";
        PreviousResultTime = "—";
        PreviousResultDetails = "Choose a bean to load its last completed result.";
        NewerAwaitingWeightNote = string.Empty;
        InventoryWarning = string.Empty;
    }

    private void UpdateInventoryWarning()
    {
        if (SelectedBean is null || !TryParseNumber(BatchWeightText, out double grams))
        {
            InventoryWarning = string.Empty;
            return;
        }

        double availableGrams = SelectedBean.RemainingQuantity * 1000d;
        InventoryWarning = grams > availableGrams
            ? $"Only {availableGrams:0.#} g recorded in inventory — you can still start."
            : string.Empty;
    }

    private bool ValidateSetup(bool showErrors)
    {
        bool temperatureValid = TryParseNumber(TemperatureText, out double temperature) &&
                                temperature > 0 && temperature <= 500;
        bool weightValid = TryParseNumber(BatchWeightText, out double weight) && weight > 0;
        if (showErrors)
        {
            TemperatureError = temperatureValid ? string.Empty : "Enter a temperature between 0 and 500 °C.";
            BatchWeightError = weightValid ? string.Empty : "Enter a batch weight greater than 0 g.";
        }

        return temperatureValid && weightValid;
    }

    private void ApplyElapsed(double elapsed)
    {
        double safe = double.IsFinite(elapsed) ? Math.Max(0, elapsed) : 0;
        ElapsedDisplay = FormatElapsed(safe);
        ElapsedSweep = RoastInstrumentGeometry.ElapsedSweep(safe);
    }

    private double CurrentElapsedSeconds()
    {
        if (_activeRoast is null)
        {
            return 0;
        }

        return _elapsedAtSnapshot + (_activeRoast.IsRunning
            ? Math.Max(0, (_clock.UtcNow - _snapshotAtUtc).TotalSeconds)
            : 0);
    }

    private bool TryParseLocalEnd(string? text, out DateTimeOffset utc)
    {
        utc = default;
        if (!TimeOnly.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out TimeOnly time))
        {
            return false;
        }

        DateTimeOffset localNow = _clock.UtcNow.ToLocalTime();
        DateTime localEnd = localNow.Date + time.ToTimeSpan();
        if (localEnd > localNow.DateTime.AddMinutes(1))
        {
            localEnd = localEnd.AddDays(-1);
        }

        utc = new DateTimeOffset(localEnd, localNow.Offset).ToUniversalTime();
        return true;
    }

    private bool TryGetRecoveryElapsed(out double? correctedElapsed)
    {
        correctedElapsed = null;
        if (!RecoveryRequiresCorrectedTime)
        {
            return true;
        }

        string[] parts = RecoveryCorrectedElapsedText.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int minutes) ||
            !int.TryParse(parts[1], out int seconds) || minutes < 0 || seconds is < 0 or >= 60)
        {
            return false;
        }

        correctedElapsed = minutes * 60d + seconds;
        return true;
    }

    private static BatchChoice ToBatchChoice(RoastWorkItem item) => new()
    {
        RoastId = item.RoastId,
        BatchNumber = item.BatchNumber,
        BeanDisplaySnapshot = item.BeanDisplaySnapshot,
        BatchWeight = item.BatchWeight,
        DroppedAtUtc = item.DroppedAtUtc,
        TotalSeconds = item.TotalSeconds
    };

    private static bool TryParseNumber(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
        double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    private static double ParseNumber(string text) =>
        TryParseNumber(text, out double value) ? value : 0;

    public static string FormatElapsed(double seconds)
    {
        int wholeSeconds = Math.Max(0, (int)Math.Floor(seconds));
        return $"{wholeSeconds / 60:D2}:{wholeSeconds % 60:D2}";
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _sessionService.SnapshotChanged += OnSnapshotChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _sessionService.SnapshotChanged -= OnSnapshotChanged;
        _subscribed = false;
    }

    private void OnSnapshotChanged(object? sender, RoastSessionSnapshot snapshot)
    {
        MainThread.BeginInvokeOnMainThread(async () => await ApplySnapshotAsync(snapshot));
    }
}
