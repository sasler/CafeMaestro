using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

public partial class App : Application
{
    private AppDataService _appDataService;
    private PreferencesService _preferencesService;
    private Models.AppData? _appData; // Make nullable to fix constructor error

    // Flag to track if first run setup is needed (to avoid firing it multiple times)
    private bool _firstRunSetupNeeded = false;

    public App()
    {
        InitializeComponent();

        // Store the service provider in application resources for consistent access
        if (Handler?.MauiContext?.Services is IServiceProvider serviceProvider)
        {
            Resources["ServiceProvider"] = serviceProvider;
            System.Diagnostics.Debug.WriteLine("App.xaml.cs: Stored service provider in application resources");
            
            // We'll use CreateWindow instead of setting MainPage directly
            // The AppShell will be created in the CreateWindow method
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("App.xaml.cs: Could not get service provider to store in application resources");
            // Fallback handled in CreateWindow
        }

        // Get the app data service
        _appDataService = Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                         new AppDataService();
        _preferencesService = Handler?.MauiContext?.Services.GetService<PreferencesService>() ??
                             new PreferencesService();

        // Initialize app data asynchronously
        InitializeAppDataAsync();

        // Load theme preference
        LoadThemePreference();
        
        // Subscribe to data changed events
        _appDataService.DataChanged += OnAppDataChanged;
    }
    
    // Handle data changes
    private void OnAppDataChanged(object? sender, Models.AppData appData)
    {
        _appData = appData;
        System.Diagnostics.Debug.WriteLine("App.xaml.cs: Data updated - " +
            $"Beans: {appData.Beans?.Count ?? 0}, Roasts: {appData.RoastLogs?.Count ?? 0}");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            // Create the Shell directly and set it as the window's page
            var appShell = new AppShell();
            var window = new Window(appShell);
            
            // Subscribe to window appearing event to show first run dialog
            window.Created += (s, e) =>
            {
                if (_firstRunSetupNeeded)
                {
                    // Show first run setup dialog after a short delay to ensure UI is ready
                    Task.Delay(1000).ContinueWith(async _ =>
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await ShowFirstRunSetupAsync();
                            // Mark first run as completed
                            await _preferencesService.SetFirstRunCompletedAsync();
                            _firstRunSetupNeeded = false;
                        });
                    });
                }
                
                // Wait for data to be fully loaded before passing it to the initial page
                // This ensures we have the correct data from the user-defined path
                Task.Run(async () =>
                {
                    // Give time for the AppData to be fully loaded
                    // Instead of a fixed delay, wait for _appData to be populated
                    int attempts = 0;
                    while (_appData == null && attempts < 10)
                    {
                        await Task.Delay(100);
                        attempts++;
                    }
                    
                    // Once data is loaded, then pass it to the current page
                    await MainThread.InvokeOnMainThreadAsync(() => {
                        if (Shell.Current?.CurrentPage != null && _appData != null)
                        {
                            System.Diagnostics.Debug.WriteLine("Data is loaded, now passing to the initial page");
                            PassDataToPage(Shell.Current.CurrentPage);
                        }
                    });
                });
            };
            
            return window;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CreateWindow: {ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            
            // Fallback to a simple window with MainPage
            return new Window(new MainPage());
        }
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

    private async void InitializeAppDataAsync()
    {
        try
        {
            // First check if this is the app's first run
            bool isFirstRun = await _preferencesService.IsFirstRunAsync();

            // Check if user has a saved file path preference
            string? savedFilePath = await _preferencesService.GetAppDataFilePathAsync();

            System.Diagnostics.Debug.WriteLine($"InitializeAppDataAsync - isFirstRun: {isFirstRun}, savedFilePath: {savedFilePath ?? "null"}");

            // If first run and no saved path, flag for setup
            if (isFirstRun && string.IsNullOrEmpty(savedFilePath))
            {
                _firstRunSetupNeeded = true;
                // Use default path initially
                // We'll use this simple approach instead since the protected method isn't available
                await _appDataService.SetCustomFilePathAsync(_appDataService.DataFilePath);
                System.Diagnostics.Debug.WriteLine("First run detected, will show setup dialog");
                return;
            }

            // Initialize the app data service with the saved preferences
            // This will load data only once at app startup
            _appData = await _appDataService.InitializeAsync(_preferencesService);
            System.Diagnostics.Debug.WriteLine($"Data file is set to: {_appDataService.DataFilePath}");
            System.Diagnostics.Debug.WriteLine($"Loaded data: Beans={_appData.Beans.Count}, Roasts={_appData.RoastLogs.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing app data: {ex.Message}");
            // Use default path on error
            // We'll use this simple approach instead since the protected method isn't available
            await _appDataService.SetCustomFilePathAsync(_appDataService.DataFilePath);
        }
    }

    // Show first run setup dialog to configure data file
    private async Task ShowFirstRunSetupAsync()
    {
        try
        {
            // Execute on main thread because it's a UI operation
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                bool useDefault = await Shell.Current.DisplayAlert(
                    "Welcome to CafeMaestro!",
                    "Would you like to store your coffee roasting data in the default application folder, or choose a custom location?",
                    "Use Default", "Choose Location");

                if (useDefault)
                {
                    // Use default path (already set)
                    // Create empty data file in default location
                    _appData = await _appDataService.CreateEmptyDataFileAsync(_appDataService.DataFilePath);
                }
                else
                {
                    // Navigate to settings page to choose location
                    await Shell.Current.GoToAsync("//settings");

                    // Display prompt about creating a data file
                    await Shell.Current.DisplayAlert(
                        "Select Data Location",
                        "Please use the options below to select an existing data file or create a new one in your preferred location.",
                        "OK");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing first run setup: {ex.Message}");
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
        return _appData ?? _appDataService.CurrentData;
    }
    
    // Pass data to a page when navigating
    public void PassDataToPage(Page page)
    {
        var navParams = new NavigationParameters(GetAppData());
        page.BindingContext = navParams;
        System.Diagnostics.Debug.WriteLine($"Passed data to {page.GetType().Name}");
    }
}