using System.Diagnostics;
using CafeMaestro.Services;
using MauiAppTheme = Microsoft.Maui.ApplicationModel.AppTheme;

namespace CafeMaestro;

public partial class SettingsPage : ContentPage
{
    private readonly AppDataService _appDataService;
    private readonly PreferencesService _preferencesService;

    public SettingsPage()
    {
        InitializeComponent();
        
        // Get services from DI
        _appDataService = Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ?? 
                          new AppDataService();
        _preferencesService = Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ?? 
                           new PreferencesService();
                               
        // Initialize UI
        LoadDataFilePath();
        LoadVersionInfo();
        LoadThemeSettings();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadDataFilePath();
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
      // Load the saved theme settings
    private async void LoadThemeSettings()
    {
        try
        {
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
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading theme settings: {ex.Message}");
            SystemThemeRadio.IsChecked = true; // Default to system theme
        }
    }

    // Handle theme radio button selection
    private async void ThemeRadioButton_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value) // Only respond to the checked button, not the unchecked ones
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
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error changing theme: {ex.Message}");
            await DisplayAlert("Error", "Failed to change the theme.", "OK");
        }
    }
    
    // Apply the selected theme to the app
    private void ApplyTheme(ThemePreference theme)
    {
        if (Application.Current == null) return;
        
        // Set the app's theme
        switch (theme)
        {
            case ThemePreference.Light:
                Application.Current.UserAppTheme = MauiAppTheme.Light;
                break;
            case ThemePreference.Dark:
                Application.Current.UserAppTheme = MauiAppTheme.Dark;
                break;
            case ThemePreference.System:
                Application.Current.UserAppTheme = MauiAppTheme.Unspecified;
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
                _appDataService.SetCustomFilePath(result.FullPath);
                await _preferencesService.SaveAppDataFilePathAsync(result.FullPath);
                LoadDataFilePath();
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
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(Application.Current.Windows[0]);
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
            // Create new empty data file
            bool success = await _appDataService.CreateEmptyDataFileAsync(filePath);
            
            if (success)
            {
                await _preferencesService.SaveAppDataFilePathAsync(filePath);
                LoadDataFilePath();
                await DisplayAlert("Success", "Created new data file.", "OK");
            }
            else
            {
                await DisplayAlert("Error", "Failed to create data file.", "OK");
            }
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
                await _preferencesService.ClearAppDataFilePathAsync();
                _appDataService.ResetToDefaultPath();
                LoadDataFilePath();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to set default data file: {ex.Message}", "OK");
        }
    }
}