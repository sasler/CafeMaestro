using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

public partial class App : Application
{
    private AppDataService _appDataService;
    private PreferencesService _preferencesService;
    private Models.AppData? _appData; // Make nullable to fix constructor error
    private bool _appDataInitialized = false;

    // Flag to track if first run setup is needed (to avoid firing it multiple times)
    private bool _firstRunSetupNeeded = false;
    
    // The initial page for the primary window
    private Page _initialPage;

    public App()
    {
        InitializeComponent();

        // Initialize services with proper fallbacks
        IServiceProvider? serviceProvider = null;
        
        // Try to get the service provider from different sources depending on initialization state
        if (Handler?.MauiContext?.Services is IServiceProvider provider)
        {
            serviceProvider = provider;
            Resources["ServiceProvider"] = serviceProvider;
            System.Diagnostics.Debug.WriteLine("App.xaml.cs: Obtained service provider from Handler.MauiContext.Services");
        }
        else if (IPlatformApplication.Current?.Services is IServiceProvider platformProvider)
        {
            serviceProvider = platformProvider;
            Resources["ServiceProvider"] = serviceProvider;
            System.Diagnostics.Debug.WriteLine("App.xaml.cs: Obtained service provider from IPlatformApplication.Current.Services");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("App.xaml.cs: Could not get service provider from standard sources");
            
            // Create a minimal service provider for the LoadingPage to use
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<AppDataService>();
            serviceCollection.AddSingleton<PreferencesService>();
            serviceProvider = serviceCollection.BuildServiceProvider();
            Resources["ServiceProvider"] = serviceProvider;
            System.Diagnostics.Debug.WriteLine("App.xaml.cs: Created minimal service provider as fallback");
        }
        
        // Get services using GetService with null fallbacks
        _appDataService = serviceProvider.GetService<AppDataService>() ?? new AppDataService();
        _preferencesService = serviceProvider.GetService<PreferencesService>() ?? new PreferencesService();
        
        System.Diagnostics.Debug.WriteLine($"App.xaml.cs: AppDataService: {_appDataService.GetHashCode()}");
        System.Diagnostics.Debug.WriteLine($"App.xaml.cs: AppDataService created");
        
        // Create the initial page
        _initialPage = new LoadingPage(serviceProvider);
        
        // Subscribe to data changed events
        _appDataService.DataChanged += OnAppDataChanged;
        
        // Load theme preference
        LoadThemePreference();
    }
    
    // Method to set first run flag from LoadingPage
    public void SetFirstRunNeeded(bool needed)
    {
        _firstRunSetupNeeded = needed;
        System.Diagnostics.Debug.WriteLine($"App.xaml.cs: First run needed set to {needed}");
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
            // Create a window with the initial page that was prepared in the constructor
            var window = new Window(_initialPage);
            
            // Subscribe to window created event to show first run dialog if needed
            window.Created += (s, e) =>
            {
                // If current Page is AppShell and first run is needed, show dialog
                if (_firstRunSetupNeeded && window.Page is AppShell)
                {
                    System.Diagnostics.Debug.WriteLine("First run setup needed, showing dialog after UI is ready");
                    // Show first run setup dialog after a short delay to ensure UI is ready
                    Task.Delay(500).ContinueWith(async _ =>
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await ShowFirstRunSetupAsync();
                        });
                    });
                }
            };
            
            return window;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CreateWindow: {ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            
            // Even in error case, show a LoadingPage with the service provider if available
            if (Handler?.MauiContext?.Services is IServiceProvider serviceProvider)
            {
                return new Window(new LoadingPage(serviceProvider));
            }
            
            // Ultimate fallback - must provide a non-null page
            return new Window(new AppShell());
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

    private async Task InitializeAppDataAsync()
    {
        try
        {
            // First check if this is the app's first run
            bool isFirstRun = await _preferencesService.IsFirstRunAsync();

            // Check if user has a saved file path preference
            string? savedFilePath = await _preferencesService.GetAppDataFilePathAsync();

            System.Diagnostics.Debug.WriteLine($"InitializeAppDataAsync - isFirstRun: {isFirstRun}, savedFilePath: {savedFilePath ?? "null"}");

            // If it's the first run or there's no saved path, we need to prompt the user
            if (isFirstRun || string.IsNullOrEmpty(savedFilePath))
            {
                _firstRunSetupNeeded = true;
                // Create empty data just for UI initialization
                _appData = new Models.AppData
                {
                    Beans = new List<Models.Bean>(),
                    RoastLogs = new List<Models.RoastData>()
                };
                System.Diagnostics.Debug.WriteLine("First run or no saved path detected, will show file selection dialog");
                _appDataInitialized = true;
                return;
            }

            // We have a saved file path, verify it exists
            if (File.Exists(savedFilePath))
            {
                // Load data from the user-defined path
                await _appDataService.SetCustomFilePathAsync(savedFilePath);
                _appData = await _appDataService.LoadAppDataAsync();
                System.Diagnostics.Debug.WriteLine($"Loaded data from user-defined path: {savedFilePath}");
                System.Diagnostics.Debug.WriteLine($"Loaded data: Beans={_appData.Beans.Count}, Roasts={_appData.RoastLogs.Count}");
            }
            else
            {
                // File doesn't exist anymore, need to prompt user again
                System.Diagnostics.Debug.WriteLine($"Saved file path not found: {savedFilePath}, will prompt for new file");
                _firstRunSetupNeeded = true;
                // Create empty data just for UI initialization
                _appData = new Models.AppData
                {
                    Beans = new List<Models.Bean>(),
                    RoastLogs = new List<Models.RoastData>()
                };
                // Clear the invalid path
                await _preferencesService.ClearAppDataFilePathAsync();
            }
            
            _appDataInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing app data: {ex.Message}");
            // Something went wrong, we need to prompt the user
            _firstRunSetupNeeded = true;
            // Create empty data just for UI initialization
            _appData = new Models.AppData
            {
                Beans = new List<Models.Bean>(),
                RoastLogs = new List<Models.RoastData>()
            };
            _appDataInitialized = true;
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
                    "Use Default", "Choose Custom Location");

                if (useDefault)
                {
                    // User chose default path
                    // Create a default file path in Documents folder
                    string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string defaultFilePath = Path.Combine(documentsPath, "CafeMaestro", "cafemaestro_data.json");
                    
                    // Ensure directory exists
                    string? directoryPath = Path.GetDirectoryName(defaultFilePath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    else
                    {
                        throw new InvalidOperationException("Could not determine directory path for default data file");
                    }
                    
                    // Create the file and save
                    _appData = await _appDataService.CreateEmptyDataFileAsync(defaultFilePath);
                    
                    // Save this path in preferences
                    await _preferencesService.SaveAppDataFilePathAsync(defaultFilePath);
                    
                    // Mark first run as completed
                    await _preferencesService.SetFirstRunCompletedAsync();
                    _firstRunSetupNeeded = false;
                    
                    System.Diagnostics.Debug.WriteLine($"Created default data file at: {defaultFilePath}");
                    
                    // Notify user
                    await Shell.Current.DisplayAlert(
                        "Data File Created",
                        $"Your coffee data will be stored at:\n{defaultFilePath}",
                        "OK");
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
            // If there's an error, try again on next app launch
            await _preferencesService.ClearAppDataFilePathAsync();
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
            Beans = new List<Models.Bean>(),
            RoastLogs = new List<Models.RoastData>()
        };
    }
    
    // Pass data to a page when navigating
    public void PassDataToPage(Page page)
    {
        var navParams = new NavigationParameters(GetAppData());
        page.BindingContext = navParams;
        System.Diagnostics.Debug.WriteLine($"Passed data to {page.GetType().Name}");
    }
}