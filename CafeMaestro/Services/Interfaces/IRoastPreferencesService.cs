namespace CafeMaestro.Services;

/// <summary>
/// Roasting preferences. Values are snapshotted into a roast when it starts or drops, so
/// changing a preference never rewrites an active draft or a historical roast.
/// </summary>
public interface IRoastPreferencesService
{
    /// <summary>Cooling window applied to a new roast. Defaults to five minutes.</summary>
    Task<int> GetCoolingDurationSecondsAsync();

    /// <summary>
    /// Returns false when the value could not be stored, so a settings screen can restore the
    /// previous choice rather than display a preference that was never persisted.
    /// </summary>
    Task<bool> SetCoolingDurationSecondsAsync(int seconds);

    /// <summary>Whether First Crack tracking is offered. Off by default.</summary>
    Task<bool> GetFirstCrackEnabledAsync();

    /// <inheritdoc cref="SetCoolingDurationSecondsAsync"/>
    Task<bool> SetFirstCrackEnabledAsync(bool enabled);

    /// <summary>Whether an Android cooling-ready notification should be scheduled after a drop.</summary>
    Task<bool> GetCoolingNotificationsEnabledAsync();

    /// <inheritdoc cref="SetCoolingDurationSecondsAsync"/>
    Task<bool> SetCoolingNotificationsEnabledAsync(bool enabled);
}

public static class RoastPreferenceDefaults
{
    /// <summary>Five minutes, per the settled operating parameters.</summary>
    public const int CoolingDurationSeconds = 300;

    /// <summary>Batch and final weights are captured to 0.1 g.</summary>
    public const double WeightPrecisionGrams = 0.1;

    public const bool FirstCrackEnabled = false;
    public const bool CoolingNotificationsEnabled = false;

    public const int MinimumCoolingDurationSeconds = 0;
    public const int MaximumCoolingDurationSeconds = 3600;

    /// <summary>Rounds a weight in grams to the configured 0.1 g precision.</summary>
    public static double NormalizeGrams(double grams) =>
        Math.Round(grams, 1, MidpointRounding.AwayFromZero);
}
