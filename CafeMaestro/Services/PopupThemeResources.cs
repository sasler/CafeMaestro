using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace CafeMaestro.Services;

/// <summary>
/// Resolves popup-only theme resources at the point they are consumed.
/// </summary>
internal static class PopupThemeResources
{
    internal const string TransparentColorKey = "TransparentColor";

    internal static Color ResolveTransparentColor()
    {
        Color? themedColor = TryGetColor(Application.Current?.Resources, TransparentColorKey);
        if (themedColor is not null)
        {
            return themedColor;
        }

        // MauiProgram configures the toolkit before App.InitializeComponent has loaded its
        // dictionaries. The bootstrap value keeps the host transparent; popup options resolve
        // the active semantic resource again once the application exists.
        return Colors.Transparent;
    }

    internal static Color? TryGetColor(ResourceDictionary? resources, string key) =>
        resources?.TryGetValue(key, out object? value) == true && value is Color color
            ? color
            : null;
}
