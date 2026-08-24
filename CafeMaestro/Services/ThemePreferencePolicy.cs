using System;
using Microsoft.Maui.ApplicationModel;

namespace CafeMaestro.Services;

/// <summary>
/// Resolves the persisted theme value into a <see cref="ThemePreference"/>.
/// </summary>
/// <remarks>
/// Dark is the fallback for installs that have never expressed a preference. Any
/// explicit choice already on the device - including <see cref="ThemePreference.System"/> -
/// is preserved, so the redesign never silently overrides a decision the user made.
/// </remarks>
public static class ThemePreferencePolicy
{
    /// <summary>The mode used when nothing usable has been stored.</summary>
    public const ThemePreference Fallback = ThemePreference.Dark;

    public static ThemePreference FromStoredValue(string? storedValue) =>
        Enum.TryParse(storedValue, ignoreCase: true, out ThemePreference preference)
        && Enum.IsDefined(preference)
            ? preference
            : Fallback;

    /// <summary>
    /// Resolves the semantic dictionary that should be active right now. Explicit
    /// Light/Dark choices ignore system changes; System follows every requested-theme
    /// event. Unspecified system state uses Light, matching MAUI's neutral fallback.
    /// </summary>
    public static AppTheme ResolveEffectiveTheme(ThemePreference preference, AppTheme requestedTheme) =>
        preference switch
        {
            ThemePreference.Light => AppTheme.Light,
            ThemePreference.Dark => AppTheme.Dark,
            _ => requestedTheme == AppTheme.Dark ? AppTheme.Dark : AppTheme.Light
        };
}
