namespace CafeMaestro.Models;

/// <summary>
/// The status a roast presents to the user once the stored status is combined with the clock.
/// </summary>
public enum RoastEffectiveStatus
{
    Cooling,
    NeedsWeight,
    Complete,
    Unweighed,
    Discarded
}

/// <summary>
/// Why a requested roast transition was refused. <see cref="None"/> accompanies a success.
/// </summary>
public enum RoastTransitionError
{
    None,
    NoActiveRoast,
    ActiveRoastAlreadyExists,
    InvalidPhase,
    BeanNotFound,
    InvalidSetup,
    RoastNotFound,
    RoastAlreadyResolved,
    InvalidWeight,
    InvalidDropTime,

    /// <summary>
    /// The device clock sits behind the roast's own anchors, so the elapsed time cannot be
    /// derived. Recovery must supply an explicit corrected duration rather than guess one.
    /// </summary>
    CorrectedElapsedRequired,
    FirstCrackUnavailable,
    ActiveRoastBlocksAction,
    PersistenceFailed
}

/// <summary>The three fields a roast starts from.</summary>
public sealed record RoastSetup(Guid BeanId, double Temperature, double BatchWeight);

public enum RecoveryDecisionKind
{
    KeepRoasting,
    EndedAt,
    Discard
}

/// <summary>What the user answered when a persisted draft was found on a cold launch.</summary>
public sealed record RecoveryDecision
{
    private RecoveryDecision()
    {
    }

    public RecoveryDecisionKind Kind { get; private init; }
    public DateTimeOffset? EndedAtUtc { get; private init; }
    public bool BeansWereUsed { get; private init; }
    public bool KeepLog { get; private init; }

    /// <summary>
    /// The roast time the user states the batch actually ran. Required when the device clock
    /// sits behind the roast's anchors, because the app cannot derive it from a broken clock.
    /// See <see cref="ActiveRoastSnapshot.RequiresCorrectedElapsed"/>.
    /// </summary>
    public double? CorrectedElapsedSeconds { get; private init; }

    public static RecoveryDecision KeepRoasting() =>
        new() { Kind = RecoveryDecisionKind.KeepRoasting };

    /// <summary>Keep roasting on a corrected timeline, after a clock change broke the anchors.</summary>
    public static RecoveryDecision KeepRoasting(double correctedElapsedSeconds) =>
        new()
        {
            Kind = RecoveryDecisionKind.KeepRoasting,
            CorrectedElapsedSeconds = correctedElapsedSeconds
        };

    public static RecoveryDecision EndedAt(DateTimeOffset endedAtUtc) =>
        new() { Kind = RecoveryDecisionKind.EndedAt, EndedAtUtc = endedAtUtc };

    /// <summary>Record the drop on a corrected timeline, stating how long the batch ran.</summary>
    public static RecoveryDecision EndedAt(
        DateTimeOffset endedAtUtc,
        double correctedElapsedSeconds) =>
        new()
        {
            Kind = RecoveryDecisionKind.EndedAt,
            EndedAtUtc = endedAtUtc,
            CorrectedElapsedSeconds = correctedElapsedSeconds
        };

    public static RecoveryDecision Discard(bool beansWereUsed, bool keepLog) =>
        new() { Kind = RecoveryDecisionKind.Discard, BeansWereUsed = beansWereUsed, KeepLog = keepLog };
}

/// <summary>Time-derived view of the one draft that may be roasting or paused.</summary>
public sealed record ActiveRoastSnapshot
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required int BatchNumber { get; init; }
    public required Guid BeanId { get; init; }
    public required string BeanDisplaySnapshot { get; init; }
    public required double Temperature { get; init; }
    public required double BatchWeight { get; init; }
    public required ActiveRoastPhase Phase { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required double ElapsedSeconds { get; init; }
    public int? FirstCrackElapsedSeconds { get; init; }
    public required bool FirstCrackEnabled { get; init; }
    public required int CoolingDurationSeconds { get; init; }

    /// <summary>True when the wall clock moved backwards or produced an impossible duration.</summary>
    public required bool IsElapsedImplausible { get; init; }

    /// <summary>
    /// The clock sits behind this roast's own anchors, so the running interval is unknowable.
    /// Recovery must collect an explicit roast duration; it must not bank a clamped zero.
    /// </summary>
    public required bool RequiresCorrectedElapsed { get; init; }

    public bool IsRunning => Phase == ActiveRoastPhase.Roasting;

    /// <summary>Whole seconds since First Crack, when it has been marked.</summary>
    public int? DevelopmentSeconds => FirstCrackElapsedSeconds is int firstCrack
        ? Math.Max(0, (int)Math.Floor(ElapsedSeconds) - firstCrack)
        : null;

    /// <summary>Development time as a percentage of total elapsed time.</summary>
    public double? DevelopmentTimeRatio => DevelopmentSeconds is int development && ElapsedSeconds > 0
        ? Math.Round(development / ElapsedSeconds * 100, 1)
        : null;
}

/// <summary>A dropped roast projected against the clock for the open-work queue.</summary>
public sealed record RoastWorkItem
{
    public required Guid RoastId { get; init; }
    public Guid? SessionId { get; init; }
    public int? BatchNumber { get; init; }
    public Guid? BeanId { get; init; }
    public required string BeanDisplaySnapshot { get; init; }
    public required double Temperature { get; init; }
    public required double BatchWeight { get; init; }
    public required DateTimeOffset DroppedAtUtc { get; init; }
    public required DateTimeOffset ReadyToWeighAtUtc { get; init; }
    public required double RemainingCoolingSeconds { get; init; }
    public required RoastEffectiveStatus Status { get; init; }
    public required int TotalSeconds { get; init; }
    public string Notes { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string RoastLevelName { get; init; } = string.Empty;

    public bool IsReadyToWeigh => Status == RoastEffectiveStatus.NeedsWeight;
}

/// <summary>The immutable view ViewModels render. Rebuilt, never mutated.</summary>
public sealed record RoastSessionSnapshot
{
    public required DateTimeOffset AsOfUtc { get; init; }
    public Guid? SessionId { get; init; }
    public required int NextBatchNumber { get; init; }
    public ActiveRoastSnapshot? ActiveRoast { get; init; }
    public required IReadOnlyList<RoastWorkItem> OpenWork { get; init; }

    /// <summary>A persisted draft was found that this process has not yet confirmed with the user.</summary>
    public required bool RequiresRecovery { get; init; }

    public bool HasSession => SessionId.HasValue;
    public bool HasActiveRoast => ActiveRoast is not null;
}

/// <summary>The outcome of one roast transition, always carrying the resulting snapshot.</summary>
public sealed record TransitionResult
{
    public required bool Success { get; init; }
    public RoastTransitionError Error { get; init; }
    public string? Message { get; init; }

    /// <summary>Non-blocking information about work that could not be completed after a saved change.</summary>
    public string? Warning { get; init; }

    public required RoastSessionSnapshot Snapshot { get; init; }

    public static TransitionResult Ok(RoastSessionSnapshot snapshot, string? warning = null) =>
        new()
        {
            Success = true,
            Error = RoastTransitionError.None,
            Snapshot = snapshot,
            Warning = warning
        };

    public static TransitionResult Fail(
        RoastTransitionError error,
        string message,
        RoastSessionSnapshot snapshot) =>
        new()
        {
            Success = false,
            Error = error,
            Message = message,
            Snapshot = snapshot
        };
}

/// <summary>Carry-forward values and the reference result for one bean's setup screen.</summary>
public sealed record RoastSetupSuggestion
{
    public required Guid BeanId { get; init; }
    public double? Temperature { get; init; }
    public double? BatchWeight { get; init; }

    /// <summary>The newest completed roast for this bean; the honest reference result.</summary>
    public RoastData? LastCompletedRoast { get; init; }

    /// <summary>Batches for this bean dropped after <see cref="LastCompletedRoast"/> that still need a weight.</summary>
    public required int NewerAwaitingWeightCount { get; init; }

    public bool HasHistory => Temperature.HasValue && BatchWeight.HasValue;
}
