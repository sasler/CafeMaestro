namespace CafeMaestro.Services;

/// <summary>
/// Constructor compatibility fallback for consumers that create RoastDataService outside the app
/// container. Notification rescheduling stays disabled unless the real preference service is
/// supplied by DI.
/// </summary>
internal sealed class DisabledRoastPreferencesService : IRoastPreferencesService
{
    public Task<int> GetCoolingDurationSecondsAsync() =>
        Task.FromResult(RoastPreferenceDefaults.CoolingDurationSeconds);

    public Task<bool> SetCoolingDurationSecondsAsync(int seconds) => Task.FromResult(false);

    public Task<bool> GetFirstCrackEnabledAsync() =>
        Task.FromResult(RoastPreferenceDefaults.FirstCrackEnabled);

    public Task<bool> SetFirstCrackEnabledAsync(bool enabled) => Task.FromResult(false);

    public Task<bool> GetCoolingNotificationsEnabledAsync() =>
        Task.FromResult(RoastPreferenceDefaults.CoolingNotificationsEnabled);

    public Task<bool> SetCoolingNotificationsEnabledAsync(bool enabled) => Task.FromResult(false);
}
