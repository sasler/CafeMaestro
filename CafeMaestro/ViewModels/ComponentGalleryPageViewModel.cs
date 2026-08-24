using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

/// <summary>
/// Backs the debug-only component gallery.
/// </summary>
/// <remarks>
/// The gallery is a review harness for the shared visual system, not a product surface.
/// It is registered only in Debug builds so it can never appear in a release shell.
/// </remarks>
public partial class ComponentGalleryPageViewModel : ObservableObject
{
    private readonly IPreferencesService _preferencesService;

    // A partial property rather than a field: the enum-typed field form trips
    // MVVMTK0045 on the WinUI target.
    [ObservableProperty]
    public partial ThemePreference SelectedTheme { get; set; }

    public ComponentGalleryPageViewModel(IPreferencesService preferencesService)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        SelectedTheme = ThemePreferencePolicy.Fallback;
    }

    public async Task LoadAsync()
    {
        SelectedTheme = await _preferencesService.GetThemePreferenceAsync();
    }

    [RelayCommand]
    private async Task SelectThemeAsync(string themeName)
    {
        if (!Enum.TryParse(themeName, ignoreCase: true, out ThemePreference preference))
        {
            return;
        }

        SelectedTheme = preference;
        await _preferencesService.SaveThemePreferenceAsync(preference);
    }
}
