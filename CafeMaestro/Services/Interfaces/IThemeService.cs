namespace CafeMaestro.Services;

/// <summary>Applies a stored appearance choice to the live MAUI application.</summary>
public interface IThemeService
{
    Task ApplyAsync(ThemePreference preference);
}
