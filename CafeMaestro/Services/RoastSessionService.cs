using System.Diagnostics;
using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>
/// Owns every roast/session transition. Each command is serialized, validated against the
/// committed graph inside one lock-scoped mutation, and reported back as a snapshot.
/// </summary>
public sealed class RoastSessionService : IRoastSessionService, IDisposable
{
    private readonly IAppDataService _appDataService;
    private readonly IRoastLevelService _roastLevelService;
    private readonly IRoastPreferencesService _roastPreferencesService;
    private readonly ICoolingNotificationService _coolingNotificationService;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private bool _recoveryAcknowledged;

    /// <summary>The batch this process dropped most recently, used to answer a repeated tap.</summary>
    private Guid? _lastDroppedRoastId;
    private bool _isDisposed;

    public RoastSessionService(
        IAppDataService appDataService,
        IRoastLevelService roastLevelService,
        IRoastPreferencesService roastPreferencesService,
        ICoolingNotificationService coolingNotificationService,
        IClock clock)
    {
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _roastLevelService = roastLevelService ?? throw new ArgumentNullException(nameof(roastLevelService));
        _roastPreferencesService = roastPreferencesService
            ?? throw new ArgumentNullException(nameof(roastPreferencesService));
        _coolingNotificationService = coolingNotificationService
            ?? throw new ArgumentNullException(nameof(coolingNotificationService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _appDataService.DataChanged += OnDataChanged;
    }

    public event EventHandler<RoastSessionSnapshot>? SnapshotChanged;

    public Task<RoastSessionSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildSnapshot(_appDataService.CurrentData));
    }

    public async Task<TransitionResult> StartAsync(
        RoastSetup setup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int coolingDurationSeconds = await _roastPreferencesService.GetCoolingDurationSecondsAsync();
        bool firstCrackEnabled = await _roastPreferencesService.GetFirstCrackEnabledAsync();

        return await ExecuteAsync(
            (data, now, context) =>
            {
                if (data.ActiveRoastSession?.ActiveRoast is not null)
                {
                    return context.Reject(
                        RoastTransitionError.ActiveRoastAlreadyExists,
                        "A batch is already roasting.");
                }

                BeanData? bean = data.Beans.FirstOrDefault(candidate => candidate.Id == setup.BeanId);
                if (bean is null)
                {
                    return context.Reject(
                        RoastTransitionError.BeanNotFound,
                        "The selected bean is no longer in the inventory.");
                }

                double batchWeight = RoastPreferenceDefaults.NormalizeGrams(setup.BatchWeight);
                if (!double.IsFinite(setup.Temperature) ||
                    setup.Temperature <= 0 ||
                    setup.Temperature > 500 ||
                    !double.IsFinite(batchWeight) ||
                    batchWeight <= 0)
                {
                    return context.Reject(
                        RoastTransitionError.InvalidSetup,
                        "Enter a temperature between 1 and 500 °C and a batch weight above 0 g.");
                }

                RoastSessionData session = data.ActiveRoastSession ??= new RoastSessionData
                {
                    Id = Guid.NewGuid(),
                    StartedAtUtc = now,
                    NextBatchNumber = 1
                };

                session.ActiveRoast = new ActiveRoastDraft
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    BatchNumber = session.NextBatchNumber,
                    BeanId = bean.Id,
                    BeanDisplaySnapshot = bean.DisplayName,
                    Temperature = setup.Temperature,
                    BatchWeight = batchWeight,
                    Phase = ActiveRoastPhase.Roasting,
                    StartedAtUtc = now,
                    RunningSinceUtc = now,
                    AccumulatedElapsedSeconds = 0,
                    FirstCrackEnabled = firstCrackEnabled,
                    CoolingDurationSeconds = coolingDurationSeconds
                };

                return true;
            },
            onCommitted: () => _recoveryAcknowledged = true,
            cancellationToken);
    }

    public Task<TransitionResult> PauseAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            (data, now, context) =>
            {
                ActiveRoastDraft? draft = data.ActiveRoastSession?.ActiveRoast;
                if (draft is null)
                {
                    return context.Reject(RoastTransitionError.NoActiveRoast, "No batch is roasting.");
                }

                if (draft.Phase != ActiveRoastPhase.Roasting)
                {
                    return context.Reject(
                        RoastTransitionError.InvalidPhase,
                        "The batch is already paused.");
                }

                draft.AccumulatedElapsedSeconds = RoastProjection.ElapsedSeconds(draft, now);
                draft.RunningSinceUtc = null;
                draft.Phase = ActiveRoastPhase.Paused;
                return true;
            },
            onCommitted: () => _recoveryAcknowledged = true,
            cancellationToken);

    public Task<TransitionResult> ResumeAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            (data, now, context) =>
            {
                ActiveRoastDraft? draft = data.ActiveRoastSession?.ActiveRoast;
                if (draft is null)
                {
                    return context.Reject(RoastTransitionError.NoActiveRoast, "No batch is roasting.");
                }

                if (draft.Phase != ActiveRoastPhase.Paused)
                {
                    return context.Reject(
                        RoastTransitionError.InvalidPhase,
                        "The batch is already roasting.");
                }

                // Anchor to the later of now and the start, so a rolled-back clock cannot
                // produce a running interval that precedes the roast itself.
                draft.RunningSinceUtc = now < draft.StartedAtUtc ? draft.StartedAtUtc : now;
                draft.Phase = ActiveRoastPhase.Roasting;
                return true;
            },
            onCommitted: () => _recoveryAcknowledged = true,
            cancellationToken);

    public Task<TransitionResult> MarkFirstCrackAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            (data, now, context) =>
            {
                ActiveRoastDraft? draft = data.ActiveRoastSession?.ActiveRoast;
                if (draft is null)
                {
                    return context.Reject(RoastTransitionError.NoActiveRoast, "No batch is roasting.");
                }

                if (!draft.FirstCrackEnabled)
                {
                    return context.Reject(
                        RoastTransitionError.FirstCrackUnavailable,
                        "First Crack tracking was off when this batch started.");
                }

                if (draft.FirstCrackElapsedSeconds.HasValue)
                {
                    return context.Reject(
                        RoastTransitionError.FirstCrackUnavailable,
                        "First Crack is already marked for this batch.");
                }

                draft.FirstCrackElapsedSeconds =
                    (int)Math.Floor(RoastProjection.ElapsedSeconds(draft, now));
                return true;
            },
            onCommitted: () => _recoveryAcknowledged = true,
            cancellationToken);

    public async Task<TransitionResult> DropAsync(
        DateTimeOffset? correctedDropUtc = null,
        CancellationToken cancellationToken = default)
    {
        bool notificationsEnabled =
            await _roastPreferencesService.GetCoolingNotificationsEnabledAsync();
        RoastData? droppedRoast = null;

        // The batch this call is acting on. A competing tap that has not committed yet is still
        // visible as the active draft; one that has already committed is the last dropped batch.
        Guid? requestedRoastId = _appDataService.CurrentData.ActiveRoastSession?.ActiveRoast?.Id
            ?? _lastDroppedRoastId;

        TransitionResult result = await ExecuteAsync(
            (data, now, context) =>
            {
                RoastSessionData? session = data.ActiveRoastSession;
                ActiveRoastDraft? draft = session?.ActiveRoast;
                if (draft is null)
                {
                    // A retry after the mutation already committed must not create a second roast.
                    return context.Reject(
                        RoastTransitionError.NoActiveRoast,
                        "No batch is roasting.");
                }

                if (data.RoastLogs.Any(roast => roast.Id == draft.Id))
                {
                    return context.Reject(
                        RoastTransitionError.RoastAlreadyResolved,
                        "This batch is already logged.");
                }

                DateTimeOffset dropUtc = correctedDropUtc ?? now;
                if (dropUtc < draft.StartedAtUtc || dropUtc > now)
                {
                    return context.Reject(
                        RoastTransitionError.InvalidDropTime,
                        "The drop time must fall between the start of the roast and now.");
                }

                RoastData roast = MaterializeRoast(draft, dropUtc, RoastCompletionStatus.AwaitingWeight);
                data.RoastLogs.Add(roast);
                DecrementInventory(data, draft.BeanId, draft.BatchWeight);
                session!.ActiveRoast = null;
                session.NextBatchNumber = session.NextBatchNumber + 1;
                droppedRoast = roast;
                return true;
            },
            onCommitted: () => _recoveryAcknowledged = true,
            cancellationToken);

        if (result.Success && droppedRoast is not null)
        {
            _lastDroppedRoastId = droppedRoast.Id;
            if (notificationsEnabled)
            {
                string? warning = await TryScheduleCoolingNotificationAsync(
                    droppedRoast,
                    cancellationToken);
                if (warning is not null)
                {
                    result = result with { Warning = warning };
                }
            }
        }

        // A second tap on the same batch arrives after the first drop already committed. The
        // physical event happened once, so this caller receives the resulting snapshot rather
        // than an error it cannot act on.
        if (!result.Success &&
            result.Error is RoastTransitionError.NoActiveRoast
                or RoastTransitionError.RoastAlreadyResolved &&
            requestedRoastId is Guid roastId &&
            _appDataService.CurrentData.RoastLogs.Any(roast => roast.Id == roastId))
        {
            return TransitionResult.Ok(result.Snapshot);
        }

        return result;
    }

    public Task<TransitionResult> DiscardAsync(
        bool beansWereUsed,
        bool keepLog,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            (data, now, context) =>
            {
                RoastSessionData? session = data.ActiveRoastSession;
                ActiveRoastDraft? draft = session?.ActiveRoast;
                if (draft is null)
                {
                    return context.Reject(RoastTransitionError.NoActiveRoast, "No batch is roasting.");
                }

                if (keepLog && !data.RoastLogs.Any(roast => roast.Id == draft.Id))
                {
                    DateTimeOffset endedAt = now < draft.StartedAtUtc ? draft.StartedAtUtc : now;
                    RoastData roast = MaterializeRoast(
                        draft,
                        endedAt,
                        RoastCompletionStatus.Discarded);
                    data.RoastLogs.Add(roast);
                }

                if (beansWereUsed)
                {
                    DecrementInventory(data, draft.BeanId, draft.BatchWeight);
                }

                session!.ActiveRoast = null;
                return true;
            },
            onCommitted: () => _recoveryAcknowledged = true,
            cancellationToken);

    public async Task<TransitionResult> SaveFinalWeightAsync(
        Guid roastId,
        double grams,
        CancellationToken cancellationToken = default)
    {
        double finalWeight = RoastPreferenceDefaults.NormalizeGrams(grams);
        if (!double.IsFinite(finalWeight) || finalWeight <= 0)
        {
            return TransitionResult.Fail(
                RoastTransitionError.InvalidWeight,
                "Enter a final weight above 0 g.",
                BuildSnapshot(_appDataService.CurrentData));
        }

        RoastData? target = _appDataService.CurrentData.RoastLogs
            .FirstOrDefault(roast => roast.Id == roastId);
        if (target is null)
        {
            return TransitionResult.Fail(
                RoastTransitionError.RoastNotFound,
                "That batch is no longer in the roast log.",
                BuildSnapshot(_appDataService.CurrentData));
        }

        if (finalWeight > target.BatchWeight)
        {
            return TransitionResult.Fail(
                RoastTransitionError.InvalidWeight,
                $"More than the {target.BatchWeight:0.#} g loaded — did you weigh both batches together?",
                BuildSnapshot(_appDataService.CurrentData));
        }

        // The level name depends only on the loss percentage, so it is resolved before the
        // mutation and the mutation itself stays synchronous inside the data lock.
        double weightLossPercentage = Math.Round(
            (target.BatchWeight - finalWeight) / target.BatchWeight * 100,
            2);
        string roastLevelName = await _roastLevelService.GetRoastLevelNameAsync(weightLossPercentage);

        TransitionResult result = await ExecuteAsync(
            (data, _, context) =>
            {
                RoastData? roast = data.RoastLogs.FirstOrDefault(candidate => candidate.Id == roastId);
                if (roast is null)
                {
                    return context.Reject(
                        RoastTransitionError.RoastNotFound,
                        "That batch is no longer in the roast log.");
                }

                if (roast.CompletionStatus != RoastCompletionStatus.AwaitingWeight)
                {
                    return context.Reject(
                        RoastTransitionError.RoastAlreadyResolved,
                        "This batch has already been resolved.");
                }

                if (finalWeight > roast.BatchWeight)
                {
                    return context.Reject(
                        RoastTransitionError.InvalidWeight,
                        "The final weight cannot exceed the batch weight.");
                }

                roast.FinalWeight = finalWeight;
                roast.CompletionStatus = RoastCompletionStatus.Complete;
                roast.RoastLevelName = roastLevelName;
                return true;
            },
            onCommitted: null,
            cancellationToken);

        if (result.Success)
        {
            await TryCancelCoolingNotificationAsync(roastId, cancellationToken);
        }

        return result;
    }

    public async Task<TransitionResult> MarkUnweighedAsync(
        Guid roastId,
        CancellationToken cancellationToken = default)
    {
        TransitionResult result = await ExecuteAsync(
            (data, _, context) =>
            {
                RoastData? roast = data.RoastLogs.FirstOrDefault(candidate => candidate.Id == roastId);
                if (roast is null)
                {
                    return context.Reject(
                        RoastTransitionError.RoastNotFound,
                        "That batch is no longer in the roast log.");
                }

                if (roast.CompletionStatus != RoastCompletionStatus.AwaitingWeight)
                {
                    return context.Reject(
                        RoastTransitionError.RoastAlreadyResolved,
                        "This batch has already been resolved.");
                }

                roast.FinalWeight = null;
                roast.CompletionStatus = RoastCompletionStatus.Unweighed;
                // A named status rather than an empty string, so the legacy log projection does
                // not silently rewrite the row back to "Pending" on its next load.
                roast.RoastLevelName = "Unweighed";
                return true;
            },
            onCommitted: null,
            cancellationToken);

        if (result.Success)
        {
            await TryCancelCoolingNotificationAsync(roastId, cancellationToken);
        }

        return result;
    }

    public Task<TransitionResult> FinishSessionAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            (data, _, context) =>
            {
                if (data.ActiveRoastSession is null)
                {
                    return context.Reject(
                        RoastTransitionError.NoActiveRoast,
                        "There is no open session to finish.");
                }

                if (data.ActiveRoastSession.ActiveRoast is not null)
                {
                    return context.Reject(
                        RoastTransitionError.ActiveRoastBlocksAction,
                        "Drop or discard the current batch before finishing the session.");
                }

                // Cooling and Needs-weight roasts are separate records and stay open.
                data.ActiveRoastSession = null;
                return true;
            },
            onCommitted: null,
            cancellationToken);

    public async Task<TransitionResult> RecoverAsync(
        RecoveryDecision decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);

        switch (decision.Kind)
        {
            case RecoveryDecisionKind.Discard:
                _recoveryAcknowledged = true;
                return await DiscardAsync(
                    decision.BeansWereUsed,
                    decision.KeepLog,
                    cancellationToken);

            case RecoveryDecisionKind.EndedAt:
                if (!decision.EndedAtUtc.HasValue)
                {
                    return TransitionResult.Fail(
                        RoastTransitionError.InvalidDropTime,
                        "A corrected end time is required.",
                        BuildSnapshot(_appDataService.CurrentData));
                }

                _recoveryAcknowledged = true;
                return await DropAsync(decision.EndedAtUtc.Value, cancellationToken);

            default:
                return await ExecuteAsync(
                    (data, now, context) =>
                    {
                        ActiveRoastDraft? draft = data.ActiveRoastSession?.ActiveRoast;
                        if (draft is null)
                        {
                            return context.Reject(
                                RoastTransitionError.NoActiveRoast,
                                "There is no roast to recover.");
                        }

                        // Confirming "still going" folds the interval the app was closed for
                        // into the accumulated total, which is the time the batch really spent
                        // roasting, and then re-anchors to the current clock.
                        double elapsedSeconds = RoastProjection.ElapsedSeconds(draft, now);

                        if (now < draft.StartedAtUtc)
                        {
                            // The device clock moved behind the roast's own anchors. Rebase them
                            // onto the current clock so elapsed time keeps advancing instead of
                            // stalling; the time already earned is held in the accumulated total.
                            RoastSessionData session = data.ActiveRoastSession!;
                            if (session.StartedAtUtc > now)
                            {
                                session.StartedAtUtc = now;
                            }

                            draft.StartedAtUtc = now;
                        }

                        if (draft.Phase == ActiveRoastPhase.Roasting)
                        {
                            draft.AccumulatedElapsedSeconds = elapsedSeconds;
                            draft.RunningSinceUtc = now;
                        }

                        if (draft.FirstCrackElapsedSeconds >
                            (int)Math.Floor(draft.AccumulatedElapsedSeconds) &&
                            draft.Phase == ActiveRoastPhase.Paused)
                        {
                            draft.FirstCrackElapsedSeconds =
                                (int)Math.Floor(draft.AccumulatedElapsedSeconds);
                        }

                        return true;
                    },
                    onCommitted: () => _recoveryAcknowledged = true,
                    cancellationToken);
        }
    }

    private RoastData MaterializeRoast(
        ActiveRoastDraft draft,
        DateTimeOffset endedAtUtc,
        RoastCompletionStatus completionStatus)
    {
        int totalSeconds = (int)Math.Floor(Math.Min(
            RoastProjection.ElapsedSeconds(draft, endedAtUtc),
            RoastProjection.MaxPlausibleRoastSeconds));
        int? firstCrackSeconds = draft.FirstCrackElapsedSeconds.HasValue
            ? Math.Min(draft.FirstCrackElapsedSeconds.Value, totalSeconds)
            : null;

        var roast = new RoastData
        {
            Id = draft.Id,
            SessionId = draft.SessionId,
            BatchNumber = draft.BatchNumber,
            BeanId = draft.BeanId,
            BeanDisplaySnapshot = draft.BeanDisplaySnapshot,
            BeanType = draft.BeanDisplaySnapshot,
            Temperature = draft.Temperature,
            BatchWeight = draft.BatchWeight,
            FinalWeight = null,
            RoastMinutes = totalSeconds / 60,
            RoastSeconds = totalSeconds % 60,
            RoastDate = endedAtUtc.ToLocalTime().DateTime,
            DroppedAtUtc = endedAtUtc,
            CompletionStatus = completionStatus,
            Notes = string.Empty,
            FirstCrackMinutes = firstCrackSeconds / 60,
            FirstCrackSeconds = firstCrackSeconds % 60
        };

        if (completionStatus == RoastCompletionStatus.AwaitingWeight)
        {
            roast.CoolingDurationSeconds = draft.CoolingDurationSeconds;
            roast.RoastLevelName = "Pending";
        }
        else
        {
            // A discarded batch owes no weight and no cooling window, and carries a named status
            // so the legacy log projection does not rewrite it to "Pending".
            roast.RoastLevelName = "Discarded";
        }

        return roast;
    }

    private static void DecrementInventory(AppData data, Guid beanId, double batchWeightGrams)
    {
        BeanData? bean = data.Beans.FirstOrDefault(candidate => candidate.Id == beanId);
        if (bean is null)
        {
            return;
        }

        double usedKilograms = batchWeightGrams / 1000.0;
        bean.RemainingQuantity = Math.Max(
            0,
            Math.Round(bean.RemainingQuantity - usedKilograms, 6, MidpointRounding.AwayFromZero));
    }

    private async Task<string?> TryScheduleCoolingNotificationAsync(
        RoastData roast,
        CancellationToken cancellationToken)
    {
        try
        {
            await _coolingNotificationService.ScheduleCoolingReadyAsync(
                roast.Id,
                RoastProjection.ReadyToWeighAtUtc(roast),
                roast.BeanDisplaySnapshot,
                cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cooling notification could not be scheduled: {ex.Message}");
            return "The roast is saved. A cooling reminder could not be scheduled.";
        }
    }

    private async Task TryCancelCoolingNotificationAsync(
        Guid roastId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _coolingNotificationService.CancelAsync(roastId, cancellationToken);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cooling notification could not be cancelled: {ex.Message}");
        }
    }

    private async Task<TransitionResult> ExecuteAsync(
        Func<AppData, DateTimeOffset, RejectionContext, bool> mutation,
        Action? onCommitted,
        CancellationToken cancellationToken)
    {
        await _commandLock.WaitAsync(cancellationToken);
        try
        {
            DateTimeOffset now = _clock.UtcNow;
            var context = new RejectionContext();
            bool committed = await _appDataService.TryUpdateAsync(
                data => mutation(data, now, context),
                cancellationToken);

            if (committed)
            {
                onCommitted?.Invoke();
                return TransitionResult.Ok(BuildSnapshot(_appDataService.CurrentData));
            }

            return TransitionResult.Fail(
                context.Error == RoastTransitionError.None
                    ? RoastTransitionError.PersistenceFailed
                    : context.Error,
                context.Message ?? "The change could not be saved. Your roast data is unchanged.",
                BuildSnapshot(_appDataService.CurrentData));
        }
        finally
        {
            _commandLock.Release();
        }
    }

    private RoastSessionSnapshot BuildSnapshot(AppData data)
    {
        DateTimeOffset now = _clock.UtcNow;
        RoastSessionData? session = data.ActiveRoastSession;
        ActiveRoastDraft? draft = session?.ActiveRoast;

        return new RoastSessionSnapshot
        {
            AsOfUtc = now,
            SessionId = session?.Id,
            NextBatchNumber = session?.NextBatchNumber ?? 1,
            ActiveRoast = draft is null ? null : RoastProjection.ToSnapshot(draft, now),
            OpenWork = RoastProjection.OpenWork(data, now),
            RequiresRecovery = draft is not null && !_recoveryAcknowledged
        };
    }

    private void OnDataChanged(object? sender, AppData data)
    {
        try
        {
            SnapshotChanged?.Invoke(this, BuildSnapshot(data));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Snapshot subscriber failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _appDataService.DataChanged -= OnDataChanged;
        _commandLock.Dispose();
    }

    /// <summary>Carries a precondition failure out of the synchronous mutation delegate.</summary>
    private sealed class RejectionContext
    {
        public RoastTransitionError Error { get; private set; } = RoastTransitionError.None;
        public string? Message { get; private set; }

        public bool Reject(RoastTransitionError error, string message)
        {
            Error = error;
            Message = message;
            return false;
        }
    }
}
