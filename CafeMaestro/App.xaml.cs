using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

public partial class App : Application
{
    private AppDataService _appDataService;
    private PreferencesService _preferencesService;

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
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Get the service provider from the current handler
        var serviceProvider = Handler?.MauiContext?.Services ?? 
                             throw new InvalidOperationException("Service provider not available");

        // Create the app's main window using DI
        var window = new Window(new AppShell(serviceProvider));

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
        };

        return window;
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
                // Use default path initially without firing events
                _appDataService.ResetToDefaultPath(false);
                System.Diagnostics.Debug.WriteLine("First run detected, will show setup dialog");
                return;
            }

            // Initialize the app data service with the saved preferences
            await _appDataService.InitializeAsync(_preferencesService);
            System.Diagnostics.Debug.WriteLine($"Data file is set to: {_appDataService.DataFilePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing app data: {ex.Message}");
            // Use default path on error (don't fire events to prevent cascading issues)
            _appDataService.ResetToDefaultPath(false);
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
                    await _appDataService.CreateEmptyDataFileAsync(_appDataService.DataFilePath);
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
}