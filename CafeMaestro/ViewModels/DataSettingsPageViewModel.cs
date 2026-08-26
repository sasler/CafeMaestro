using System.Collections.ObjectModel;
using System.Globalization;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

/// <summary>
/// Data &amp; Backups. The backup, restore, reset and CSV transfer operations are unchanged;
/// they gained an active-roast guard so a replacement cannot land under a running roast.
/// </summary>
public partial class DataSettingsPageViewModel : ObservableObject, IQueryAttributable
{
    public const string PersistenceRecoveryKey = "PersistenceRecovery";
    private readonly IAppDataService _appDataService;
    private readonly IDataBackupService _dataBackupService;
    private readonly IUserFileService _userFileService;
    private readonly IRoastDataService _roastDataService;
    private readonly IRoastSessionService _roastSessionService;
    private readonly INavigationService _navigationService;
    private readonly IShareService _shareService;
    private readonly IAlertService _alertService;
    private bool _isSubscribed;
    private bool _isPersistenceRecovery;

    public DataSettingsPageViewModel(
        IAppDataService appDataService,
        IDataBackupService dataBackupService,
        IUserFileService userFileService,
        IRoastDataService roastDataService,
        IRoastSessionService roastSessionService,
        INavigationService navigationService,
        IShareService shareService,
        IAlertService alertService)
    {
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _dataBackupService = dataBackupService ??
                             throw new ArgumentNullException(nameof(dataBackupService));
        _userFileService = userFileService ??
                           throw new ArgumentNullException(nameof(userFileService));
        _roastDataService = roastDataService ??
                            throw new ArgumentNullException(nameof(roastDataService));
        _roastSessionService = roastSessionService ??
                               throw new ArgumentNullException(nameof(roastSessionService));
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

    public bool CanRunDataOperation => !IsDataOperationInProgress;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _isPersistenceRecovery = query.TryGetValue(PersistenceRecoveryKey, out object? value) &&
            bool.TryParse(value?.ToString(), out bool enabled) &&
            enabled;
    }

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

    public async Task OnAppearingAsync()
    {
        EnsureSubscribed();
        RefreshDataStatus(_appDataService.CurrentData);
        await LoadAutomaticBackupsAsync();
    }

    public void OnDisappearing()
    {
        Unsubscribe();
    }

    public Task GoBackAsync() => _navigationService.GoBackAsync();

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private async Task StartNewDataAsync()
    {
        if (await IsBlockedByActiveRoastAsync("Start New Data"))
        {
            return;
        }

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
        if (await IsBlockedByActiveRoastAsync("Restore Backup"))
        {
            return;
        }

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

        if (await IsBlockedByActiveRoastAsync("Restore Previous Data"))
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
        _navigationService.GoToAsync(
            Routes.Import,
            new Dictionary<string, object> { [ImportPageViewModel.KindParameter] = ImportKind.Beans });

    [RelayCommand(CanExecute = nameof(CanRunDataOperation))]
    private Task ImportRoastLogsAsync() =>
        _navigationService.GoToAsync(
            Routes.Import,
            new Dictionary<string, object> { [ImportPageViewModel.KindParameter] = ImportKind.Roasts });

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

    /// <summary>
    /// Replacing the dataset under a running roast would strand the draft that the session
    /// service owns, so the operation stops here and says so. A snapshot that cannot be read
    /// is treated as "a roast may be running" rather than waved through.
    /// </summary>
    private async Task<bool> IsBlockedByActiveRoastAsync(string operationTitle)
    {
        bool roastInProgress;
        try
        {
            RoastSessionSnapshot snapshot = await _roastSessionService.GetSnapshotAsync();
            roastInProgress = snapshot.HasActiveRoast || snapshot.RequiresRecovery;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Active-roast check failed: {ex.Message}");
            roastInProgress = true;
        }

        if (!roastInProgress)
        {
            return false;
        }

        if (_isPersistenceRecovery)
        {
            bool continueRecovery = await _alertService.ShowConfirmationAsync(
                "Persistence Recovery",
                "CafeMaestro could not save the active roast. Replacing the dataset will abandon " +
                "that unsaved in-memory roast. Continue only if Retry cannot recover it.",
                "Continue",
                "Cancel");
            return !continueRecovery;
        }

        await _alertService.ShowAlertAsync(
            operationTitle,
            "A roast is still in progress. Drop or discard it on the Roast tab before replacing " +
            "your data, so nothing about the running batch is lost.",
            "OK");
        return true;
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

    private static string FormatDate(DateTime value)
    {
        if (value == default)
        {
            return "Unknown";
        }

        return value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }
}
