using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace CafeMaestro;

public partial class App : Application
{
    private readonly IAppDataService _appDataService;
    private readonly IPreferencesService _preferencesService;
    private readonly IServiceProvider _serviceProvider;
    private ThemePreference _activeThemePreference = ThemePreference.Dark;
    private Models.AppData? _appData; // Make nullable to fix constructor error


    // The initial page for the primary window
    private readonly Page _initialPage;

    public App(IAppDataService appDataService, IPreferencesService preferencesService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Resources["ServiceProvider"] = _serviceProvider;

        // Create the initial page
        _initialPage = CreateLoadingPage();

        // Subscribe to data changed events
        _appDataService.DataChanged += OnAppDataChanged;
        RequestedThemeChanged += OnRequestedThemeChanged;

        // Load theme preference
        LoadThemePreference();
    }

    // Handle data changes
    private void OnAppDataChanged(object? sender, Models.AppData appData)
    {
        _appData = appData;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            Window window = new(_initialPage);
            ApplyPlatformChrome(ThemePreferencePolicy.ResolveEffectiveTheme(
                _activeThemePreference, RequestedTheme));
            return window;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating window: {ex.Message}");
            return new Window(CreateLoadingPage());
        }
    }
    private LoadingPage CreateLoadingPage()
    {
        return new LoadingPage(_appDataService, _preferencesService, _serviceProvider.GetRequiredService<AppShell>());
    }

    // Load and apply the saved theme preference
    private async void LoadThemePreference()
    {
        try
        {
            var theme = await _preferencesService.GetThemePreferenceAsync();

            // Apply the app theme for system-level controls
            switch (theme)
            {
                case Services.ThemePreference.Light:
                    UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Light;
                    SetTheme(ThemePreference.Light);
                    break;
                case Services.ThemePreference.Dark:
                    UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Dark;
                    SetTheme(ThemePreference.Dark);
                    break;
                case Services.ThemePreference.System:
                default:
                    UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Unspecified;
                    SetTheme(ThemePreference.System);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading theme preference: {ex.Message}");
            // Dark is the fallback when no usable preference can be read.
            UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Dark;
            SetTheme(ThemePreference.Dark);
        }
    }

    public void SetTheme(string theme) => SetTheme(theme switch
    {
        "Light" => ThemePreference.Light,
        "Dark" => ThemePreference.Dark,
        _ => ThemePreference.System
    });

    private void SetTheme(ThemePreference preference)
    {
        _activeThemePreference = preference;
        AppTheme effectiveTheme = ThemePreferencePolicy.ResolveEffectiveTheme(preference, RequestedTheme);
        ApplyThemeDictionary(effectiveTheme);
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (_activeThemePreference == ThemePreference.System)
        {
            ApplyThemeDictionary(ThemePreferencePolicy.ResolveEffectiveTheme(
                _activeThemePreference, e.RequestedTheme));
        }
    }

    private void ApplyThemeDictionary(AppTheme effectiveTheme)
    {
        try
        {
            // Safely get the merged dictionaries collection
            var mergedDictionaries = Resources?.MergedDictionaries;
            if (mergedDictionaries == null)
                return;

            // Swap only the colour dictionary: tokens, icon geometries and component
            // styles stay merged and re-resolve their DynamicResource colours.
            // Dictionaries added by an earlier SetTheme call were constructed in code and
            // therefore have no Source, so match on type as well or they accumulate.
            var themeDictionaries = mergedDictionaries
                .Where(dict => dict is DarkTheme or LightTheme
                    || dict.Source?.OriginalString is string source
                       && (source.Contains("LightTheme.xaml") || source.Contains("DarkTheme.xaml")))
                .ToList();

            foreach (var dict in themeDictionaries)
            {
                mergedDictionaries.Remove(dict);
            }

            // Add the new theme dictionary
            ResourceDictionary newTheme = effectiveTheme == AppTheme.Dark
                ? new DarkTheme()
                : new LightTheme();

            mergedDictionaries.Add(newTheme);
            ApplyPlatformChrome(effectiveTheme);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SetTheme: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    static partial void ApplyPlatformChrome(AppTheme effectiveTheme);

    // Get the current app data
    public Models.AppData GetAppData()
    {
        return _appData ?? new Models.AppData
        {
            Beans = new List<Models.BeanData>(),
            RoastLogs = new List<Models.RoastData>()
        };
    }

    // Pass data to a page when navigating
    public void PassDataToPage(Page page)
    {
        if (page.BindingContext is not null)
        {
            return;
        }

        page.BindingContext = new NavigationParameters(GetAppData());
    }
}
