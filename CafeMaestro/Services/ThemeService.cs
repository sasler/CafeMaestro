using Microsoft.Maui.ApplicationModel;
using MauiAppTheme = Microsoft.Maui.ApplicationModel.AppTheme;

namespace CafeMaestro.Services;

public sealed class ThemeService : IThemeService
{
    public Task ApplyAsync(ThemePreference preference) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        if (Application.Current is not App app)
        {
            return;
        }

        app.UserAppTheme = preference switch
        {
            ThemePreference.Light => MauiAppTheme.Light,
            ThemePreference.Dark => MauiAppTheme.Dark,
            _ => MauiAppTheme.Unspecified
        };
        app.SetTheme(preference.ToString());
    });
}
