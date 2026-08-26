using Microsoft.Maui.Storage;

namespace CafeMaestro.Services;

/// <summary>
/// Roasting preferences backed by platform preferences. These are settings, not secrets, so
/// they live alongside the other lightweight values rather than in secure storage.
/// </summary>
public sealed class RoastPreferencesService : IRoastPreferencesService
{
    private const string CoolingDurationKey = "RoastCoolingDurationSeconds";
    private const string FirstCrackEnabledKey = "RoastFirstCrackEnabled";
    private const string CoolingNotificationsKey =
        RoastPreferenceDefaults.CoolingNotificationsPreferenceKey;

    private readonly IPreferences _preferences;

    public RoastPreferencesService()
        : this(Preferences.Default)
    {
    }

    public RoastPreferencesService(IPreferences preferences)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    public Task<int> GetCoolingDurationSecondsAsync()
    {
        int stored = Get(
            CoolingDurationKey,
            RoastPreferenceDefaults.CoolingDurationSeconds);
        return Task.FromResult(ClampCoolingDuration(stored));
    }

    public Task<bool> SetCoolingDurationSecondsAsync(int seconds) =>
        Task.FromResult(Set(CoolingDurationKey, ClampCoolingDuration(seconds)));

    public Task<bool> GetFirstCrackEnabledAsync() =>
        Task.FromResult(Get(FirstCrackEnabledKey, RoastPreferenceDefaults.FirstCrackEnabled));

    public Task<bool> SetFirstCrackEnabledAsync(bool enabled) =>
        Task.FromResult(Set(FirstCrackEnabledKey, enabled));

    public Task<bool> GetCoolingNotificationsEnabledAsync() =>
        Task.FromResult(Get(
            CoolingNotificationsKey,
            RoastPreferenceDefaults.CoolingNotificationsEnabled));

    public Task<bool> SetCoolingNotificationsEnabledAsync(bool enabled) =>
        Task.FromResult(Set(CoolingNotificationsKey, enabled));

    private static int ClampCoolingDuration(int seconds) => Math.Clamp(
        seconds,
        RoastPreferenceDefaults.MinimumCoolingDurationSeconds,
        RoastPreferenceDefaults.MaximumCoolingDurationSeconds);

    private T Get<T>(string key, T defaultValue)
    {
        try
        {
            return _preferences.Get(key, defaultValue);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to read preference '{key}': {ex.Message}");
            return defaultValue;
        }
    }

    private bool Set<T>(string key, T value)
    {
        try
        {
            _preferences.Set(key, value);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to write preference '{key}': {ex.Message}");
            return false;
        }
    }
}
