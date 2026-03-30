using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using MauiAppTheme = Microsoft.Maui.ApplicationModel.AppTheme;

namespace CafeMaestro.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly IPreferencesService _preferencesService;
    private readonly IAppDataService _appDataService;
    private readonly IRoastDataService _roastDataService;
    private readonly IRoastLevelService _roastLevelService;
    private readonly IFileSaver _fileSaver;
    private readonly IFolderPicker _folderPicker;
    private readonly INavigationService _navigationService;
    private readonly IMessenger _messenger;
    private bool _isLoadingThemeSettings;
    private bool _isThemeInitialized;
    private bool _isSubscribed;
    private bool _isFirstTimeNavigation = true;
    private RoastLevelViewModel? _currentEditRoastLevel;
    private bool _isNewRoastLevel;

    public SettingsPageViewModel(
        IPreferencesService preferencesService,
        IAppDataService appDataService,
        IRoastDataService roastDataService,
        IRoastLevelService roastLevelService,
        IFileSaver fileSaver,
        IFolderPicker folderPicker,
        INavigationService navigationService)
        : this(
            preferencesService,
            appDataService,
            roastDataService,
            roastLevelService,
            fileSaver,
            folderPicker,
            navigationService,
            WeakReferenceMessenger.Default)
    {
    }

    public SettingsPageViewModel(
        IPreferencesService preferencesService,
        IAppDataService appDataService,
        IRoastDataService roastDataService,
        IRoastLevelService roastLevelService,
        IFileSaver fileSaver,
        IFolderPicker folderPicker,
        INavigationService navigationService,
        IMessenger messenger)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _roastDataService = roastDataService ?? throw new ArgumentNullException(nameof(roastDataService));
        _roastLevelService = roastLevelService ?? throw new ArgumentNullException(nameof(roastLevelService));
        _fileSaver = fileSaver ?? throw new ArgumentNullException(nameof(fileSaver));
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
    }

    [ObservableProperty]
    private string _dataFilePath = "Loading...";

    [ObservableProperty]
    private ObservableCollection<RoastLevelViewModel> _roastLevels = new();

    [ObservableProperty]
    private bool _isEditRoastLevelPopupVisible;

    [ObservableProperty]
    private string _editPopupTitle = "Edit Roast Level";

    [ObservableProperty]
    private string _roastLevelName = string.Empty;

    [ObservableProperty]
    private string _minWeightLossText = "0.0";

    [ObservableProperty]
    private string _maxWeightLossText = "0.0";

    [ObservableProperty]
    private int _selectedThemeIndex;

    [ObservableProperty]
    private string _versionDisplay = string.Empty;

    [ObservableProperty]
    private string _versionHistoryDisplay = string.Empty;

    public bool ShouldHighlightDataFileSection { get; private set; }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (_isLoadingThemeSettings || !_isThemeInitialized)
        {
            return;
        }

        _ = UpdateThemeAsync(value);
    }

    public async Task OnAppearingAsync()
    {
        EnsureSubscribed();
        LoadDataFilePath();
        LoadVersionInfo();
        await LoadThemeSettingsAsync();
        await LoadRoastLevelsAsync();

        if (_isFirstTimeNavigation)
        {
            _isFirstTimeNavigation = false;
            ShouldHighlightDataFileSection = await _preferencesService.IsFirstRunAsync();
        }
    }

    public void OnDisappearing()
    {
        Unsubscribe();
    }

    public void MarkDataFileSectionHighlighted()
    {
        ShouldHighlightDataFileSection = false;
    }

    public async Task UseExistingDataFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            await _preferencesService.SaveAppDataFilePathAsync(filePath);
            await _appDataService.SetCustomFilePathAsync(filePath);
            await _preferencesService.SetFirstRunCompletedAsync();

            LoadDataFilePath();
            await LoadRoastLevelsAsync();
            SendAlert("Success", "Data file location has been set.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error selecting data file: {ex.Message}");
            SendAlert("Error", $"Failed to select data file: {ex.Message}");
        }
    }

    public async Task<bool> DeleteRoastLevelCoreAsync(RoastLevelViewModel roastLevelViewModel)
    {
        try
        {
            bool success = await _roastLevelService.DeleteRoastLevelAsync(roastLevelViewModel.Id);
            if (!success)
            {
                SendAlert("Error", "Failed to delete roast level.");
                return false;
            }

            await LoadRoastLevelsAsync();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting roast level: {ex.Message}");
            SendAlert("Error", $"Failed to delete roast level: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ResetRoastLevelsToDefaultsCoreAsync()
    {
        try
        {
            bool success = await _roastLevelService.SaveRoastLevelsAsync(CreateDefaultRoastLevels());
            if (!success)
            {
                SendAlert("Error", "Failed to reset roast levels. Please try again.");
                return false;
            }

            await LoadRoastLevelsAsync();
            SendAlert("Success", "Roast levels reset to defaults.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error resetting roast levels: {ex.Message}");
            SendAlert("Error", $"Failed to reset roast levels: {ex.Message}");
            return false;
        }
    }

    public Task GoBackAsync()
    {
        return _navigationService.GoToAsync(Routes.Main);
    }

    [RelayCommand]
    private async Task ChooseExistingDataFileAsync()
    {
        try
        {
            var message = _messenger.Send(new PickDataFileRequestMessage("Select data file location"));
            string? filePath = await message.Response;

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                await UseExistingDataFileAsync(filePath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error requesting data file selection: {ex.Message}");
            SendAlert("Error", $"Failed to select data file: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CreateNewDataFileAsync()
    {
        try
        {
            const string jsonContent = "{}";
            const string fileName = "NewCafeMaestroDataFile.json";

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
            var result = await _fileSaver.SaveAsync(fileName, stream, CancellationToken.None);

            if (result.IsSuccessful)
            {
                await CreateNewDataFileAtPathAsync(result.FilePath);
                return;
            }

            if (result.Exception is OperationCanceledException)
            {
                return;
            }

            if (result.Exception != null &&
                result.Exception.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SendAlert("Error", $"Failed to create file: {result.Exception?.Message ?? "Unknown error"}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating new data file: {ex.Message}");
            SendAlert("Error", $"Failed to create file: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task UseDefaultDataFileAsync()
    {
        try
        {
            await _appDataService.ResetToDefaultPathAsync();
            await _preferencesService.SaveAppDataFilePathAsync(_appDataService.DataFilePath);
            await _preferencesService.SetFirstRunCompletedAsync();

            LoadDataFilePath();
            await LoadRoastLevelsAsync();
            SendAlert("Success", $"Using default data file location:\n{_appDataService.DataFilePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error using default data file: {ex.Message}");
            SendAlert("Error", $"Failed to use default data file location: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await _folderPicker.PickAsync(cancellationTokenSource.Token);

            if (result.IsSuccessful && result.Folder != null)
            {
                string fileName = $"CafeMaestro_RoastLog_{DateTime.Now:yyyy-MM-dd}.csv";
                string fullPath = Path.Combine(result.Folder.Path, fileName);
                await _roastDataService.ExportRoastLogAsync(fullPath);
                SendAlert("Success", $"Roast log exported successfully to:\n{fullPath}");
                return;
            }

            if (result.Exception is OperationCanceledException)
            {
                return;
            }

            if (result.Exception != null)
            {
                SendAlert("Error", $"Failed to select folder: {result.Exception.Message}");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error exporting roast log: {ex.Message}");
            SendAlert("Error", $"Failed to export data: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        try
        {
            var message = _messenger.Send(new SettingsActionSheetRequestMessage(
                "Select Import Type",
                "Cancel",
                "Coffee Beans",
                "Roast Logs"));

            string? action = await message.Response;
            await HandleImportSelectionAsync(action);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in import selection: {ex.Message}");
            SendAlert("Error", $"Failed to open import page: {ex.Message}");
        }
    }

    [RelayCommand]
    private void EditRoastLevel(RoastLevelViewModel roastLevelViewModel)
    {
        try
        {
            _currentEditRoastLevel = new RoastLevelViewModel
            {
                Id = roastLevelViewModel.Id,
                Name = roastLevelViewModel.Name,
                MinWeightLossPercentage = roastLevelViewModel.MinWeightLossPercentage,
                MaxWeightLossPercentage = roastLevelViewModel.MaxWeightLossPercentage
            };

            _isNewRoastLevel = false;
            EditPopupTitle = "Edit Roast Level";
            RoastLevelName = _currentEditRoastLevel.Name;
            MinWeightLossText = _currentEditRoastLevel.MinWeightLossPercentage.ToString("F1");
            MaxWeightLossText = _currentEditRoastLevel.MaxWeightLossPercentage.ToString("F1");
            IsEditRoastLevelPopupVisible = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error editing roast level: {ex.Message}");
            SendAlert("Error", $"Failed to edit roast level: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteRoastLevelAsync(RoastLevelViewModel roastLevelViewModel)
    {
        try
        {
            var message = _messenger.Send(new SettingsConfirmationRequestMessage(
                "Confirm Delete",
                $"Are you sure you want to delete the roast level '{roastLevelViewModel.Name}'?",
                "Yes",
                "No"));

            bool confirm = await message.Response;
            if (confirm)
            {
                await DeleteRoastLevelCoreAsync(roastLevelViewModel);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error requesting roast level deletion: {ex.Message}");
            SendAlert("Error", $"Failed to delete roast level: {ex.Message}");
        }
    }

    [RelayCommand]
    private void AddRoastLevel()
    {
        _currentEditRoastLevel = new RoastLevelViewModel
        {
            Id = Guid.NewGuid(),
            Name = string.Empty,
            MinWeightLossPercentage = 0,
            MaxWeightLossPercentage = 0
        };

        _isNewRoastLevel = true;
        EditPopupTitle = "Add Roast Level";
        RoastLevelName = string.Empty;
        MinWeightLossText = "0.0";
        MaxWeightLossText = "0.0";
        IsEditRoastLevelPopupVisible = true;
    }

    [RelayCommand]
    private async Task SaveRoastLevelAsync()
    {
        if (_currentEditRoastLevel == null)
        {
            IsEditRoastLevelPopupVisible = false;
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(RoastLevelName))
            {
                SendAlert("Validation Error", "Name is required.");
                return;
            }

            if (!double.TryParse(MinWeightLossText, out double minWeight))
            {
                SendAlert("Validation Error", "Min weight loss must be a valid number.");
                return;
            }

            if (!double.TryParse(MaxWeightLossText, out double maxWeight))
            {
                SendAlert("Validation Error", "Max weight loss must be a valid number.");
                return;
            }

            if (minWeight < 0)
            {
                SendAlert("Validation Error", "Minimum weight loss percentage must be at least 0.");
                return;
            }

            if (maxWeight <= minWeight)
            {
                SendAlert("Validation Error", "Maximum weight loss must be greater than minimum weight loss.");
                return;
            }

            _currentEditRoastLevel.Name = RoastLevelName.Trim();
            _currentEditRoastLevel.MinWeightLossPercentage = minWeight;
            _currentEditRoastLevel.MaxWeightLossPercentage = maxWeight;

            bool success = _isNewRoastLevel
                ? await _roastLevelService.AddRoastLevelAsync(_currentEditRoastLevel.ToModel())
                : await _roastLevelService.UpdateRoastLevelAsync(_currentEditRoastLevel.ToModel());

            if (!success)
            {
                SendAlert("Error", "Failed to save roast level. Please try again.");
                return;
            }

            IsEditRoastLevelPopupVisible = false;
            await LoadRoastLevelsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving roast level: {ex.Message}");
            SendAlert("Error", $"An error occurred: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelRoastLevel()
    {
        IsEditRoastLevelPopupVisible = false;
    }

    [RelayCommand]
    private async Task ResetRoastLevelsToDefaultsAsync()
    {
        try
        {
            var message = _messenger.Send(new SettingsConfirmationRequestMessage(
                "Reset Roast Levels",
                "Restore the default roast levels and overwrite your current custom list?",
                "Reset",
                "Cancel"));

            bool confirm = await message.Response;
            if (confirm)
            {
                await ResetRoastLevelsToDefaultsCoreAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error requesting roast level reset: {ex.Message}");
            SendAlert("Error", $"Failed to reset roast levels: {ex.Message}");
        }
    }

    private async Task CreateNewDataFileAtPathAsync(string filePath)
    {
        await _preferencesService.SaveAppDataFilePathAsync(filePath);
        await _appDataService.CreateEmptyDataFileAsync(filePath);
        await _preferencesService.SetFirstRunCompletedAsync();

        LoadDataFilePath();
        await LoadRoastLevelsAsync();
        SendAlert("Success", "Created new data file.");
    }

    private async Task HandleImportSelectionAsync(string? action)
    {
        switch (action)
        {
            case "Coffee Beans":
                await _navigationService.GoToAsync(Routes.BeanImport);
                break;
            case "Roast Logs":
                await _navigationService.GoToAsync(Routes.RoastImport);
                break;
        }
    }

    private void LoadDataFilePath()
    {
        DataFilePath = string.IsNullOrWhiteSpace(_appDataService.DataFilePath)
            ? "No data file selected"
            : _appDataService.DataFilePath;
    }

    private void LoadVersionInfo()
    {
        try
        {
            string version = AppInfo.Current.VersionString;
            string build = AppInfo.Current.BuildString;
            VersionDisplay = $"{version} (Build {build})";

            var versionHistory = new StringBuilder();

            if (Microsoft.Maui.ApplicationModel.VersionTracking.IsFirstLaunchForCurrentVersion)
            {
                versionHistory.AppendLine("This is the first time running this version.");
                versionHistory.AppendLine();
            }

            versionHistory.AppendLine($"First installed version: {Microsoft.Maui.ApplicationModel.VersionTracking.FirstInstalledVersion}");

            List<string> versions = Microsoft.Maui.ApplicationModel.VersionTracking.VersionHistory.ToList();
            if (versions.Count > 0)
            {
                versionHistory.AppendLine();
                versionHistory.AppendLine("Version History:");

                foreach (string historicVersion in versions.Take(5))
                {
                    versionHistory.AppendLine($"- {historicVersion}");
                }

                if (versions.Count > 5)
                {
                    versionHistory.AppendLine($"(+ {versions.Count - 5} more versions)");
                }
            }

            VersionHistoryDisplay = versionHistory.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading version info: {ex.Message}");
            VersionDisplay = "Unavailable";
            VersionHistoryDisplay = string.Empty;
        }
    }

    private async Task LoadThemeSettingsAsync()
    {
        try
        {
            _isLoadingThemeSettings = true;
            ThemePreference theme = await _preferencesService.GetThemePreferenceAsync();
            SelectedThemeIndex = MapThemePreferenceToIndex(theme);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading theme settings: {ex.Message}");
            SelectedThemeIndex = 0;
        }
        finally
        {
            _isThemeInitialized = true;
            _isLoadingThemeSettings = false;
        }
    }

    private async Task UpdateThemeAsync(int selectedIndex)
    {
        try
        {
            ThemePreference selectedTheme = MapIndexToThemePreference(selectedIndex);
            await _preferencesService.SaveThemePreferenceAsync(selectedTheme);
            ApplyTheme(selectedTheme);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating theme: {ex.Message}");
            SendAlert("Error", $"Failed to change the theme: {ex.Message}");
        }
    }

    private void ApplyTheme(ThemePreference theme)
    {
        if (Application.Current == null)
        {
            return;
        }

        switch (theme)
        {
            case ThemePreference.Light:
                Application.Current.UserAppTheme = MauiAppTheme.Light;
                (Application.Current as App)?.SetTheme("Light");
                break;
            case ThemePreference.Dark:
                Application.Current.UserAppTheme = MauiAppTheme.Dark;
                (Application.Current as App)?.SetTheme("Dark");
                break;
            default:
                Application.Current.UserAppTheme = MauiAppTheme.Unspecified;
                (Application.Current as App)?.SetTheme("System");
                break;
        }
    }

    private async Task LoadRoastLevelsAsync()
    {
        try
        {
            List<RoastLevelData> roastLevels = await _roastLevelService.GetRoastLevelsAsync();
            RoastLevels.Clear();

            foreach (RoastLevelData roastLevel in roastLevels.OrderBy(level => level.MinWeightLossPercentage))
            {
                RoastLevels.Add(RoastLevelViewModel.FromModel(roastLevel));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading roast levels: {ex.Message}");
        }
    }

    private void EnsureSubscribed()
    {
        if (_isSubscribed)
        {
            return;
        }

        _appDataService.DataFilePathChanged += HandleDataFilePathChanged;
        _appDataService.DataChanged += HandleAppDataChanged;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _appDataService.DataFilePathChanged -= HandleDataFilePathChanged;
        _appDataService.DataChanged -= HandleAppDataChanged;
        _isSubscribed = false;
    }

    private void HandleDataFilePathChanged(object? sender, string filePath)
    {
        DataFilePath = string.IsNullOrWhiteSpace(filePath) ? "No data file selected" : filePath;
    }

    private void HandleAppDataChanged(object? sender, AppData appData)
    {
        _ = LoadRoastLevelsAsync();
    }

    private void SendAlert(string title, string message, string cancel = "OK")
    {
        _messenger.Send(new SettingsAlertMessage(new SettingsAlertRequest(title, message, cancel)));
    }

    private static int MapThemePreferenceToIndex(ThemePreference theme)
    {
        return theme switch
        {
            ThemePreference.Light => 1,
            ThemePreference.Dark => 2,
            _ => 0
        };
    }

    private static ThemePreference MapIndexToThemePreference(int selectedIndex)
    {
        return selectedIndex switch
        {
            1 => ThemePreference.Light,
            2 => ThemePreference.Dark,
            _ => ThemePreference.System
        };
    }

    private static List<RoastLevelData> CreateDefaultRoastLevels()
    {
        return
        [
            new RoastLevelData("Under Developed", 0.0, 11.0),
            new RoastLevelData("Light", 11.0, 13.0),
            new RoastLevelData("Medium-Light", 13.0, 14.0),
            new RoastLevelData("Medium", 14.0, 16.0),
            new RoastLevelData("Dark", 16.0, 18.0),
            new RoastLevelData("Extra Dark", 18.0, 22.0),
            new RoastLevelData("Burned", 22.0, 100.0)
        ];
    }
}

public readonly record struct SettingsAlertRequest(string Title, string Message, string Cancel);

public sealed class SettingsAlertMessage(SettingsAlertRequest value) : ValueChangedMessage<SettingsAlertRequest>(value);

public sealed class PickDataFileRequestMessage(string pickerTitle) : AsyncRequestMessage<string?>
{
    public string PickerTitle { get; } = pickerTitle;
}

public sealed class SettingsActionSheetRequestMessage(
    string title,
    string cancel,
    params string[] buttons) : AsyncRequestMessage<string?>
{
    public string Title { get; } = title;

    public string Cancel { get; } = cancel;

    public IReadOnlyList<string> Buttons { get; } = buttons;
}

public sealed class SettingsConfirmationRequestMessage(
    string title,
    string message,
    string accept,
    string cancel) : AsyncRequestMessage<bool>
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public string Accept { get; } = accept;

    public string Cancel { get; } = cancel;
}
