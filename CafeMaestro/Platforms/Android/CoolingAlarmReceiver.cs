using Android.App;
using Android.Content;
using Android.OS;

namespace CafeMaestro.Services;

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter([AndroidCoolingNotificationService.ActionShowCoolingReady])]
public sealed class CoolingAlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null ||
            !Guid.TryParse(intent.GetStringExtra(AndroidCoolingNotificationService.ExtraRoastId), out Guid roastId))
        {
            return;
        }

        Intent openIntent = new(context, typeof(MainActivity));
        openIntent.SetAction(Intent.ActionView);
        openIntent.SetData(Android.Net.Uri.Parse($"cafemaestro://cooling/{roastId:D}"));
        openIntent.PutExtra(AndroidCoolingNotificationService.ExtraRoastId, roastId.ToString());
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        PendingIntentFlags pendingFlags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            pendingFlags |= PendingIntentFlags.Immutable;
        }
        PendingIntent? contentIntent = PendingIntent.GetActivity(
            context,
            0,
            openIntent,
            pendingFlags);

        string bean = intent.GetStringExtra(AndroidCoolingNotificationService.ExtraBeanName) ?? "Your batch";
        int batchNumber = intent.GetIntExtra(AndroidCoolingNotificationService.ExtraBatchNumber, 0);
        string title = batchNumber > 0 ? $"Batch {batchNumber} is ready to weigh" : "Batch ready to weigh";
        Notification.Builder builder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new Notification.Builder(context, AndroidCoolingNotificationService.ChannelId)
            : new Notification.Builder(context);
        Notification notification = builder
            .SetContentTitle(title)
            .SetContentText($"{bean} has finished its cooling window.")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentIntent(contentIntent)
            .SetAutoCancel(true)
            .Build();
        NotificationManager? manager =
            (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.Notify(roastId.GetHashCode(), notification);
    }
}
