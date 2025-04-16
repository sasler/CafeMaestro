using System.Diagnostics;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;
using MauiAppTheme = Microsoft.Maui.ApplicationModel.AppTheme;

namespace CafeMaestro;

public partial class SettingsPage : ContentPage
{
    private readonly PreferencesService _preferencesService;
    private readonly AppDataService _appDataService;
    private readonly BeanService _beanService;
    private readonly RoastDataService _roastDataService;

    private string _currentFilePath = string.Empty;
    private bool _isLoadingThemeSettings = false; // Flag to suppress events during initialization
    private bool _isThemeInitialized = false; // Additional safeguard for theme initialization
    private bool _isFirstTimeNavigation = true; // Track if this is the first time appearing

    public SettingsPage(PreferencesService? preferencesService = null, AppDataService? appDataService = null,
                        BeanService? beanService = null, RoastDataService? roastDataService = null)
    {
        InitializeComponent();

        // First try to get the services from the application resources (our stored service provider)
        if (Application.Current?.Resources.TryGetValue("ServiceProvider", out var serviceProviderObj) == true && 
            serviceProviderObj is IServiceProvider serviceProvider)
        {
            _preferencesService = preferencesService ?? 
                                 serviceProvider.GetService<PreferencesService>() ??
                                 Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ??
                                 throw new InvalidOperationException("PreferencesService not available");
                             
            _appDataService = appDataService ?? 
                             serviceProvider.GetService<AppDataService>() ??
                             Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                             throw new InvalidOperationException("AppDataService not available");
                         
            _beanService = beanService ?? 
                          serviceProvider.GetService<BeanService>() ??
                          Application.Current?.Handler?.MauiContext?.Services.GetService<BeanService>() ??
                          throw new InvalidOperationException("BeanService not available");
                      
            _roastDataService = roastDataService ?? 
                               serviceProvider.GetService<RoastDataService>() ??
                               Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ??
                               throw new InvalidOperationException("RoastDataService not available");
        }
        else
        {
            // Fall back to the old way if app resources doesn't have our provider
            _preferencesService = preferencesService ?? 
                                 Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ??
                                 throw new InvalidOperationException("PreferencesService not available");
                             
            _appDataService = appDataService ?? 
                             Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                             throw new InvalidOperationException("AppDataService not available");
                         
            _beanService = beanService ?? 
                          Application.Current?.Handler?.MauiContext?.Services.GetService<BeanService>() ??
                          throw new InvalidOperationException("BeanService not available");
                      
            _roastDataService = roastDataService ?? 
                               Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ??
                               throw new InvalidOperationException("RoastDataService not available");
        }

        System.Diagnostics.Debug.WriteLine($"SettingsPage constructor - Using AppDataService at path: {_appDataService.DataFilePath}");

        // Initialize UI
        LoadDataFilePath();
        LoadVersionInfo();
        LoadThemeSettings();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LoadDataFilePath();
        
        // Check if this is first run and first navigation to this page
        // This will highlight the data file section when coming from first-run setup
        if (_isFirstTimeNavigation)
        {
            _isFirstTimeNavigation = false;
            
            bool isFirstRun = await _preferencesService.IsFirstRunAsync();
            if (isFirstRun)
            {
                // Highlight the data file section visually
                HighlightDataFileSection();
            }
        }
    }
    
    // Highlight the data file section to draw attention to it
    private void HighlightDataFileSection()
    {
        // Apply a visual highlight to draw attention (pulse animation)
        var dataFileSection = DataFileSection;
        if (dataFileSection != null)
        {
            // Store original color to restore later
            var originalColor = dataFileSection.BackgroundColor;
            
            // Highlight with animation
            dataFileSection.BackgroundColor = Colors.LightYellow;
            
            // Pulse animation to draw attention
            dataFileSection.Scale = 0.97;
            dataFileSection.FadeTo(0.8, 250).ContinueWith(_ => 
            {
                MainThread.BeginInvokeOnMainThread(async () => 
                {
                    await dataFileSection.FadeTo(1, 250);
                    await dataFileSection.ScaleTo(1.02, 150);
                    await dataFileSection.ScaleTo(1.0, 150);
                    
                    // Restore original background after animation
                    await Task.Delay(500);
                    dataFileSection.BackgroundColor = originalColor;
                });
            });
        }
    }

    private void LoadDataFilePath()
    {
        DataFilePath.Text = _appDataService.DataFilePath;
    }

    private void LoadVersionInfo()
    {
        // Get version from assembly info
        var version = AppInfo.Current.VersionString;
        var build = AppInfo.Current.BuildString;
        
        VersionLabel.Text = $"{version} (Build {build})";
    }

    private async void LoadThemeSettings()
    {
        try
        {
            _isLoadingThemeSettings = true; // Suppress events

            var theme = await _preferencesService.GetThemePreferenceAsync();

            // Set the corresponding radio button
            switch (theme)
            {
                case ThemePreference.Light:
                    LightThemeRadio.IsChecked = true;
                    break;
                case ThemePreference.Dark:
                    DarkThemeRadio.IsChecked = true;
                    break;
                case ThemePreference.System:
                default:
                    SystemThemeRadio.IsChecked = true;
                    break;
            }

            _isThemeInitialized = true; // Mark theme as initialized
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading theme settings: {ex.Message}");
            SystemThemeRadio.IsChecked = true; // Default to system theme
        }
        finally
        {
            _isLoadingThemeSettings = false; // Re-enable events
        }
    }

    private async void ThemeRadioButton_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (_isLoadingThemeSettings || !_isThemeInitialized || !e.Value) // Ignore events during initialization or unchecked state
            return;

        try
        {
            var radioButton = sender as RadioButton;
            ThemePreference selectedTheme = ThemePreference.System; // Default

            // Determine which theme was selected
            if (radioButton == LightThemeRadio)
                selectedTheme = ThemePreference.Light;
            else if (radioButton == DarkThemeRadio)
                selectedTheme = ThemePreference.Dark;
            else if (radioButton == SystemThemeRadio)
                selectedTheme = ThemePreference.System;

            // Save the preference
            await _preferencesService.SaveThemePreferenceAsync(selectedTheme);

            // Apply the theme immediately
            ApplyTheme(selectedTheme);
        }        catch (Exception ex)
        {
            Debug.WriteLine($"Error changing theme: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            await DisplayAlert("Error", $"Failed to change the theme: {ex.Message}", "OK");
        }
    }

    private void ApplyTheme(ThemePreference theme)
    {
        if (Application.Current == null) return;
        
        // Set the app's theme
        switch (theme)
        {
            case ThemePreference.Light:
                Application.Current.UserAppTheme = MauiAppTheme.Light;
                // Also apply theme resources
                (Application.Current as App)?.SetTheme("Light");
                break;
            case ThemePreference.Dark:
                Application.Current.UserAppTheme = MauiAppTheme.Dark;
                // Also apply theme resources
                (Application.Current as App)?.SetTheme("Dark");
                break;
            case ThemePreference.System:
                Application.Current.UserAppTheme = MauiAppTheme.Unspecified;
                // Also apply theme resources based on system setting
                (Application.Current as App)?.SetTheme("System");
                break;
        }
    }

    private async void SelectDataFileButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.json" } }
                });

            var options = new PickOptions
            {
                PickerTitle = "Select data file location",
                FileTypes = customFileType
            };

            var result = await FilePicker.PickAsync(options);
            
            if (result != null)
            {
                // First save path to preferences
                await _preferencesService.SaveAppDataFilePathAsync(result.FullPath);
                
                // Then update AppDataService - this now returns the loaded data
                var appData = await _appDataService.SetCustomFilePathAsync(result.FullPath);
                
                // Reload the UI
                LoadDataFilePath();
                
                // If this was during first run, mark setup as completed
                await _preferencesService.SetFirstRunCompletedAsync();
                
                await DisplayAlert("Success", "Data file location has been set.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select data file: {ex.Message}", "OK");
        }
    }

    private async void CreateDataFileButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            #if WINDOWS
            // On Windows, use the folder picker then prompt for filename
            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                folderPicker.FileTypeFilter.Add("*");
                
                // Get the current window handle
                IntPtr hwnd = IntPtr.Zero;
                if (Application.Current?.Windows != null && Application.Current.Windows.Count > 0)
                {
                    hwnd = WinRT.Interop.WindowNative.GetWindowHandle(Application.Current.Windows[0]);
                }
                else
                {
                    // Can't get window handle, show error and use standard picker
                    await DisplayAlert("Error", "Cannot access window handle", "OK");
                    await UseStandardFilePicker();
                    return;
                }
                // Associate the folder picker with the window
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    // Prompt user for filename
                    string fileName = await DisplayPromptAsync(
                        "Create Data File", 
                        "Enter a filename for your data file:", 
                        initialValue: "cafemaestro_data.json",
                        maxLength: 100);
                        
                    if (string.IsNullOrWhiteSpace(fileName))
                        return;
                    
                    // Ensure filename has .json extension
                    if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        fileName += ".json";
                    
                    string filePath = Path.Combine(folder.Path, fileName);
                    await CreateNewDataFile(filePath);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Windows folder picker failed: {ex.Message}", "OK");
                await UseStandardFilePicker();
            }
            #else
            await UseStandardFilePicker();
            #endif
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create data file: {ex.Message}", "OK");
        }
    }
    
    private async Task UseStandardFilePicker()
    {
        var customFileType = new FilePickerFileType(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".json" } },
                { DevicePlatform.Android, new[] { "application/json" } },
                { DevicePlatform.iOS, new[] { "public.json" } },
                { DevicePlatform.MacCatalyst, new[] { "public.json" } }
            });

        var options = new PickOptions
        {
            PickerTitle = "Choose location for new data file",
            FileTypes = customFileType
        };

        var result = await FilePicker.PickAsync(options);
        if (result != null)
        {
            string filePath = result.FullPath;
            
            // Ensure the file has the correct extension
            if (!filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                filePath += ".json";
                
            await CreateNewDataFile(filePath);
        }
    }
    
    private async Task CreateNewDataFile(string filePath)
    {
        try
        {
            // First save the path to preferences
            await _preferencesService.SaveAppDataFilePathAsync(filePath);
            
            // Create new empty data file through AppDataService - now returns loaded data
            var appData = await _appDataService.CreateEmptyDataFileAsync(filePath);
            
            // Update UI
            LoadDataFilePath();
            
            // If this was during first run, mark setup as completed
            await _preferencesService.SetFirstRunCompletedAsync();
            
            await DisplayAlert("Success", "Created new data file.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create file: {ex.Message}", "OK");
        }
    }

    private async void UseDefaultButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            bool confirmed = await DisplayAlert("Confirm", 
                "Are you sure you want to use the default data location? This will move your data to the app's private storage.", 
                "Yes", "No");
                
            if (confirmed)
            {
                // First clear the preferences
                await _preferencesService.ClearAppDataFilePathAsync();
                
                // Then reset the AppDataService path - now returns loaded data
                var appData = await _appDataService.ResetToDefaultPathAsync();
                
                // Create the file if it doesn't exist
                if (!_appDataService.DataFileExists())
                {
                    appData = await _appDataService.CreateEmptyDataFileAsync(_appDataService.DataFilePath);
                }
                
                // Update UI
                LoadDataFilePath();
                
                // If this was during first run, mark setup as completed
                await _preferencesService.SetFirstRunCompletedAsync();
                
                await DisplayAlert("Success", "Using default data location.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to set default data file: {ex.Message}", "OK");
        }
    }
    
    private async void ExportButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv" } },
                    { DevicePlatform.Android, new[] { "text/csv" } },
                    { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.comma-separated-values-text" } }
                });

            var options = new PickOptions
            {
                PickerTitle = "Select where to save CSV file",
                FileTypes = customFileType
            };

            var result = await FilePicker.PickAsync(options);
            if (result != null)
            {
                await _roastDataService.ExportRoastLogAsync(result.FullPath);
                await DisplayAlert("Success", "Roast log exported successfully!", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to export data: {ex.Message}", "OK");
        }
    }
    
    // Override OnBackButtonPressed to handle Android back button
    protected override bool OnBackButtonPressed()
    {
        // When back button is pressed, navigate to MainPage
        try
        {
            // Navigate back to MainPage using direct Shell.CurrentItem assignment
            // This works better on Android than GoToAsync
            if (Shell.Current?.Items.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(() => {
                    Shell.Current.CurrentItem = Shell.Current.Items[0]; // MainPage is the first item
                    System.Diagnostics.Debug.WriteLine("Navigated back to MainPage using hardware back button in SettingsPage");
                });
                return true; // Indicate we've handled the back button
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling back button in SettingsPage: {ex.Message}");
        }
        
        return base.OnBackButtonPressed(); // Let the system handle it if our code fails
    }
}