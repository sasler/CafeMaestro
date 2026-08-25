using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiAppTheme = Microsoft.Maui.ApplicationModel.AppTheme;

namespace CafeMaestro.ViewModels;

public partial class DataSettingsPageViewModel : ObservableObject
{
    private readonly IPreferencesService _preferencesService;
    private readonly IAppDataService _appDataService;
    private readonly IDataBackupService _dataBackupService;
    private readonly IUserFileService _userFileService;
    private readonly IRoastDataService _roastDataService;
    private readonly IRoastLevelService _roastLevelService;
    private readonly INavigationService _navigationService;
    private readonly IShareService _shareService;
    private readonly IAlertService _alertService;
    private bool _isLoadingThemeSettings;
    private bool _isThemeInitialized;
    private bool _isSubscribed;
    private RoastLevelViewModel? _currentEditRoastLevel;
    private bool _isNewRoastLevel;

    public DataSettingsPageViewModel(
        IPreferencesService preferencesService,
        IAppDataService appDataService,
        IDataBackupService dataBackupService,
        IUserFileService userFileService,
        IRoastDataService roastDataService,
        IRoastLevelService roastLevelService,
        INavigationService navigationService,
        IShareService shareService,
        IAlertService alertService)
    {
        _preferencesService = preferencesService ??
                              throw new ArgumentNullException(nameof(preferencesService));
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _dataBackupService = dataBackupService ??
                             throw new ArgumentNullException(nameof(dataBackupService));
        _userFileService = userFileService ??
                           throw new ArgumentNullException(nameof(userFileService));
        _roastDataService = roastDataService ??
                            throw new ArgumentNullException(nameof(roastDataService));
        _roastLevelService = roastLevelService ??
                             throw new ArgumentNullException(nameof(roastLevelService));
        _navigationService = navigationService ??
                             throw new ArgumentNullException(nameof(navigationService));
        _shareService = shareService ?? throw new ArgumentNullException(nameof(shareService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
    }

    [ObservableProperty]
    public partial string DataStatusDisplay { get; set; } =
        "Saved automatically on this device";

    [ObservableProperty]
    public partial string DataSummaryDisplay { get; set; } = "Beans: 0  •  Roasts: 0";

    [ObservableProperty]
    public partial string LastModifiedDisplay { get; set; } = "Last modified: —";

    [ObservableProperty]
    public partial bool IsDataOperationInProgress { get; set; }

    [ObservableProperty]
    public partial bool HasAutomaticBackups { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<DataBackupSummary> AutomaticBackups { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<RoastLevelViewModel> RoastLevels { get; set; } = [];

    [ObservableProperty]
    public partial bool IsEditRoastLevelPopupVisible { get; set; }

    [ObservableProperty]
    public partial string EditPopupTitle { get; set; } = "Edit Roast Level";

    [ObservableProperty]
    public partial string RoastLevelName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MinWeightLossText { get; set; } = "0.0";

    [ObservableProperty]
    public partial string MaxWeightLossText { get; set; } = "0.0";

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial string VersionDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VersionHistoryDisplay { get; set; } = string.Empty;

    public bool CanRunDataOperation => !IsDataOperationInProgress;

    public bool ShouldHighlightDataFileSection => false;

    partial void OnIsDataOperationInProgressChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunDataOperation));
        StartNewDataCommand.NotifyCanExecuteChanged();
        SaveBackupCommand.NotifyCanExecuteChanged();
        RestoreFromBackupCommand.NotifyCanExecuteChanged();
        ShareBackupCommand.NotifyCanExecuteChanged();
        RestoreSafetyBackupCommand.NotifyCanExecuteChanged();
        SaveRecoveryCopyCommand.NotifyCanExecuteChanged();
        ImportCoffeeBeansCommand.NotifyCanExecuteChanged();
        ImportRoastLogsCommand.NotifyCanExecuteChanged();
        ExportRoastLogCsvCommand.NotifyCanExecuteChanged();
        ShareRoastLogCsvCommand.NotifyCanExecuteChanged();
    }

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
        RefreshDataStatus(_appDataService.CurrentData);
        LoadVersionInfo();
        await LoadThemeSettingsAsync();
        await Task.WhenAll(LoadRoastLevelsAsync(), LoadAutomaticBackupsAsync());
    }

    public void OnDisappearing()
    {
        Unsubscribe();
    }

    public void MarkDataFileSectionHighlighted()
    {
    }

    public Task GoBackAsync() => _navigationService.GoToAsync(Routes.Roast);

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private async Task StartNewDataAsync()
    {
        bool confirmed = await _alertService.ShowConfirmationAsync(
            "Start New Data",
            "Start with an empty CafeMaestro dataset? Your current data will first be kept in Automatic Backups.",
            "Start New",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        await RunDataOperationAsync(async () =>
        {
            AppData data = await _dataBackupService.StartNewDataAsync();
            RefreshDataStatus(data);
            await LoadAutomaticBackupsAsync();
            await LoadRoastLevelsAsync();
            await _alertService.ShowAlertAsync(
                "New Data Ready",
                "A new dataset is active. Your previous data is available under Automatic Backups.",
                "OK");
        }, "CafeMaestro could not start a new dataset.");
    }

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private async Task SaveBackupAsync()
    {
        await RunDataOperationAsync(async () =>
        {
            await using Stream stream = await _dataBackupService.CreateExportStreamAsync();
            DocumentSaveResult result = await _userFileService.SaveFileAsync(
                $"CafeMaestro_Backup_{DateTime.Now:yyyy-MM-dd_HHmm}.json",
                "application/json",
                stream);
            if (result.IsCanceled)
            {
                return;
            }

            if (!result.IsSuccessful)
            {
                throw result.Exception ?? new IOException("The backup could not be saved.");
            }

            await _alertService.ShowAlertAsync(
                "Backup Saved",
                "A copy of your CafeMaestro data was saved. Your active data remains on this device.",
                "OK");
        }, "CafeMaestro could not save the backup.");
    }

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private async Task RestoreFromBackupAsync()
    {
        UserFileSelection? selection = null;

        await RunDataOperationAsync(async () =>
        {
            selection = await _userFileService.PickFileAsync(
                UserFileType.Json,
                "Select a CafeMaestro backup");
            if (selection is null)
            {
                return;
            }

            DataBackupSummary preview =
                await _dataBackupService.PreviewExternalBackupAsync(selection.LocalPath);
            bool confirmed = await _alertService.ShowConfirmationAsync(
                "Restore Backup",
                $"Restore “{selection.DisplayName}”?\n\n" +
                $"{preview.BeanCount} beans • {preview.RoastCount} roasts\n" +
                $"Last modified: {FormatDate(preview.LastModified)}\n" +
                $"Created by CafeMaestro {preview.AppVersion}\n\n" +
                "Your current data will be kept in Automatic Backups. The selected file will not be changed.",
                "Restore",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            AppData restored =
                await _dataBackupService.RestoreExternalBackupAsync(selection.LocalPath);
            RefreshDataStatus(restored);
            await LoadAutomaticBackupsAsync();
            await LoadRoastLevelsAsync();
            await _alertService.ShowAlertAsync(
                "Backup Restored",
                "The backup was copied into CafeMaestro. The selected source file was not changed.",
                "OK");
        }, "CafeMaestro could not restore the selected backup.");

        _userFileService.DeleteTemporaryFile(selection?.LocalPath);
    }

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private async Task ShareBackupAsync()
    {
        await RunDataOperationAsync(async () =>
        {
            if (!_appDataService.DataFileExists())
            {
                throw new FileNotFoundException("The current CafeMaestro data file is unavailable.");
            }

            await _shareService.ShareFileAsync(
                _appDataService.DataFilePath,
                "Share CafeMaestro Backup");
        }, "CafeMaestro could not share the backup.");
    }

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private async Task RestoreSafetyBackupAsync(DataBackupSummary backup)
    {
        if (backup is null || !backup.IsRestorable)
        {
            return;
        }

        bool confirmed = await _alertService.ShowConfirmationAsync(
            "Restore Previous Data",
            $"Restore the automatic backup from {FormatDate(backup.CreatedAt)}?\n\n" +
            $"{backup.BeanCount} beans • {backup.RoastCount} roasts\n\n" +
            "Your current data will also be kept as a new automatic backup.",
            "Restore",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        await RunDataOperationAsync(async () =>
        {
            AppData restored = await _dataBackupService.RestoreSafetyBackupAsync(backup.Id);
            RefreshDataStatus(restored);
            await LoadAutomaticBackupsAsync();
            await LoadRoastLevelsAsync();
            await _alertService.ShowAlertAsync(
                "Previous Data Restored",
                "The selected automatic backup is now active.",
                "OK");
        }, "CafeMaestro could not restore the automatic backup.");
    }

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private async Task SaveRecoveryCopyAsync(DataBackupSummary backup)
    {
        if (backup is null || !backup.IsRawRecovery)
        {
            return;
        }

        await RunDataOperationAsync(async () =>
        {
            await using Stream stream =
                await _dataBackupService.CreateSafetyBackupExportStreamAsync(backup.Id);
            DocumentSaveResult result = await _userFileService.SaveFileAsync(
                $"CafeMaestro_Raw_Recovery_{backup.CreatedAt:yyyy-MM-dd_HHmmss}.json",
                "application/json",
                stream);
            if (result.IsCanceled)
            {
                return;
            }

            if (!result.IsSuccessful)
            {
                throw result.Exception ?? new IOException("The recovery copy could not be saved.");
            }

            await _alertService.ShowAlertAsync(
                "Recovery Copy Saved",
                "The original recovery JSON was saved unchanged for manual repair or support.",
                "OK");
        }, "CafeMaestro could not save the raw recovery copy.");
    }

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private Task ImportCoffeeBeansAsync() =>
        _navigationService.GoToAsync(Routes.BeanImport);

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private Task ImportRoastLogsAsync() =>
        _navigationService.GoToAsync(Routes.RoastImport);

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private async Task ExportRoastLogCsvAsync()
    {
        await RunDataOperationAsync(async () =>
        {
            await using var stream = new MemoryStream();
            await _roastDataService.ExportRoastLogAsync(stream);
            stream.Position = 0;
            DocumentSaveResult result = await _userFileService.SaveFileAsync(
                $"CafeMaestro_RoastLog_{DateTime.Now:yyyy-MM-dd}.csv",
                "text/csv",
                stream);
            if (result.IsCanceled)
            {
                return;
            }

            if (!result.IsSuccessful)
            {
                throw result.Exception ?? new IOException("The roast log could not be saved.");
            }

            await _alertService.ShowAlertAsync(
                "Roast Log Exported",
                "The roast log CSV was saved successfully.",
                "OK");
        }, "CafeMaestro could not export the roast log.");
    }

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private async Task ShareRoastLogCsvAsync()
    {
        string temporaryPath = Path.Combine(
            FileSystem.CacheDirectory,
            $"CafeMaestro_RoastLog_{DateTime.Now:yyyy-MM-dd}.csv");

        await RunDataOperationAsync(async () =>
        {
            await using var source = new MemoryStream();
            await _roastDataService.ExportRoastLogAsync(source);
            source.Position = 0;
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             useAsync: true))
            {
                await source.CopyToAsync(destination);
            }

            await _shareService.ShareFileAsync(temporaryPath, "Share Roast Log CSV");
        }, "CafeMaestro could not share the roast log.");
    }

    [RelayCommand]
    private void EditRoastLevel(RoastLevelViewModel roastLevel)
    {
        _currentEditRoastLevel = new RoastLevelViewModel
        {
            Id = roastLevel.Id,
            Name = roastLevel.Name,
            MinWeightLossPercentage = roastLevel.MinWeightLossPercentage,
            MaxWeightLossPercentage = roastLevel.MaxWeightLossPercentage
        };
        _isNewRoastLevel = false;
        EditPopupTitle = "Edit Roast Level";
        RoastLevelName = _currentEditRoastLevel.Name;
        MinWeightLossText = _currentEditRoastLevel.MinWeightLossPercentage.ToString(
            "F1",
            CultureInfo.InvariantCulture);
        MaxWeightLossText = _currentEditRoastLevel.MaxWeightLossPercentage.ToString(
            "F1",
            CultureInfo.InvariantCulture);
        IsEditRoastLevelPopupVisible = true;
    }

    [RelayCommand]
    private async Task DeleteRoastLevelAsync(RoastLevelViewModel roastLevel)
    {
        bool confirmed = await _alertService.ShowConfirmationAsync(
            "Delete Roast Level",
            $"Delete “{roastLevel.Name}”?",
            "Delete",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        bool success = await _roastLevelService.DeleteRoastLevelAsync(roastLevel.Id);
        if (!success)
        {
            await _alertService.ShowAlertAsync(
                "Delete Failed",
                "CafeMaestro could not delete the roast level.",
                "OK");
            return;
        }

        await LoadRoastLevelsAsync();
    }

    [RelayCommand]
    private void AddRoastLevel()
    {
        _currentEditRoastLevel = new RoastLevelViewModel
        {
            Id = Guid.NewGuid()
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
        if (_currentEditRoastLevel is null)
        {
            IsEditRoastLevelPopupVisible = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(RoastLevelName) ||
            !double.TryParse(
                MinWeightLossText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double minimum) ||
            !double.TryParse(
                MaxWeightLossText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double maximum) ||
            minimum < 0 ||
            maximum <= minimum)
        {
            await _alertService.ShowAlertAsync(
                "Invalid Roast Level",
                "Enter a name and a valid range where the maximum is greater than the minimum.",
                "OK");
            return;
        }

        _currentEditRoastLevel.Name = RoastLevelName.Trim();
        _currentEditRoastLevel.MinWeightLossPercentage = minimum;
        _currentEditRoastLevel.MaxWeightLossPercentage = maximum;
        bool success = _isNewRoastLevel
            ? await _roastLevelService.AddRoastLevelAsync(_currentEditRoastLevel.ToModel())
            : await _roastLevelService.UpdateRoastLevelAsync(_currentEditRoastLevel.ToModel());

        if (!success)
        {
            await _alertService.ShowAlertAsync(
                "Save Failed",
                "CafeMaestro could not save the roast level.",
                "OK");
            return;
        }

        IsEditRoastLevelPopupVisible = false;
        await LoadRoastLevelsAsync();
    }

    [RelayCommand]
    private void CancelRoastLevel()
    {
        IsEditRoastLevelPopupVisible = false;
    }

    [RelayCommand]
    private async Task ResetRoastLevelsToDefaultsAsync()
    {
        bool confirmed = await _alertService.ShowConfirmationAsync(
            "Reset Roast Levels",
            "Restore the default roast levels and replace the current custom list?",
            "Reset",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        bool success = await _roastLevelService.SaveRoastLevelsAsync(
            AppDataFactory.CreateDefault().RoastLevels);
        if (!success)
        {
            await _alertService.ShowAlertAsync(
                "Reset Failed",
                "CafeMaestro could not restore the default roast levels.",
                "OK");
            return;
        }

        await LoadRoastLevelsAsync();
    }

    private async Task RunDataOperationAsync(Func<Task> operation, string failureMessage)
    {
        if (IsDataOperationInProgress)
        {
            return;
        }

        IsDataOperationInProgress = true;
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            // User cancellation is not an error.
        }
        catch (Exception ex)
        {
            await _alertService.ShowAlertAsync(
                "File Operation Failed",
                $"{failureMessage}\n\n{ex.Message}",
                "OK");
        }
        finally
        {
            IsDataOperationInProgress = false;
        }
    }

    private async Task LoadAutomaticBackupsAsync()
    {
        IReadOnlyList<DataBackupSummary> backups =
            await _dataBackupService.GetSafetyBackupsAsync();
        AutomaticBackups = new ObservableCollection<DataBackupSummary>(backups);
        HasAutomaticBackups = AutomaticBackups.Count > 0;
    }

    private async Task LoadRoastLevelsAsync()
    {
        List<RoastLevelData> roastLevels = await _roastLevelService.GetRoastLevelsAsync();
        RoastLevels = new ObservableCollection<RoastLevelViewModel>(
            roastLevels
                .OrderBy(level => level.MinWeightLossPercentage)
                .Select(RoastLevelViewModel.FromModel));
    }

    private void RefreshDataStatus(AppData data)
    {
        if (_appDataService.IsRecoveryRequired)
        {
            DataStatusDisplay = "Recovery required";
            DataSummaryDisplay = "The active data file could not be loaded safely.";
            LastModifiedDisplay = "Use Share Backup to preserve the original file before recovery.";
            return;
        }

        DataStatusDisplay = "Saved automatically on this device";
        DataSummaryDisplay =
            $"Beans: {data.Beans?.Count ?? 0}  •  Roasts: {data.RoastLogs?.Count ?? 0}";
        LastModifiedDisplay = $"Last modified: {FormatDate(data.LastModified)}";
    }

    private void EnsureSubscribed()
    {
        if (_isSubscribed)
        {
            return;
        }

        _appDataService.DataChanged += HandleAppDataChanged;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _appDataService.DataChanged -= HandleAppDataChanged;
        _isSubscribed = false;
    }

    private void HandleAppDataChanged(object? sender, AppData data)
    {
        RefreshDataStatus(data);
    }

    private async Task LoadThemeSettingsAsync()
    {
        _isLoadingThemeSettings = true;
        try
        {
            SelectedThemeIndex = MapThemePreferenceToIndex(
                await _preferencesService.GetThemePreferenceAsync());
        }
        finally
        {
            _isThemeInitialized = true;
            _isLoadingThemeSettings = false;
        }
    }

    private async Task UpdateThemeAsync(int selectedIndex)
    {
        ThemePreference selectedTheme = MapIndexToThemePreference(selectedIndex);
        await _preferencesService.SaveThemePreferenceAsync(selectedTheme);
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.UserAppTheme = selectedTheme switch
        {
            ThemePreference.Light => MauiAppTheme.Light,
            ThemePreference.Dark => MauiAppTheme.Dark,
            _ => MauiAppTheme.Unspecified
        };
        (Application.Current as App)?.SetTheme(selectedTheme.ToString());
    }

    private void LoadVersionInfo()
    {
        try
        {
            VersionDisplay = $"{AppInfo.Current.VersionString} (Build {AppInfo.Current.BuildString})";
            var versionHistory = new StringBuilder();
            versionHistory.AppendLine(
                $"First installed version: {VersionTracking.FirstInstalledVersion}");
            List<string> versions = VersionTracking.VersionHistory.ToList();
            if (versions.Count > 0)
            {
                versionHistory.AppendLine();
                versionHistory.AppendLine("Version History:");
                foreach (string version in versions.Take(5))
                {
                    versionHistory.AppendLine($"- {version}");
                }
            }

            VersionHistoryDisplay = versionHistory.ToString().TrimEnd();
        }
        catch
        {
            VersionDisplay = "Unavailable";
            VersionHistoryDisplay = string.Empty;
        }
    }

    private static string FormatDate(DateTime value)
    {
        if (value == default)
        {
            return "Unknown";
        }

        return value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    private static int MapThemePreferenceToIndex(ThemePreference theme) =>
        theme switch
        {
            ThemePreference.Light => 1,
            ThemePreference.Dark => 2,
            _ => 0
        };

    private static ThemePreference MapIndexToThemePreference(int index) =>
        index switch
        {
            1 => ThemePreference.Light,
            2 => ThemePreference.Dark,
            _ => ThemePreference.System
        };
}
