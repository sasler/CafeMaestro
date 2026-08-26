using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>
/// Keeps the public five-argument RoastSessionService constructor usable by focused domain tests
/// and older embedders. The app's DI path supplies the full policy workflow instead.
/// </summary>
internal sealed class LegacyCoolingNotificationWorkflow : ICoolingNotificationWorkflow
{
    private readonly IRoastPreferencesService _roastPreferences;
    private readonly ICoolingNotificationService _notifications;

    public LegacyCoolingNotificationWorkflow(
        IRoastPreferencesService roastPreferences,
        ICoolingNotificationService notifications)
    {
        _roastPreferences = roastPreferences ?? throw new ArgumentNullException(nameof(roastPreferences));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
    }

    public Task ReconcileAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<string?> HandleSuccessfulDropAsync(
        RoastData droppedRoast,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(droppedRoast);
        if (!await _roastPreferences.GetCoolingNotificationsEnabledAsync())
        {
            return null;
        }

        try
        {
            DateTimeOffset? readyAt = droppedRoast.ReadyToWeighAtUtc;
            if (!readyAt.HasValue)
            {
                return null;
            }

            await _notifications.ScheduleCoolingReadyAsync(
                droppedRoast.Id,
                readyAt.Value,
                droppedRoast.BeanDisplaySnapshot,
                droppedRoast.BatchNumber,
                cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cooling notification could not be scheduled: {ex.Message}");
            return "The roast is saved. A cooling reminder could not be scheduled.";
        }
    }

    public async Task CancelAsync(Guid roastId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _notifications.CancelAsync(roastId, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cooling notification could not be cancelled: {ex.Message}");
        }
    }
}
