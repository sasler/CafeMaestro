namespace CafeMaestro.Services;

/// <summary>
/// Schedules and cancels the optional cooling-ready reminder, keyed by roast id.
/// Calls are idempotent and always happen after persistence, so a failure here never
/// rolls back a saved drop.
/// </summary>
public interface ICoolingNotificationService
{
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
    public Task ScheduleCoolingReadyAsync(
        Guid roastId,
        DateTimeOffset readyToWeighAtUtc,
        string beanDisplayName,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CancelAsync(Guid roastId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
