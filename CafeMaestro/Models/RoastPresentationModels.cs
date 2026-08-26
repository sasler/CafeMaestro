using System.Collections.ObjectModel;

namespace CafeMaestro.Models;

public enum RoastPresentationState
{
    Setup,
    Active,
    Handoff,
    Recovery,
    PersistenceError
}

public sealed record RoastChannelPresentation
{
    public required Guid RoastId { get; init; }
    public required string BatchLabel { get; init; }
    public required string BeanDisplaySnapshot { get; init; }
    public required string StatusLabel { get; init; }
    public required string TimeDisplay { get; init; }
    public required double CoolingProgress { get; init; }
    public required bool IsReady { get; init; }

    public string SemanticDescription =>
        $"{BatchLabel}, {BeanDisplaySnapshot}, {StatusLabel}, {TimeDisplay}.";
}

/// <summary>
/// The exact user-visible instant captured when Drop is pressed. A persistence retry reuses
/// both values so waiting on the error screen cannot extend the roast or move its cooling anchor.
/// </summary>
public sealed record DropProposal(DateTimeOffset DroppedAtUtc, double ElapsedSeconds);

public sealed record BatchChoiceOutcome(BatchChoice? Choice)
{
    public static readonly BatchChoiceOutcome Cancelled = new((BatchChoice?)null);
}

public sealed record ConfirmationOutcome(bool Confirmed);

public sealed record DropTimeCorrectionOutcome(DateTimeOffset? CorrectedDropUtc);

public sealed record TimeCorrectionRequest
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required int CurrentSeconds { get; init; }
    public required int MaximumSeconds { get; init; }
}

public sealed record TimeCorrectionOutcome(int? Seconds)
{
    public static readonly TimeCorrectionOutcome Cancelled = new((int?)null);
}
