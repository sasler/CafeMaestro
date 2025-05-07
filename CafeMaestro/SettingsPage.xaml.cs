using System.Diagnostics;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Maui;
using MauiAppTheme = Microsoft.Maui.ApplicationModel.AppTheme;
using CommunityToolkit.Maui.Storage;
using System.Text;
using System.Windows.Input;
using System.Collections.ObjectModel;
using CafeMaestro.Models;

namespace CafeMaestro;

public partial class SettingsPage : ContentPage
{
    private readonly PreferencesService _preferencesService;
    private readonly AppDataService _appDataService;
    private readonly BeanDataService _beanService;
    private readonly RoastDataService _roastDataService;
    private readonly RoastLevelService _roastLevelService;
    private readonly IFileSaver _fileSaver;
    private readonly IFolderPicker _folderPicker;

    // Collection for roast levels
    private ObservableCollection<RoastLevelViewModel> _roastLevels = new ObservableCollection<RoastLevelViewModel>();

    // Commands for roast level management
    public ICommand EditRoastLevelCommand { get; private set; }
    public ICommand DeleteRoastLevelCommand { get; private set; }

    private string _currentFilePath = string.Empty;
    private bool _isLoadingThemeSettings = false; // Flag to suppress events during initialization
    private bool _isThemeInitialized = false; // Additional safeguard for theme initialization
    private bool _isFirstTimeNavigation = true; // Track if this is the first time appearing

    // Current roast level being edited
    private RoastLevelViewModel? _currentEditRoastLevel;
    private bool _isNewRoastLevel = false;

    public SettingsPage(PreferencesService? preferencesService = null, AppDataService? appDataService = null,
                        BeanDataService? beanService = null, RoastDataService? roastDataService = null,
                        RoastLevelService? roastLevelService = null, IFileSaver? fileSaver = null, IFolderPicker? folderPicker = null)
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
                          serviceProvider.GetService<BeanDataService>() ??
                          Application.Current?.Handler?.MauiContext?.Services.GetService<BeanDataService>() ??
                          throw new InvalidOperationException("BeanService not available");

            _roastDataService = roastDataService ??
                               serviceProvider.GetService<RoastDataService>() ??
                               Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ??
                               throw new InvalidOperationException("RoastDataService not available");

            _roastLevelService = roastLevelService ??
                                serviceProvider.GetService<RoastLevelService>() ??
                                Application.Current?.Handler?.MauiContext?.Services.GetService<RoastLevelService>() ??
                                throw new InvalidOperationException("RoastLevelService not available");

            _fileSaver = fileSaver ??
                        serviceProvider.GetService<IFileSaver>() ??
                        Application.Current?.Handler?.MauiContext?.Services.GetService<IFileSaver>() ??
                        FileSaver.Default;

            _folderPicker = folderPicker ??
                           serviceProvider.GetService<IFolderPicker>() ??
                           Application.Current?.Handler?.MauiContext?.Services.GetService<IFolderPicker>() ??
                           FolderPicker.Default;
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
                          Application.Current?.Handler?.MauiContext?.Services.GetService<BeanDataService>() ??
                          throw new InvalidOperationException("BeanService not available");

            _roastDataService = roastDataService ??
                               Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ??
                               throw new InvalidOperationException("RoastDataService not available");

            _roastLevelService = roastLevelService ??
                                Application.Current?.Handler?.MauiContext?.Services.GetService<RoastLevelService>() ??
                                throw new InvalidOperationException("RoastLevelService not available");

            _fileSaver = fileSaver ??
                        Application.Current?.Handler?.MauiContext?.Services.GetService<IFileSaver>() ??
                        FileSaver.Default;

            _folderPicker = folderPicker ??
                           Application.Current?.Handler?.MauiContext?.Services.GetService<IFolderPicker>() ??
                           FolderPicker.Default;
        }

        // Initialize UI
        LoadDataFilePath();
        LoadVersionInfo();
        LoadThemeSettings();
        LoadRoastLevels();

        // Initialize commands
        EditRoastLevelCommand = new Command<RoastLevelViewModel>(EditRoastLevel);
        DeleteRoastLevelCommand = new Command<RoastLevelViewModel>(DeleteRoastLevel);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Reload data
        LoadDataFilePath();

        // Reload roast levels when returning to this page
        LoadRoastLevels();

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

        // Basic version info
        VersionLabel.Text = $"{version} (Build {build})";

        // Add version history information from VersionTracking
        var versionHistory = new StringBuilder();

        // Check if this is the first launch for the current version
        if (Microsoft.Maui.ApplicationModel.VersionTracking.IsFirstLaunchForCurrentVersion)
        {
            versionHistory.AppendLine("\nThis is the first time running this version.");
        }

        // Add first installed version info
        versionHistory.AppendLine($"\nFirst installed version: {Microsoft.Maui.ApplicationModel.VersionTracking.FirstInstalledVersion}");

        // Add version history (limited to last 5 versions)
        var versions = Microsoft.Maui.ApplicationModel.VersionTracking.VersionHistory.ToList();
        if (versions.Any())
        {
            versionHistory.AppendLine("\nVersion History:");

            // Display the most recent 5 versions
            foreach (var v in versions.Take(5))
            {
                versionHistory.AppendLine($"- {v}");
            }

            // Indicate if there are more versions in history
            if (versions.Count > 5)
            {
                versionHistory.AppendLine($"(+ {versions.Count - 5} more versions)");
            }
        }

        // Add the version history to the label
        VersionHistoryLabel.Text = versionHistory.ToString();
    }

    private async void LoadThemeSettings()
    {
        try
        {
            _isLoadingThemeSettings = true; // Suppress events

            var theme = await _preferencesService.GetThemePreferenceAsync();

            // Set the selected index of the ThemePicker
            switch (theme)
            {
                case ThemePreference.Light:
                    ThemePicker.SelectedIndex = 1; // Light Theme
                    break;
                case ThemePreference.Dark:
                    ThemePicker.SelectedIndex = 2; // Dark Theme
                    break;
                case ThemePreference.System:
                default:
                    ThemePicker.SelectedIndex = 0; // System Theme
                    break;
            }

            _isThemeInitialized = true; // Mark theme as initialized
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading theme settings: {ex.Message}");
            ThemePicker.SelectedIndex = 0; // Default to system theme
        }
        finally
        {
            _isLoadingThemeSettings = false; // Re-enable events
        }
    }

    private async void ThemePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_isLoadingThemeSettings || !_isThemeInitialized) // Ignore events during initialization
            return;

        try
        {
            ThemePreference selectedTheme = ThemePreference.System; // Default

            // Determine which theme was selected based on the picker index
            switch (ThemePicker.SelectedIndex)
            {
                case 1: // Light Theme
                    selectedTheme = ThemePreference.Light;
                    break;
                case 2: // Dark Theme
                    selectedTheme = ThemePreference.Dark;
                    break;
                case 0: // System Theme
                default:
                    selectedTheme = ThemePreference.System;
                    break;
            }

            // Save the preference
            await _preferencesService.SaveThemePreferenceAsync(selectedTheme);

            // Apply the theme immediately
            ApplyTheme(selectedTheme);
        }
        catch (Exception ex)
        {
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

    private async void ExistingDataFileButton_Clicked(object sender, EventArgs e)
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

    private async void NewDataFileButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Create empty JSON content for the new file
            var jsonContent = "{}";
            var fileName = "NewCafeMaestroDataFile.json";

            // Use a memory stream for the file content
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));

            // Save the file using FileSaver
            var result = await _fileSaver.SaveAsync(fileName, stream, CancellationToken.None);

            // Check if operation was canceled by the user (no exception but not successful)
            if (!result.IsSuccessful && result.Exception.Message.Contains("cancel"))
            {
                return;
            }

            if (result.IsSuccessful)
            {
                await CreateNewDataFile(result.FilePath);
            }
            else if (result.Exception != null)
            {
                await DisplayAlert("Error", $"Failed to create file: {result.Exception.Message}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create file: {ex.Message}", "OK");
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

    private async void ExportButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Create a cancellation token source with a reasonable timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

            // Open the folder picker
            var result = await _folderPicker.PickAsync(cts.Token);

            if (result.IsSuccessful && result.Folder != null)
            {
                // Generate a filename with current date
                string fileName = $"CafeMaestro_RoastLog_{DateTime.Now:yyyy-MM-dd}.csv";

                // Combine the folder path with the filename
                string fullPath = Path.Combine(result.Folder.Path, fileName);

                // Export the data to the selected folder
                await _roastDataService.ExportRoastLogAsync(fullPath);

                await DisplayAlert("Success", $"Roast log exported successfully to:\n{fullPath}", "OK");
            }
            else if (!result.IsSuccessful && result.Exception != null)
            {
                // Check if the operation was canceled through the token
                if (result.Exception is OperationCanceledException)
                {
                    // Operation was canceled through the token, no need for an error message
                    return;
                }

                await DisplayAlert("Error", $"Failed to select folder: {result.Exception.Message}", "OK");
            }
        }
        catch (OperationCanceledException)
        {
            // Silently handle cancellation
            return;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to export data: {ex.Message}", "OK");
        }
    }

    public async void ImportButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Show a selection dialog for what to import
            string action = await DisplayActionSheet("Select Import Type", "Cancel", null, "Coffee Beans", "Roast Logs");

            // Handle the selection
            switch (action)
            {
                case "Coffee Beans":
                    // Navigate to the BeanImportPage using the registered route name
                    await Shell.Current.GoToAsync(nameof(BeanImportPage));
                    break;

                case "Roast Logs":
                    // Navigate to the RoastImportPage using the registered route name
                    await Shell.Current.GoToAsync(nameof(RoastImportPage));
                    break;

                case "Cancel":
                default:
                    // User canceled, do nothing
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in import selection: {ex.Message}");
            await DisplayAlert("Error", $"Failed to open import page: {ex.Message}", "OK");
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
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Shell.Current.CurrentItem = Shell.Current.Items[0]; // MainPage is the first item
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

    private async void LoadRoastLevels()
    {
        try
        {
            var roastLevels = await _roastLevelService.GetRoastLevelsAsync();
            _roastLevels.Clear();

            foreach (var roastLevel in roastLevels.OrderBy(l => l.MinWeightLossPercentage))
            {
                _roastLevels.Add(RoastLevelViewModel.FromModel(roastLevel));
            }

            // Set the collection as the ItemsSource for the CollectionView
            RoastLevelsCollection.ItemsSource = _roastLevels;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading roast levels: {ex.Message}");
        }
    }

    private void EditRoastLevel(RoastLevelViewModel roastLevelViewModel)
    {
        try
        {
            // Store the roast level being edited
            _currentEditRoastLevel = new RoastLevelViewModel
            {
                Id = roastLevelViewModel.Id,
                Name = roastLevelViewModel.Name,
                MinWeightLossPercentage = roastLevelViewModel.MinWeightLossPercentage,
                MaxWeightLossPercentage = roastLevelViewModel.MaxWeightLossPercentage
            };
            _isNewRoastLevel = false;

            // Set title
            EditPopupTitle.Text = "Edit Roast Level";

            // Populate the form fields
            RoastLevelNameEntry.Text = _currentEditRoastLevel.Name;
            MinWeightLossEntry.Text = _currentEditRoastLevel.MinWeightLossPercentage.ToString("F1");
            MaxWeightLossEntry.Text = _currentEditRoastLevel.MaxWeightLossPercentage.ToString("F1");

            // Show the popup
            EditRoastLevelPopup.IsVisible = true;
            RoastLevelNameEntry.Focus();
        }
        catch (Exception ex)
        {
            DisplayAlert("Error", $"Failed to edit roast level: {ex.Message}", "OK");
        }
    }

    private async void DeleteRoastLevel(RoastLevelViewModel roastLevelViewModel)
    {
        try
        {
            bool confirm = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete the roast level '{roastLevelViewModel.Name}'?", "Yes", "No");
            if (confirm)
            {
                await _roastLevelService.DeleteRoastLevelAsync(roastLevelViewModel.Id);
                _roastLevels.Remove(roastLevelViewModel);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to delete roast level: {ex.Message}", "OK");
        }
    }

    private void AddRoastLevel_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Create a new roast level
            _currentEditRoastLevel = new RoastLevelViewModel
            {
                Id = Guid.NewGuid(),
                Name = "",
                MinWeightLossPercentage = 0,
                MaxWeightLossPercentage = 0
            };
            _isNewRoastLevel = true;

            // Set title
            EditPopupTitle.Text = "Add Roast Level";

            // Clear form fields
            RoastLevelNameEntry.Text = string.Empty;
            MinWeightLossEntry.Text = "0.0";
            MaxWeightLossEntry.Text = "0.0";

            // Show the popup
            EditRoastLevelPopup.IsVisible = true;
            RoastLevelNameEntry.Focus();
        }
        catch (Exception ex)
        {
            DisplayAlert("Error", $"Failed to create new roast level: {ex.Message}", "OK");
        }
    }

    private async void SaveRoastLevel_Clicked(object sender, EventArgs e)
    {
        if (_currentEditRoastLevel == null)
        {
            EditRoastLevelPopup.IsVisible = false;
            return;
        }

        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(RoastLevelNameEntry.Text))
            {
                await DisplayAlert("Validation Error", "Name is required.", "OK");
                return;
            }

            if (!double.TryParse(MinWeightLossEntry.Text, out double minWeight))
            {
                await DisplayAlert("Validation Error", "Min weight loss must be a valid number.", "OK");
                return;
            }

            if (!double.TryParse(MaxWeightLossEntry.Text, out double maxWeight))
            {
                await DisplayAlert("Validation Error", "Max weight loss must be a valid number.", "OK");
                return;
            }

            if (minWeight < 0)
            {
                await DisplayAlert("Validation Error", "Minimum weight loss percentage must be at least 0.", "OK");
                return;
            }

            if (maxWeight <= minWeight)
            {
                await DisplayAlert("Validation Error", "Maximum weight loss must be greater than minimum weight loss.", "OK");
                return;
            }

            // Update the roast level model
            _currentEditRoastLevel.Name = RoastLevelNameEntry.Text;
            _currentEditRoastLevel.MinWeightLossPercentage = minWeight;
            _currentEditRoastLevel.MaxWeightLossPercentage = maxWeight;

            // Save changes
            bool success;
            if (_isNewRoastLevel)
            {
                // Add new roast level
                success = await _roastLevelService.AddRoastLevelAsync(_currentEditRoastLevel.ToModel());
                if (success)
                {
                    // Add to the collection view
                    _roastLevels.Add(_currentEditRoastLevel);
                }
            }
            else
            {
                // Update existing roast level
                success = await _roastLevelService.UpdateRoastLevelAsync(_currentEditRoastLevel.ToModel());
                if (success)
                {
                    // Update in the collection view
                    var existingItem = _roastLevels.FirstOrDefault(r => r.Id == _currentEditRoastLevel.Id);
                    if (existingItem != null)
                    {
                        var index = _roastLevels.IndexOf(existingItem);
                        _roastLevels[index] = _currentEditRoastLevel;
                    }
                }
            }

            if (success)
            {
                // Hide the popup
                EditRoastLevelPopup.IsVisible = false;

                // Refresh the display
                LoadRoastLevels();
            }
            else
            {
                await DisplayAlert("Error", "Failed to save roast level. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private void CancelRoastLevel_Clicked(object sender, EventArgs e)
    {
        // Just hide the popup, don't save changes
        EditRoastLevelPopup.IsVisible = false;
    }
}