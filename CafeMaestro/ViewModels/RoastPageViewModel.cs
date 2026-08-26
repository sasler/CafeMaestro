using System.Collections.ObjectModel;
using System.Globalization;
using CafeMaestro.Drawing;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace CafeMaestro.ViewModels;

public partial class RoastPageViewModel : ObservableObject, IQueryAttributable
{
    private readonly IRoastSessionService _sessionService;
    private readonly IRoastQueryService _queryService;
    private readonly IBeanDataService _beanService;
    private readonly IOverlayService _overlayService;
    private readonly IDisplayWakeService _displayWakeService;
    private readonly IRoastRecoveryAdapter _recoveryAdapter;
    private readonly INavigationService _navigationService;
    private readonly IAlertService _alertService;
    private readonly IClock _clock;
    private readonly object _lifecycleSync = new();
    private readonly SemaphoreSlim _lifecycleWakeGate = new(1, 1);

    private RoastSessionSnapshot? _snapshot;
    private ActiveRoastSnapshot? _activeRoast;
    private double _elapsedAtSnapshot;
    private DateTimeOffset _snapshotAtUtc;
    private bool _subscribed;
    private bool _suppressBeanSelectionChanged;
    private Guid _requestedBeanId;
    private Func<Task>? _retryAction;
    private DropProposal? _pendingDropProposal;
    private long _lifecycleGeneration;
    private CancellationTokenSource? _lifecycleCancellation;

    [ObservableProperty]
    public partial RoastPresentationState PresentationState { get; set; } = RoastPresentationState.Setup;

    [ObservableProperty]
    public partial bool IsWindowStopped { get; set; }

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
    public partial string ActiveTimerSemanticDescription { get; set; } = "Roasting, 0 seconds";

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
    public bool HasChannels => Channels.Count > 0;
    public bool CanMarkFirstCrack => IsFirstCrackVisible;
    public bool CanKeepRoastingAfterRecovery => !RecoveryRequiresCorrectedTime;

    public RoastPageViewModel(
        IRoastSessionService sessionService,
        IRoastQueryService queryService,
        IBeanDataService beanService,
        IOverlayService overlayService,
        IDisplayWakeService displayWakeService,
        IRoastRecoveryAdapter recoveryAdapter,
        INavigationService navigationService,
        IAlertService alertService,
        IClock clock)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _beanService = beanService ?? throw new ArgumentNullException(nameof(beanService));
        _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
        _displayWakeService = displayWakeService ?? throw new ArgumentNullException(nameof(displayWakeService));
        _recoveryAdapter = recoveryAdapter ?? throw new ArgumentNullException(nameof(recoveryAdapter));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
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
        if (!_suppressBeanSelectionChanged)
        {
            _ = SelectBeanAsync(value);
        }
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

    partial void OnChannelsChanged(ObservableCollection<RoastChannelPresentation> value) =>
        OnPropertyChanged(nameof(HasChannels));

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
        UpdateActiveTimerSemantic(CurrentElapsedSeconds());
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("BeanId", out object? beanIdValue) &&
            Guid.TryParse(beanIdValue?.ToString(), out Guid beanId) && beanId != Guid.Empty)
        {
            _requestedBeanId = beanId;
        }
    }

    public async Task OnAppearingAsync()
    {
        Subscribe();
        await LoadBeansAsync();
        if (_requestedBeanId != Guid.Empty)
        {
            await SelectRequestedBeanAsync(_requestedBeanId);
            _requestedBeanId = Guid.Empty;
        }
        await RefreshAsync();
    }

    public async Task OnDisappearingAsync()
    {
        Unsubscribe();
        await SetWakeForGenerationAsync(false, CurrentLifecycleGeneration());
    }

    public async Task OnWindowStoppedAsync()
    {
        (long generation, CancellationTokenSource? cancellation) = BeginWindowStopped();
        cancellation?.Cancel();
        await SetWakeForGenerationAsync(false, generation);
    }

    public async Task OnWindowResumedAsync()
    {
        (long generation, CancellationTokenSource cancellation) = BeginWindowResumed();
        try
        {
            RoastSessionSnapshot snapshot = await _sessionService.GetSnapshotAsync(cancellation.Token);
            if (!IsCurrentLifecycleGeneration(generation))
            {
                return;
            }

            if (!TryMarkWindowResumed(generation))
            {
                return;
            }

            if (PresentationState == RoastPresentationState.PersistenceError && _retryAction is not null)
            {
                _snapshot = snapshot;
                _activeRoast = snapshot.ActiveRoast;
                _snapshotAtUtc = snapshot.AsOfUtc;
                _elapsedAtSnapshot = snapshot.ActiveRoast?.ElapsedSeconds ?? 0;
                await SetWakeForGenerationAsync(false, generation);
                return;
            }

            await ApplySnapshotAsync(snapshot, generation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer lifecycle event owns the state now.
        }
        finally
        {
            CompleteWindowResume(generation, cancellation);
        }
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
        _pendingDropProposal ??= new DropProposal(_clock.UtcNow, CurrentElapsedSeconds());
        await CommitDropProposalAsync(_pendingDropProposal);
    }

    [RelayCommand]
    public async Task PrimaryHandoffActionAsync()
    {
        if (_snapshot is null)
        {
            return;
        }

        RoastWorkItem? readyOldest = CurrentSessionWork()
            .Where(IsReadyNow)
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
        RoastWorkItem? newest = CurrentSessionWork()
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

        RoastWorkItem? item = CurrentSessionWork().FirstOrDefault(work => work.RoastId == channel.RoastId);
        if (item is not null && IsReadyNow(item))
        {
            await ShowWeighInAsync(item);
        }
    }

    /// <summary>
    /// Releases one cooling batch early. The countdown is a convenience, not a measurement, so a
    /// roaster who can feel the beans are cool confirms once and the batch moves to Needs weight
    /// with no weight invented on their behalf.
    /// </summary>
    [RelayCommand]
    public async Task CompleteCoolingAsync(RoastChannelPresentation? channel)
    {
        if (channel is null || _snapshot is null)
        {
            return;
        }

        RoastWorkItem? item = CurrentSessionWork().FirstOrDefault(work => work.RoastId == channel.RoastId);
        if (item is null || IsReadyNow(item))
        {
            return;
        }

        string batchLabel = item.BatchNumber is int batch ? $"Batch {batch}" : "This batch";
        bool confirmed = await _alertService.ShowConfirmationAsync(
            "Stop cooling?",
            $"{batchLabel} moves to Needs weight now. Its countdown and cooling reminder end; " +
                "the final weight is still yours to enter.",
            "READY NOW",
            "KEEP COOLING");
        if (!confirmed)
        {
            return;
        }

        await HandleTransitionAsync(
            await _sessionService.CompleteCoolingAsync(item.RoastId),
            () => CompleteCoolingAsync(channel));
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
    public async Task OpenDataSettingsAsync()
    {
        if (PresentationState != RoastPresentationState.PersistenceError)
        {
            return;
        }

        // The escape hatch now opens the focused Data & Backups page directly rather than the
        // Settings index, so recovery is one step away from the error screen.
        await _navigationService.GoToAsync(
            Routes.DataSettings,
            new Dictionary<string, object>
            {
                [DataSettingsPageViewModel.PersistenceRecoveryKey] = bool.TrueString
            });
        _pendingDropProposal = null;
        _retryAction = null;
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
            if (PresentationState == RoastPresentationState.Handoff)
            {
                UpdateHandoffActions();
            }
            return;
        }

        double elapsed = _elapsedAtSnapshot;
        if (_activeRoast.IsRunning)
        {
            elapsed += Math.Max(0, (_clock.UtcNow - _snapshotAtUtc).TotalSeconds);
        }

        ApplyElapsed(elapsed);
        if (_activeRoast.FirstCrackElapsedSeconds is int firstCrackSeconds)
        {
            double developmentSeconds = Math.Max(0, elapsed - firstCrackSeconds);
            DevelopmentDisplay = FormatElapsed(developmentSeconds);
            DtrDisplay = elapsed > 0 ? $"{developmentSeconds / elapsed * 100:0.0}%" : "0.0%";
        }

        UpdateChannels();
    }

    public async Task<bool> HandleBackNavigationAsync()
    {
        if (PresentationState == RoastPresentationState.PersistenceError)
        {
            return true;
        }

        if (PresentationState is not RoastPresentationState.Active and not RoastPresentationState.Recovery)
        {
            return false;
        }

        if (PresentationState == RoastPresentationState.Recovery)
        {
            await ConfirmAndDiscardAsync();
            return true;
        }

        NavigationChoice choice = await _overlayService.ConfirmNavigationAsync();
        if (choice == NavigationChoice.KeepRoasting)
        {
            return true;
        }

        await ConfirmAndDiscardAsync();
        return true;
    }

    [RelayCommand]
    public async Task DiscardRecoveryAsync() => await ConfirmAndDiscardAsync();

    private async Task<bool> ConfirmAndDiscardAsync()
    {
        if (_activeRoast is null)
        {
            return false;
        }

        DiscardOutcome outcome = await _overlayService.ShowDiscardAsync(new DiscardRequest
        {
            BeanDisplaySnapshot = _activeRoast.BeanDisplaySnapshot,
            BatchNumber = _activeRoast.BatchNumber,
            ElapsedDisplay = ElapsedDisplay
        });
        if (!outcome.ShouldDiscard)
        {
            return false;
        }

        TransitionResult result = await _sessionService.DiscardAsync(outcome.BeansWereUsed, outcome.KeepLog);
        await HandleTransitionAsync(result, ConfirmAndDiscardRetryAsync);
        return result.Success;
    }

    private async Task ConfirmAndDiscardRetryAsync() => await ConfirmAndDiscardAsync();

    private async Task LoadBeansAsync()
    {
        IReadOnlyList<BeanData> beans = await _beanService.GetSortedAvailableBeansAsync();
        AvailableBeans = new ObservableCollection<BeanData>(beans);
    }

    private async Task SelectRequestedBeanAsync(Guid beanId)
    {
        BeanData? bean = AvailableBeans.FirstOrDefault(candidate => candidate.Id == beanId)
            ?? await _beanService.GetBeanByIdAsync(beanId);
        if (bean is null)
        {
            _suppressBeanSelectionChanged = true;
            SelectedBean = null;
            _suppressBeanSelectionChanged = false;
            await _alertService.ShowAlertAsync(
                "Bean unavailable",
                "The selected bean is no longer in inventory.",
                "OK");
            return;
        }

        EnsureBeanAvailable(bean);
        _suppressBeanSelectionChanged = true;
        SelectedBean = bean;
        _suppressBeanSelectionChanged = false;
        await SelectBeanAsync(bean);
    }

    private async Task ApplySnapshotAsync(RoastSessionSnapshot snapshot, long? lifecycleGeneration = null)
    {
        long generation = lifecycleGeneration ?? CurrentLifecycleGeneration();
        if (lifecycleGeneration is not null && !IsCurrentLifecycleGeneration(generation))
        {
            return;
        }

        _snapshot = snapshot;
        _activeRoast = snapshot.ActiveRoast;
        _snapshotAtUtc = snapshot.AsOfUtc;
        _elapsedAtSnapshot = snapshot.ActiveRoast?.ElapsedSeconds ?? 0;
        _retryAction = null;
        _pendingDropProposal = null;
        ErrorMessage = string.Empty;

        if (snapshot.RequiresRecovery && snapshot.ActiveRoast is not null)
        {
            ApplyRecovery(snapshot.ActiveRoast);
            await SetWakeForGenerationAsync(false, generation);
            return;
        }

        if (snapshot.ActiveRoast is not null)
        {
            ApplyActive(snapshot.ActiveRoast);
            await SetWakeForGenerationAsync(
                snapshot.ActiveRoast.IsRunning && !IsWindowStopped,
                generation);
            return;
        }

        await SetWakeForGenerationAsync(false, generation);
        if (snapshot.HasSession && CurrentSessionWork().Count > 0)
        {
            ApplyHandoff(snapshot);
        }
        else
        {
            PresentationState = RoastPresentationState.Setup;
        }
    }

    private (long Generation, CancellationTokenSource? Cancellation) BeginWindowStopped()
    {
        lock (_lifecycleSync)
        {
            _lifecycleGeneration++;
            CancellationTokenSource? cancellation = _lifecycleCancellation;
            _lifecycleCancellation = null;
            IsWindowStopped = true;
            return (_lifecycleGeneration, cancellation);
        }
    }

    private (long Generation, CancellationTokenSource Cancellation) BeginWindowResumed()
    {
        CancellationTokenSource? previous;
        CancellationTokenSource current = new();
        long generation;
        lock (_lifecycleSync)
        {
            _lifecycleGeneration++;
            generation = _lifecycleGeneration;
            previous = _lifecycleCancellation;
            _lifecycleCancellation = current;
        }

        previous?.Cancel();
        return (generation, current);
    }

    private void CompleteWindowResume(long generation, CancellationTokenSource cancellation)
    {
        bool isCurrent;
        lock (_lifecycleSync)
        {
            isCurrent = _lifecycleGeneration == generation &&
                ReferenceEquals(_lifecycleCancellation, cancellation);
            if (isCurrent)
            {
                _lifecycleCancellation = null;
                IsWindowStopped = false;
            }
        }

        cancellation.Dispose();
    }

    private bool TryMarkWindowResumed(long generation)
    {
        lock (_lifecycleSync)
        {
            if (_lifecycleGeneration != generation)
            {
                return false;
            }

            IsWindowStopped = false;
            return true;
        }
    }

    private long CurrentLifecycleGeneration()
    {
        lock (_lifecycleSync)
        {
            return _lifecycleGeneration;
        }
    }

    private bool IsCurrentLifecycleGeneration(long generation)
    {
        lock (_lifecycleSync)
        {
            return _lifecycleGeneration == generation;
        }
    }

    private async Task SetWakeForGenerationAsync(bool keepScreenOn, long generation)
    {
        await _lifecycleWakeGate.WaitAsync();
        try
        {
            if (!IsCurrentLifecycleGeneration(generation) ||
                (keepScreenOn && IsWindowStopped))
            {
                return;
            }

            await _displayWakeService.SetKeepScreenOnAsync(keepScreenOn);
        }
        finally
        {
            _lifecycleWakeGate.Release();
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
        UpdateHandoffActions();
    }

    private void UpdateHandoffActions()
    {
        if (_snapshot is null)
        {
            return;
        }

        RoastWorkItem? readyOldest = CurrentSessionWork()
            .Where(IsReadyNow)
            .OrderBy(item => item.BatchNumber ?? int.MaxValue)
            .FirstOrDefault();
        if (_snapshot.NextBatchNumber == 2)
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
            SecondaryActionText = "DONE FOR NOW";
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
        foreach (RoastWorkItem item in CurrentSessionWork().OrderBy(work => work.BatchNumber ?? int.MaxValue))
        {
            double remaining = Math.Max(0, (item.ReadyToWeighAtUtc - _clock.UtcNow).TotalSeconds);
            bool ready = IsReadyNow(item);
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
            .Where(item => item.SessionId == _snapshot.SessionId)
            .OrderByDescending(item => item.BatchNumber ?? 0)
            .FirstOrDefault();
        if (source?.BeanId is not Guid beanId)
        {
            PresentationState = RoastPresentationState.Setup;
            return;
        }

        BeanData? bean = AvailableBeans.FirstOrDefault(candidate => candidate.Id == beanId)
            ?? await _beanService.GetBeanByIdAsync(beanId);
        if (bean is null)
        {
            PresentationState = RoastPresentationState.Setup;
            return;
        }

        EnsureBeanAvailable(bean);

        IReadOnlyList<RoastData> roasts = await _queryService.GetRoastsForBeanAsync(beanId);
        RoastData? dropped = roasts.FirstOrDefault(roast => roast.Id == source.RoastId);
        RoastSetupSuggestion suggestion = await _queryService.GetSetupSuggestionAsync(beanId);
        _suppressBeanSelectionChanged = true;
        SelectedBean = bean;
        _suppressBeanSelectionChanged = false;
        TemperatureText = (dropped?.Temperature ?? suggestion.Temperature)
            ?.ToString("0.#", CultureInfo.CurrentCulture) ?? string.Empty;
        BatchWeightText = source.BatchWeight.ToString("0.#", CultureInfo.CurrentCulture);
        ApplyPreviousResult(suggestion);
        UpdateInventoryWarning();
        PresentationState = RoastPresentationState.Setup;
    }

    private async Task ShowWeighInAsync(RoastWorkItem item)
    {
        IReadOnlyList<RoastWorkItem> ready = CurrentSessionWork().Where(IsReadyNow).ToList();
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

    private async Task CommitDropProposalAsync(DropProposal proposal)
    {
        TransitionResult result = await _sessionService.DropAsync(proposal);
        await HandleTransitionAsync(result, () => CommitDropProposalAsync(proposal));
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
        UpdateActiveTimerSemantic(safe);
    }

    private void UpdateActiveTimerSemantic(double seconds)
    {
        int wholeSeconds = Math.Max(0, (int)Math.Floor(double.IsFinite(seconds) ? seconds : 0));
        int minutes = wholeSeconds / 60;
        int remainingSeconds = wholeSeconds % 60;
        string duration = minutes switch
        {
            > 0 when remainingSeconds > 0 => $"{minutes} {(minutes == 1 ? "minute" : "minutes")} {remainingSeconds} {(remainingSeconds == 1 ? "second" : "seconds")}",
            > 0 => $"{minutes} {(minutes == 1 ? "minute" : "minutes")}",
            _ => $"{remainingSeconds} {(remainingSeconds == 1 ? "second" : "seconds")}"
        };
        ActiveTimerSemanticDescription = $"{(IsPaused ? "Paused" : "Roasting")}, {duration}";
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

    private IReadOnlyList<RoastWorkItem> CurrentSessionWork()
    {
        Guid? sessionId = _snapshot?.SessionId ?? _activeRoast?.SessionId;
        return sessionId is Guid current
            ? _snapshot?.OpenWork.Where(item => item.SessionId == current).ToList() ?? []
            : [];
    }

    private bool IsReadyNow(RoastWorkItem item) =>
        item.IsReadyToWeigh || item.ReadyToWeighAtUtc <= _clock.UtcNow;

    private void EnsureBeanAvailable(BeanData bean)
    {
        if (AvailableBeans.All(candidate => candidate.Id != bean.Id))
        {
            AvailableBeans.Add(bean);
        }
    }

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
        if (PresentationState == RoastPresentationState.PersistenceError && _retryAction is not null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () => await ApplySnapshotAsync(snapshot));
    }
}
