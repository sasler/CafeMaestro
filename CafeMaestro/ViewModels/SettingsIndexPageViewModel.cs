using System.Globalization;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

public enum SettingsSection
{
    Roasting,
    Appearance,
    Data,
    RoastLevels,
    About
}

/// <summary>
/// The Settings tab: a short list of destinations, each showing the value it currently holds.
/// Summaries are rebuilt on every appearance so returning from a detail page shows the change
/// that was just made.
/// </summary>
public partial class SettingsIndexPageViewModel : ObservableObject
{
    private readonly IRoastPreferencesService _roastPreferences;
    private readonly IPreferencesService _preferencesService;
    private readonly IAppDataService _appDataService;
    private readonly IRoastLevelService _roastLevelService;
    private readonly IDataBackupService _dataBackupService;
    private readonly INavigationService _navigationService;
    private readonly IAppVersionProvider _versionProvider;

    public SettingsIndexPageViewModel(
        IRoastPreferencesService roastPreferences,
        IPreferencesService preferencesService,
        IAppDataService appDataService,
        IRoastLevelService roastLevelService,
        IDataBackupService dataBackupService,
        INavigationService navigationService,
        IAppVersionProvider versionProvider)
    {
        _roastPreferences = roastPreferences ??
                            throw new ArgumentNullException(nameof(roastPreferences));
        _preferencesService = preferencesService ??
                              throw new ArgumentNullException(nameof(preferencesService));
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _roastLevelService = roastLevelService ??
                             throw new ArgumentNullException(nameof(roastLevelService));
        _dataBackupService = dataBackupService ??
                             throw new ArgumentNullException(nameof(dataBackupService));
        _navigationService = navigationService ??
                             throw new ArgumentNullException(nameof(navigationService));
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
    }

    [ObservableProperty]
    public partial string RoastingSummary { get; set; } = "Loading…";

    [ObservableProperty]
    public partial string AppearanceSummary { get; set; } = "Loading…";

    [ObservableProperty]
    public partial string DataSummary { get; set; } = "Loading…";

    [ObservableProperty]
    public partial string RoastLevelSummary { get; set; } = "Loading…";

    [ObservableProperty]
    public partial string AboutSummary { get; set; } = "Loading…";

    [ObservableProperty]
    public partial bool IsWideLayout { get; set; }

    [ObservableProperty]
    public partial SettingsSection SelectedSection { get; set; } = SettingsSection.Roasting;

    public bool IsRoastingSelected => SelectedSection == SettingsSection.Roasting;
    public bool IsAppearanceSelected => SelectedSection == SettingsSection.Appearance;
    public bool IsDataSelected => SelectedSection == SettingsSection.Data;
    public bool IsRoastLevelsSelected => SelectedSection == SettingsSection.RoastLevels;
    public bool IsAboutSelected => SelectedSection == SettingsSection.About;
    public bool IsRoastingHighlighted => IsWideLayout && IsRoastingSelected;
    public bool IsAppearanceHighlighted => IsWideLayout && IsAppearanceSelected;
    public bool IsDataHighlighted => IsWideLayout && IsDataSelected;
    public bool IsRoastLevelsHighlighted => IsWideLayout && IsRoastLevelsSelected;
    public bool IsAboutHighlighted => IsWideLayout && IsAboutSelected;

    /// <summary>System Back on the Settings tab root returns to Roast, the launch destination.</summary>
    public Task GoBackAsync() => _navigationService.GoToAsync(Routes.Roast);

    public async Task OnAppearingAsync()
    {
        await Task.WhenAll(
            RefreshRoastingSummaryAsync(),
            RefreshAppearanceSummaryAsync(),
            RefreshDataSummaryAsync(),
            RefreshRoastLevelSummaryAsync());
        RefreshAboutSummary();
    }

    public void SetWideLayout(bool isWideLayout)
    {
        IsWideLayout = isWideLayout;
        NotifySelectionState();
    }

    [RelayCommand]
    private Task OpenRoastingAsync() => OpenSectionAsync(SettingsSection.Roasting, Routes.RoastingSettings);

    [RelayCommand]
    private Task OpenAppearanceAsync() => OpenSectionAsync(SettingsSection.Appearance, Routes.AppearanceSettings);

    [RelayCommand]
    private Task OpenDataAsync() => OpenSectionAsync(SettingsSection.Data, Routes.DataSettings);

    [RelayCommand]
    private Task OpenRoastLevelsAsync() => OpenSectionAsync(SettingsSection.RoastLevels, Routes.RoastLevelSettings);

    [RelayCommand]
    private Task OpenAboutAsync() => OpenSectionAsync(SettingsSection.About, Routes.About);

    [RelayCommand]
    private Task OpenSelectedSectionAsync() => _navigationService.GoToAsync(RouteFor(SelectedSection));

    partial void OnSelectedSectionChanged(SettingsSection value) => NotifySelectionState();

    private Task OpenSectionAsync(SettingsSection section, string route)
    {
        if (IsWideLayout)
        {
            SelectedSection = section;
            return Task.CompletedTask;
        }

        return _navigationService.GoToAsync(route);
    }

    private void NotifySelectionState()
    {
        OnPropertyChanged(nameof(IsRoastingSelected));
        OnPropertyChanged(nameof(IsAppearanceSelected));
        OnPropertyChanged(nameof(IsDataSelected));
        OnPropertyChanged(nameof(IsRoastLevelsSelected));
        OnPropertyChanged(nameof(IsAboutSelected));
        OnPropertyChanged(nameof(IsRoastingHighlighted));
        OnPropertyChanged(nameof(IsAppearanceHighlighted));
        OnPropertyChanged(nameof(IsDataHighlighted));
        OnPropertyChanged(nameof(IsRoastLevelsHighlighted));
        OnPropertyChanged(nameof(IsAboutHighlighted));
    }

    private static string RouteFor(SettingsSection section) => section switch
    {
        SettingsSection.Roasting => Routes.RoastingSettings,
        SettingsSection.Appearance => Routes.AppearanceSettings,
        SettingsSection.Data => Routes.DataSettings,
        SettingsSection.RoastLevels => Routes.RoastLevelSettings,
        _ => Routes.About
    };

    private async Task RefreshRoastingSummaryAsync()
    {
        bool firstCrack = await _roastPreferences.GetFirstCrackEnabledAsync();
        int coolingSeconds = await _roastPreferences.GetCoolingDurationSecondsAsync();
        RoastingSummary = DescribeRoasting(firstCrack, coolingSeconds);
    }

    private async Task RefreshAppearanceSummaryAsync()
    {
        AppearanceSummary = AppearanceSettingsPageViewModel.DescribeTheme(
            await _preferencesService.GetThemePreferenceAsync());
    }

    private async Task RefreshDataSummaryAsync()
    {
        AppData data = _appDataService.CurrentData;
        DateTime? lastBackup = null;
        try
        {
            IReadOnlyList<DataBackupSummary> backups =
                await _dataBackupService.GetSafetyBackupsAsync();
            lastBackup = backups
                .Where(backup => backup.CreatedAt != default)
                .Select(backup => (DateTime?)backup.CreatedAt)
                .Max();
        }
        catch (Exception ex)
        {
            // A summary line must never block the index from rendering.
            System.Diagnostics.Debug.WriteLine($"Backup summary unavailable: {ex.Message}");
        }

        DataSummary = DescribeData(
            data.Beans?.Count ?? 0,
            data.RoastLogs?.Count ?? 0,
            lastBackup,
            DateTime.Now);
    }

    private async Task RefreshRoastLevelSummaryAsync()
    {
        List<RoastLevelData> levels = await _roastLevelService.GetRoastLevelsAsync();
        RoastLevelSummary = RoastLevelSettingsPageViewModel.DescribeCount(levels.Count);
    }

    private void RefreshAboutSummary()
    {
        try
        {
            AboutSummary = $"Version {_versionProvider.VersionString}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Version summary unavailable: {ex.Message}");
            AboutSummary = "Version and licence";
        }
    }

    internal static string DescribeRoasting(bool firstCrackEnabled, int coolingSeconds)
    {
        string firstCrack = firstCrackEnabled ? "First Crack on" : "First Crack off";
        int minutes = coolingSeconds / 60;
        string cooling = coolingSeconds <= 0
            ? "No cooling countdown"
            : minutes >= 1
                ? $"Cooling {minutes} min"
                : $"Cooling {coolingSeconds} s";
        string precision = string.Format(
            CultureInfo.CurrentCulture,
            "{0:0.0} g",
            RoastPreferenceDefaults.WeightPrecisionGrams);
        return $"{firstCrack} · {cooling} · {precision}";
    }

    internal static string DescribeData(
        int beanCount,
        int roastCount,
        DateTime? lastBackupLocal,
        DateTime now)
    {
        string beans = beanCount == 1 ? "1 bean" : $"{beanCount} beans";
        string roasts = roastCount == 1 ? "1 roast" : $"{roastCount} roasts";
        if (lastBackupLocal is not { } backup)
        {
            return $"{beans} · {roasts} · no backup yet";
        }

        DateTime backupLocal = backup.Kind == DateTimeKind.Utc ? backup.ToLocalTime() : backup;
        int days = (now.Date - backupLocal.Date).Days;
        string when = days switch
        {
            <= 0 => "backed up today",
            1 => "backed up yesterday",
            < 7 => $"backed up {days} days ago",
            _ => $"backed up {backupLocal.ToString("d MMM", CultureInfo.CurrentCulture)}"
        };

        return $"{beans} · {roasts} · {when}";
    }
}
