using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace CafeMaestro.Services;

public sealed class AndroidCoolingNotificationService : ICoolingNotificationService
{
    internal const string ChannelId = "cooling-reminders";
    internal const string ActionShowCoolingReady = "cafemaestro.action.SHOW_COOLING_READY";
    internal const string ExtraRoastId = "roastId";
    internal const string ExtraBeanName = "beanName";
    internal const string ExtraBatchNumber = "batchNumber";
    internal const int NotificationId = 0;
    private const string PermissionRequestedKey = "CoolingNotificationPermissionRequested";
    private const string ScheduledIdsKey = "CoolingNotificationScheduledIds";

    private readonly Context _context;
    private readonly IPreferences _preferences;
    private readonly SemaphoreSlim _registryGate = new(1, 1);

    public AndroidCoolingNotificationService(IPreferences preferences)
    {
        _context = Android.App.Application.Context;
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        EnsureChannel();
    }

    public Task<CoolingNotificationPermissionState> GetPermissionStateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            _context.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) !=
                Permission.Granted)
        {
            return Task.FromResult(_preferences.Get(PermissionRequestedKey, false)
                ? CoolingNotificationPermissionState.Denied
                : CoolingNotificationPermissionState.NotDetermined);
        }

        NotificationManager? manager =
            (NotificationManager?)_context.GetSystemService(Context.NotificationService);
        bool enabled = !OperatingSystem.IsAndroidVersionAtLeast(24) ||
            manager?.AreNotificationsEnabled() == true;
        return Task.FromResult(enabled
            ? CoolingNotificationPermissionState.Granted
            : CoolingNotificationPermissionState.Denied);
    }

    public async Task<CoolingNotificationPermissionState> RequestPermissionAsync(
        CancellationToken cancellationToken = default)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
        {
            return await GetPermissionStateAsync(cancellationToken);
        }

        try
        {
            _preferences.Set(PermissionRequestedKey, true);
            PermissionStatus status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            return status == PermissionStatus.Granted
                ? CoolingNotificationPermissionState.Granted
                : CoolingNotificationPermissionState.Denied;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification permission request failed: {ex.Message}");
            return CoolingNotificationPermissionState.Denied;
        }
    }

    public Task ScheduleCoolingReadyAsync(
        Guid roastId,
        DateTimeOffset readyToWeighAtUtc,
        string beanDisplayName,
        CancellationToken cancellationToken = default) =>
        ScheduleCoolingReadyAsync(
            roastId, readyToWeighAtUtc, beanDisplayName, null, cancellationToken);

    public async Task ScheduleCoolingReadyAsync(
        Guid roastId,
        DateTimeOffset readyToWeighAtUtc,
        string beanDisplayName,
        int? batchNumber,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await GetPermissionStateAsync(cancellationToken) != CoolingNotificationPermissionState.Granted)
        {
            return;
        }

        await _registryGate.WaitAsync(cancellationToken);
        try
        {
            // Re-read the app-level opt-in while holding the same gate as native scheduling. This
            // closes the disable-vs-in-flight-schedule race: a schedule that starts after the
            // preference write observes the disabled state and cannot recreate an orphaned alarm.
            if (!IsAppOptedIn())
            {
                return;
            }

            AlarmManager? alarms = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
            PendingIntent? pendingIntent = CreateAlarmPendingIntent(
                roastId, beanDisplayName, batchNumber, PendingIntentFlags.UpdateCurrent);
            if (alarms is null || pendingIntent is null)
            {
                return;
            }

            // Record the durable identity before touching AlarmManager. If the process dies after
            // this point, startup/disable reconciliation can still discover and cancel the alarm.
            Remember(roastId);

            long triggerAtMillis = Math.Max(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                readyToWeighAtUtc.ToUnixTimeMilliseconds());
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            }
            else
            {
                alarms.Set(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            }
        }
        finally
        {
            _registryGate.Release();
        }
    }

    public async Task CancelAsync(Guid roastId, CancellationToken cancellationToken = default)
    {
        await _registryGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancelCore(roastId);
        }
        finally
        {
            _registryGate.Release();
        }
    }

    public async Task CancelAllAsync(CancellationToken cancellationToken = default)
    {
        await _registryGate.WaitAsync(cancellationToken);
        try
        {
            Exception? firstFailure = null;
            foreach (Guid roastId in ReadScheduledIds().ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    CancelCore(roastId);
                }
                catch (Exception ex)
                {
                    firstFailure ??= ex;
                }
            }

            if (firstFailure is not null)
            {
                throw firstFailure;
            }
        }
        finally
        {
            _registryGate.Release();
        }
    }

    private void CancelCore(Guid roastId)
    {
        AlarmManager? alarms = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
        PendingIntent? pendingIntent = CreateAlarmPendingIntent(
            roastId, null, null, PendingIntentFlags.NoCreate);
        if (pendingIntent is not null)
        {
            alarms?.Cancel(pendingIntent);
            pendingIntent.Cancel();
        }

        NotificationManager? manager =
            (NotificationManager?)_context.GetSystemService(Context.NotificationService);
        if (manager is null)
        {
            throw new InvalidOperationException("Android notification manager is unavailable.");
        }

        // The tag/id pair is stable across process launches. Also clear the pre-review hash-only
        // form once, so an upgrade cannot leave a previously delivered reminder in the tray.
        manager.Cancel(NotificationTag(roastId), NotificationId);
        manager.Cancel(roastId.GetHashCode());
        Forget(roastId);
    }

    private PendingIntent? CreateAlarmPendingIntent(
        Guid roastId,
        string? beanDisplayName,
        int? batchNumber,
        PendingIntentFlags flags)
    {
        Intent intent = new(_context, typeof(CoolingAlarmReceiver));
        intent.SetAction(ActionShowCoolingReady);
        intent.SetData(Android.Net.Uri.Parse($"cafemaestro://cooling/{roastId:D}"));
        intent.PutExtra(ExtraRoastId, roastId.ToString());
        if (beanDisplayName is not null)
        {
            intent.PutExtra(ExtraBeanName, beanDisplayName);
        }
        if (batchNumber.HasValue)
        {
            intent.PutExtra(ExtraBatchNumber, batchNumber.Value);
        }
        return PendingIntent.GetBroadcast(
            _context,
            0,
            intent,
            flags | (OperatingSystem.IsAndroidVersionAtLeast(23)
                ? PendingIntentFlags.Immutable
                : 0));
    }

    private void EnsureChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }
        NotificationManager? manager =
            (NotificationManager?)_context.GetSystemService(Context.NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            ChannelId,
            "Cooling reminders",
            NotificationImportance.Low)
        {
            Description = "Best-effort reminders when a roasted batch is ready to weigh."
        });
    }

    private HashSet<Guid> ReadScheduledIds() => (_preferences.Get(ScheduledIdsKey, string.Empty) ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(value => Guid.TryParse(value, out Guid id) ? id : Guid.Empty)
        .Where(id => id != Guid.Empty)
        .ToHashSet();

    private void Remember(Guid roastId)
    {
        HashSet<Guid> ids = ReadScheduledIds();
        ids.Add(roastId);
        WriteScheduledIds(ids);
    }

    private void Forget(Guid roastId)
    {
        HashSet<Guid> ids = ReadScheduledIds();
        ids.Remove(roastId);
        WriteScheduledIds(ids);
    }

    private void WriteScheduledIds(IEnumerable<Guid> ids) =>
        _preferences.Set(ScheduledIdsKey, string.Join(',', ids.Select(id => id.ToString("D"))));

    private bool IsAppOptedIn()
    {
        try
        {
            return _preferences.Get(
                RoastPreferenceDefaults.CoolingNotificationsPreferenceKey,
                RoastPreferenceDefaults.CoolingNotificationsEnabled);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cooling notification preference read failed: {ex.Message}");
            return false;
        }
    }

    internal static string NotificationTag(Guid roastId) => roastId.ToString("D");
}
