using CommunityToolkit.Mvvm.ComponentModel;

namespace CafeMaestro.Models;

public enum RoastPresentationState
{
    Setup,
    Active,
    Handoff,
    Recovery,
    PersistenceError
}

public sealed class RoastChannelPresentation : ObservableObject
{
    private Guid _roastId;
    private string _batchLabel = string.Empty;
    private string _beanDisplaySnapshot = string.Empty;
    private string _statusLabel = string.Empty;
    private string _timeDisplay = string.Empty;
    private string _coolingRemainingLabel = string.Empty;
    private double _coolingProgress;
    private bool _isReady;

    public Guid RoastId
    {
        get => _roastId;
        set => SetProperty(ref _roastId, value);
    }

    public string BatchLabel
    {
        get => _batchLabel;
        set
        {
            if (SetProperty(ref _batchLabel, value))
            {
                NotifySemanticPropertiesChanged();
            }
        }
    }

    public string BeanDisplaySnapshot
    {
        get => _beanDisplaySnapshot;
        set
        {
            if (SetProperty(ref _beanDisplaySnapshot, value))
            {
                NotifySemanticPropertiesChanged();
            }
        }
    }

    public string StatusLabel
    {
        get => _statusLabel;
        set
        {
            if (SetProperty(ref _statusLabel, value))
            {
                NotifySemanticPropertiesChanged();
            }
        }
    }

    public string TimeDisplay
    {
        get => _timeDisplay;
        set => SetProperty(ref _timeDisplay, value);
    }

    /// <summary>
    /// Remaining cooling time rounded to whole minutes, for the screen reader only. The visible
    /// countdown lives in <see cref="TimeDisplay"/> and changes every second; announcing that
    /// would make TalkBack re-read a focused card continuously, so this coarse value is what the
    /// semantic description carries. Assigning the same bucket raises no notification, which is
    /// what keeps announcements down to roughly one a minute.
    /// </summary>
    public string CoolingRemainingLabel
    {
        get => _coolingRemainingLabel;
        set
        {
            if (SetProperty(ref _coolingRemainingLabel, value))
            {
                NotifySemanticPropertiesChanged();
            }
        }
    }

    public double CoolingProgress
    {
        get => _coolingProgress;
        set => SetProperty(ref _coolingProgress, value);
    }

    public bool IsReady
    {
        get => _isReady;
        set
        {
            if (SetProperty(ref _isReady, value))
            {
                OnPropertyChanged(nameof(CanCompleteCooling));
                NotifySemanticPropertiesChanged();
            }
        }
    }

    /// <summary>Whether this batch can still be released from cooling early.</summary>
    public bool CanCompleteCooling => !IsReady;

    public string SemanticDescription => string.IsNullOrEmpty(CoolingRemainingLabel)
        ? $"{BatchLabel}, {BeanDisplaySnapshot}, {StatusLabel}."
        : $"{BatchLabel}, {BeanDisplaySnapshot}, {StatusLabel}, {CoolingRemainingLabel}.";

    public string CompleteCoolingSemanticDescription =>
        $"Ready now. End cooling for {BatchLabel}, {BeanDisplaySnapshot}.";

    /// <summary>
    /// Buckets remaining cooling seconds to whole minutes. Returns an empty label once the batch
    /// is ready, so a ready card announces its status without a stale duration trailing it.
    /// </summary>
    public static string DescribeCoolingRemaining(double remainingSeconds, bool isReady)
    {
        if (isReady || !double.IsFinite(remainingSeconds) || remainingSeconds <= 0)
        {
            return string.Empty;
        }

        int minutes = (int)Math.Ceiling(remainingSeconds / 60d);
        return minutes <= 1 ? "under a minute left" : $"about {minutes} minutes left";
    }

    private void NotifySemanticPropertiesChanged()
    {
        OnPropertyChanged(nameof(SemanticDescription));
        OnPropertyChanged(nameof(CompleteCoolingSemanticDescription));
    }
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
