using Android.App;
using Android.OS;
using Android.Views;
using Microsoft.Maui.ApplicationModel;

namespace CafeMaestro;

public partial class App
{
    static partial void ApplyPlatformChrome(AppTheme effectiveTheme)
    {
        Activity? activity = Platform.CurrentActivity;
        Android.Views.Window? window = activity?.Window;
        if (activity is null || window is null)
        {
            return;
        }

        activity.RunOnUiThread(() =>
        {
            Android.Graphics.Color statusBarColor = ResolveAndroidColor("PlatformStatusBarColor");
            Android.Graphics.Color navigationBarColor = ResolveAndroidColor("PlatformNavigationBarColor");
            bool useDarkStatusIcons = IsLightResource("PlatformStatusBarColor");
            bool useDarkNavigationIcons = IsLightResource("PlatformNavigationBarColor");

            window.DecorView.SetBackgroundColor(statusBarColor);

            // Android 15+ is edge-to-edge and draws app content behind transparent
            // system bars. Earlier versions still accept explicit bar colours.
            if (!OperatingSystem.IsAndroidVersionAtLeast(35))
            {
                window.SetStatusBarColor(statusBarColor);
                window.SetNavigationBarColor(
                    !OperatingSystem.IsAndroidVersionAtLeast(26) && useDarkNavigationIcons
                        ? statusBarColor
                        : navigationBarColor);
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                WindowInsetsControllerAppearance appearance = WindowInsetsControllerAppearance.None;
                if (useDarkStatusIcons)
                {
                    appearance |= WindowInsetsControllerAppearance.LightStatusBars;
                }
                if (useDarkNavigationIcons)
                {
                    appearance |= WindowInsetsControllerAppearance.LightNavigationBars;
                }
                const WindowInsetsControllerAppearance mask =
                    WindowInsetsControllerAppearance.LightStatusBars
                    | WindowInsetsControllerAppearance.LightNavigationBars;

                window.InsetsController?.SetSystemBarsAppearance((int)appearance, (int)mask);
            }
            else if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                SystemUiFlags flags = window.DecorView.SystemUiFlags;
                flags = useDarkStatusIcons
                    ? flags | SystemUiFlags.LightStatusBar
                    : flags & ~SystemUiFlags.LightStatusBar;
                if (OperatingSystem.IsAndroidVersionAtLeast(26))
                {
                    flags = useDarkNavigationIcons
                        ? flags | SystemUiFlags.LightNavigationBar
                        : flags & ~SystemUiFlags.LightNavigationBar;
                }
                window.DecorView.SystemUiFlags = flags;
            }
        });
    }

    private static Android.Graphics.Color ResolveAndroidColor(string resourceKey)
    {
        if (ResolveSemanticColor(resourceKey) is Microsoft.Maui.Graphics.Color color)
        {
            return Android.Graphics.Color.Argb(
                ToByte(color.Alpha), ToByte(color.Red), ToByte(color.Green), ToByte(color.Blue));
        }

        return Android.Graphics.Color.Transparent;
    }

    private static bool IsLightResource(string resourceKey)
    {
        Microsoft.Maui.Graphics.Color? color = ResolveSemanticColor(resourceKey);
        if (color is null)
        {
            return false;
        }

        double luminance = (0.2126 * color.Red) + (0.7152 * color.Green) + (0.0722 * color.Blue);
        return luminance > 0.5;
    }

    private static Microsoft.Maui.Graphics.Color? ResolveSemanticColor(string resourceKey) =>
        Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(resourceKey, out object? value) == true
            ? value as Microsoft.Maui.Graphics.Color
            : null;

    private static byte ToByte(float channel) => (byte)Math.Round(channel * byte.MaxValue);
}
