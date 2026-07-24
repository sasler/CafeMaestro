using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace CafeMaestro;

public partial class App : Application
{
    private readonly IAppDataService _appDataService;
    private readonly IPreferencesService _preferencesService;
    private readonly IServiceProvider _serviceProvider;
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
            return new Window(_initialPage);
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
                    SetTheme("Light");
                    break;
                case Services.ThemePreference.Dark:
                    UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Dark;
                    SetTheme("Dark");
                    break;
                case Services.ThemePreference.System:
                default:
                    UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Unspecified;
                    SetTheme("System");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading theme preference: {ex.Message}");
            // Default to system theme
            UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Unspecified;
            SetTheme("System");
        }
    }

    public void SetTheme(string theme)
    {
        try
        {
            // Safely get the merged dictionaries collection
            var mergedDictionaries = Resources?.MergedDictionaries;
            if (mergedDictionaries == null)
                return;

            // Since we can't set Source programmatically, we'll handle styles.xaml differently
            // First, let's identify theme dictionaries and other dictionaries
            var themeDictionaries = new List<ResourceDictionary>();
            var otherDictionaries = new List<ResourceDictionary>();

            foreach (var dict in mergedDictionaries.ToList())
            {
                string? source = dict.Source?.OriginalString;
                if (source != null && (source.Contains("LightTheme.xaml") || source.Contains("DarkTheme.xaml")))
                {
                    themeDictionaries.Add(dict);
                }
                else
                {
                    otherDictionaries.Add(dict);
                }
            }
            // Remove only theme dictionaries, keeping other dictionaries intact
            foreach (var dict in themeDictionaries)
            {
                mergedDictionaries.Remove(dict);
            }

            // Add the new theme dictionary
            ResourceDictionary newTheme;
            switch (theme)
            {
                case "Light":
                    newTheme = new LightTheme();
                    break;
                case "Dark":
                    newTheme = new DarkTheme();
                    break;
                default:
                    // Set theme based on system preference
                    if (Current?.RequestedTheme == AppTheme.Dark)
                        newTheme = new DarkTheme();
                    else
                        newTheme = new LightTheme();
                    break;
            }

            // Add the theme dictionary first for proper precedence
            mergedDictionaries.Add(newTheme);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SetTheme: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

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
