using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

/// <summary>
/// System/Light/Dark selection. An explicit choice already on the device is loaded as-is;
/// only a device that has never stored one falls back to Dark, which
/// <see cref="ThemePreferencePolicy"/> owns.
/// </summary>
public partial class AppearanceSettingsPageViewModel : ObservableObject
{
    private readonly IPreferencesService _preferencesService;
    private readonly IThemeService _themeService;
    private bool _isLoading;
    private bool _isInitialized;

    public AppearanceSettingsPageViewModel(
        IPreferencesService preferencesService,
        IThemeService themeService)
    {
        _preferencesService = preferencesService ??
                              throw new ArgumentNullException(nameof(preferencesService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
    }

    [ObservableProperty]
    public partial ThemePreference SelectedTheme { get; set; } = ThemePreferencePolicy.Fallback;

    public bool IsSystemSelected => SelectedTheme == ThemePreference.System;

    public bool IsLightSelected => SelectedTheme == ThemePreference.Light;

    public bool IsDarkSelected => SelectedTheme == ThemePreference.Dark;

    public string SelectedThemeDisplay => DescribeTheme(SelectedTheme);

    public string SelectedThemeDetail => SelectedTheme switch
    {
        ThemePreference.System => "CafeMaestro follows your device's light and dark setting.",
        ThemePreference.Light => "Light stays selected regardless of the device setting.",
        _ => "Dark stays selected regardless of the device setting."
    };

    public async Task OnAppearingAsync()
    {
        _isLoading = true;
        try
        {
            SelectedTheme = await _preferencesService.GetThemePreferenceAsync();
        }
        finally
        {
            _isInitialized = true;
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectThemeAsync(ThemePreference theme)
    {
        if (theme == SelectedTheme && _isInitialized)
        {
            return;
        }

        SelectedTheme = theme;
        await ApplyThemeAsync(theme);
    }

    partial void OnSelectedThemeChanged(ThemePreference value)
    {
        OnPropertyChanged(nameof(IsSystemSelected));
        OnPropertyChanged(nameof(IsLightSelected));
        OnPropertyChanged(nameof(IsDarkSelected));
        OnPropertyChanged(nameof(SelectedThemeDisplay));
        OnPropertyChanged(nameof(SelectedThemeDetail));
    }

    /// <summary>Human-readable label shared with the Settings index summary.</summary>
    public static string DescribeTheme(ThemePreference theme) => theme switch
    {
        ThemePreference.Light => "Light",
        ThemePreference.Dark => "Dark",
        _ => "System"
    };

    private async Task ApplyThemeAsync(ThemePreference theme)
    {
        if (_isLoading)
        {
            return;
        }

        await _preferencesService.SaveThemePreferenceAsync(theme);
        await _themeService.ApplyAsync(theme);
    }
}
