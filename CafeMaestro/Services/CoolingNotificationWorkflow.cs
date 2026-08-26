using CafeMaestro.Models;
using Microsoft.Maui.Storage;

namespace CafeMaestro.Services;

/// <summary>
/// Cross-platform policy around the native scheduler. Persisted roast data remains authoritative;
/// reminders are rebuilt from it and every platform failure is best effort.
/// </summary>
public sealed class CoolingNotificationWorkflow : ICoolingNotificationWorkflow
{
    private const string FirstDropPromptSeenKey = "CoolingNotificationFirstDropPromptSeen";

    private readonly IAppDataService _appDataService;
    private readonly IRoastPreferencesService _roastPreferences;
    private readonly ICoolingNotificationService _notifications;
    private readonly IAlertService _alerts;
    private readonly IPreferences _preferences;

    public CoolingNotificationWorkflow(
        IAppDataService appDataService,
        IRoastPreferencesService roastPreferences,
        ICoolingNotificationService notifications,
        IAlertService alerts,
        IPreferences preferences)
    {
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _roastPreferences = roastPreferences ?? throw new ArgumentNullException(nameof(roastPreferences));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _notifications.CancelAllAsync(cancellationToken);
            if (!await _roastPreferences.GetCoolingNotificationsEnabledAsync() ||
                await _notifications.GetPermissionStateAsync(cancellationToken) !=
                    CoolingNotificationPermissionState.Granted)
            {
                return;
            }

            foreach (RoastData roast in _appDataService.CurrentData.RoastLogs
                         .Where(candidate => candidate.CompletionStatus == RoastCompletionStatus.AwaitingWeight))
            {
                if (roast.ReadyToWeighAtUtc is not DateTimeOffset readyAt)
                {
                    continue;
                }

                await _notifications.ScheduleCoolingReadyAsync(
                    roast.Id,
                    readyAt,
                    roast.BeanDisplaySnapshot,
                    roast.BatchNumber,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cooling reminder reconciliation failed: {ex.Message}");
        }
    }

    public async Task AfterSuccessfulDropAsync(
        RoastWorkItem droppedRoast,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(droppedRoast);

        try
        {
            CoolingNotificationPermissionState state =
                await _notifications.GetPermissionStateAsync(cancellationToken);
            if (state == CoolingNotificationPermissionState.Unavailable)
            {
                return;
            }

            bool enabled = await _roastPreferences.GetCoolingNotificationsEnabledAsync();
            if (_preferences.Get(FirstDropPromptSeenKey, false))
            {
                if (enabled && state == CoolingNotificationPermissionState.Granted)
                {
                    await ScheduleAsync(droppedRoast, cancellationToken);
                }
                return;
            }

            // Record presentation before showing UI. A dismissal or denial is a normal, final
            // first-drop outcome and must not become a recurring nag.
            _preferences.Set(FirstDropPromptSeenKey, true);
            bool accepted = await _alerts.ShowConfirmationAsync(
                "Cooling reminder?",
                "CafeMaestro can send a best-effort reminder when this batch is ready to weigh. Android may deliver it late.",
                "Enable reminder",
                "Not now");
            if (!accepted || (!enabled && !await _roastPreferences.SetCoolingNotificationsEnabledAsync(true)))
            {
                return;
            }

            state = state == CoolingNotificationPermissionState.NotDetermined
                ? await _notifications.RequestPermissionAsync(cancellationToken)
                : state;
            if (state == CoolingNotificationPermissionState.Granted)
            {
                await ScheduleAsync(droppedRoast, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cooling reminder onboarding failed: {ex.Message}");
        }
    }

    private Task ScheduleAsync(RoastWorkItem roast, CancellationToken cancellationToken) =>
        _notifications.ScheduleCoolingReadyAsync(
            roast.RoastId,
            roast.ReadyToWeighAtUtc,
            roast.BeanDisplaySnapshot,
            roast.BatchNumber,
            cancellationToken);
}
