using Android.Content;
using Android.OS;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;

namespace CafeMaestro;

public partial class MainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        QueueCoolingActivation(Intent);
    }

    protected override async void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (!QueueCoolingActivation(intent))
        {
            return;
        }

        try
        {
            IAppActivationService? activation =
                IPlatformApplication.Current?.Services.GetService<IAppActivationService>();
            if (activation is not null)
            {
                await activation.HandlePendingAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warm notification activation failed: {ex.Message}");
        }
    }

    private static bool QueueCoolingActivation(Intent? intent)
    {
        string? rawRoastId = intent?.GetStringExtra(AndroidCoolingNotificationService.ExtraRoastId);
        if (!Guid.TryParse(rawRoastId, out Guid roastId))
        {
            return false;
        }

        IAppActivationService? activation =
            IPlatformApplication.Current?.Services.GetService<IAppActivationService>();
        if (activation is null)
        {
            return false;
        }

        activation.Queue(new AppActivationPayload(
            "cooling-ready",
            new Dictionary<string, string> { ["roastId"] = roastId.ToString() }));
        return true;
    }
}
