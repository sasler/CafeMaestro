namespace CafeMaestro.Services;

/// <summary>
/// The delivery state of cooling reminders, kept separate from the app preference so a
/// settings screen can say why a reminder will not arrive.
/// </summary>
public enum CoolingNotificationPermissionState
{
    /// <summary>This platform/build cannot post cooling reminders at all.</summary>
    Unavailable,

    /// <summary>Notifications are supported and no OS permission is required.</summary>
    Granted,

    /// <summary>Supported, but the OS has not been asked yet.</summary>
    NotDetermined,

    /// <summary>Supported, and the user declined the OS permission. Not an error.</summary>
    Denied
}

/// <summary>
/// Schedules and cancels the optional cooling-ready reminder, keyed by roast id.
/// Calls are idempotent and always happen after persistence, so a failure here never
/// rolls back a saved drop.
/// </summary>
public interface ICoolingNotificationService
{
    /// <summary>
    /// The current OS-level delivery state. Never throws; an unusable platform reports
    /// <see cref="CoolingNotificationPermissionState.Unavailable"/>.
    /// </summary>
    Task<CoolingNotificationPermissionState> GetPermissionStateAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prompts for the OS permission when one is needed and returns the resulting state.
    /// A denial is a normal outcome, not a failure.
    /// </summary>
    Task<CoolingNotificationPermissionState> RequestPermissionAsync(
        CancellationToken cancellationToken = default);

    Task ScheduleCoolingReadyAsync(
        Guid roastId,
        DateTimeOffset readyToWeighAtUtc,
        string beanDisplayName,
        CancellationToken cancellationToken = default);

    Task CancelAsync(Guid roastId, CancellationToken cancellationToken = default);
}

/// <summary>The cross-platform default until the Android implementation lands.</summary>
public sealed class NoOpCoolingNotificationService : ICoolingNotificationService
{
    public Task<CoolingNotificationPermissionState> GetPermissionStateAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CoolingNotificationPermissionState.Unavailable);

    public Task<CoolingNotificationPermissionState> RequestPermissionAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CoolingNotificationPermissionState.Unavailable);

    public Task ScheduleCoolingReadyAsync(
        Guid roastId,
        DateTimeOffset readyToWeighAtUtc,
        string beanDisplayName,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CancelAsync(Guid roastId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
