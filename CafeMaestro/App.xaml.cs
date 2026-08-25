using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace CafeMaestro;

public partial class App : Application
{
    private readonly IAppDataService _appDataService;
    private readonly IPreferencesService _preferencesService;
    private readonly IServiceProvider _serviceProvider;
    private readonly RoastPageViewModel _roastPageViewModel;
    private Window? _window;
    private ThemePreference _activeThemePreference = ThemePreference.Dark;
    private Models.AppData? _appData; // Make nullable to fix constructor error


    // The initial page for the primary window
    private readonly Page _initialPage;

    public App(
        IAppDataService appDataService,
        IPreferencesService preferencesService,
        IServiceProvider serviceProvider,
        RoastPageViewModel roastPageViewModel)
    {
        InitializeComponent();
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _roastPageViewModel = roastPageViewModel ?? throw new ArgumentNullException(nameof(roastPageViewModel));
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
            AttachWindowLifecycle(window);
            ApplyPlatformChrome(ThemePreferencePolicy.ResolveEffectiveTheme(
                _activeThemePreference, RequestedTheme));
            return window;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating window: {ex.Message}");
            Window fallbackWindow = new(CreateLoadingPage());
            AttachWindowLifecycle(fallbackWindow);
            return fallbackWindow;
        }
    }
    private LoadingPage CreateLoadingPage()
    {
        return new LoadingPage(
            _appDataService,
            _preferencesService,
            _serviceProvider.GetRequiredService<IAppActivationService>(),
            _serviceProvider.GetRequiredService<AppShell>());
    }

    private void AttachWindowLifecycle(Window window)
    {
        if (ReferenceEquals(_window, window))
        {
            return;
        }

        DetachWindowLifecycle();
        _window = window;
        _window.Stopped += OnWindowStopped;
        _window.Resumed += OnWindowResumed;
        _window.Destroying += OnWindowDestroying;
    }

    private void DetachWindowLifecycle()
    {
        if (_window is null)
        {
            return;
        }

        _window.Stopped -= OnWindowStopped;
        _window.Resumed -= OnWindowResumed;
        _window.Destroying -= OnWindowDestroying;
        _window = null;
    }

    private async void OnWindowStopped(object? sender, EventArgs e)
    {
        try
        {
            await _roastPageViewModel.OnWindowStoppedAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Window stop cleanup failed: {ex.Message}");
        }
    }

    private async void OnWindowResumed(object? sender, EventArgs e)
    {
        try
        {
            await _roastPageViewModel.OnWindowResumedAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Window resume refresh failed: {ex.Message}");
        }
    }

    private void OnWindowDestroying(object? sender, EventArgs e) => DetachWindowLifecycle();

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
