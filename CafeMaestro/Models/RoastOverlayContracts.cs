namespace CafeMaestro.Models;

/// <summary>
/// Identity of the batch a weigh-in acts on. Passed in whole so the sheet can keep the batch
/// visible at all times and never has to look a roast up again.
/// </summary>
public sealed record WeighInRequest
{
    public required Guid RoastId { get; init; }
    public int? BatchNumber { get; init; }
    public required string BeanDisplaySnapshot { get; init; }
    public required double BatchWeight { get; init; }
    public required DateTimeOffset DroppedAtUtc { get; init; }
    public required int TotalSeconds { get; init; }

    /// <summary>Existing result when the focused sheet is editing a completed roast.</summary>
    public double? InitialFinalWeight { get; init; }

    /// <summary>True when another batch still needs a weight after this one is resolved.</summary>
    public bool HasAnotherBatchWaiting { get; init; }
}

public enum WeighInOutcomeKind
{
    Cancelled,
    Saved,
    MarkedUnweighed
}

public sealed record WeighInOutcome(WeighInOutcomeKind Kind, double? FinalWeight = null)
{
    public static readonly WeighInOutcome Cancelled = new(WeighInOutcomeKind.Cancelled);
}

/// <summary>One selectable batch in the "which batch is this?" sheet.</summary>
public sealed record BatchChoice
{
    public required Guid RoastId { get; init; }
    public int? BatchNumber { get; init; }
    public required string BeanDisplaySnapshot { get; init; }
    public required double BatchWeight { get; init; }
    public required DateTimeOffset DroppedAtUtc { get; init; }
    public required int TotalSeconds { get; init; }
}

public sealed record DiscardRequest
{
    public required string BeanDisplaySnapshot { get; init; }
    public required int BatchNumber { get; init; }
    public required string ElapsedDisplay { get; init; }
}

public enum DiscardOutcomeKind
{
    Cancelled,
    Discard,
    KeepLog
}

/// <summary>
/// What the discard sheet decided. <see cref="BeansWereUsed"/> answers the inventory question;
/// <see cref="DiscardOutcomeKind.KeepLog"/> preserves the failed roast as a Discarded record.
/// </summary>
public sealed record DiscardOutcome(DiscardOutcomeKind Kind, bool BeansWereUsed = true)
{
    public static readonly DiscardOutcome Cancelled = new(DiscardOutcomeKind.Cancelled);

    public bool ShouldDiscard => Kind is DiscardOutcomeKind.Discard or DiscardOutcomeKind.KeepLog;

    public bool KeepLog => Kind == DiscardOutcomeKind.KeepLog;
}

/// <summary>The answer to "you are still roasting" when the user tries to navigate away.</summary>
public enum NavigationChoice
{
    KeepRoasting,
    DiscardBatch
}
