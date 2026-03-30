using System.Collections.ObjectModel;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

public partial class RoastLogPageViewModel : ObservableObject
{
    private readonly IRoastDataService _roastDataService;
    private readonly IAppDataService _appDataService;
    private readonly IPreferencesService _preferencesService;
    private readonly INavigationService _navigationService;
    private readonly List<RoastData> _allRoasts = [];
    private bool _isSubscribed;
    private bool _hasInitializedPath;

    [ObservableProperty]
    public partial ObservableCollection<RoastData> Roasts { get; set; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial RoastData? SelectedRoast { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial int RecordCount { get; set; }

    public RoastLogPageViewModel(
        IRoastDataService roastDataService,
        IAppDataService appDataService,
        IPreferencesService preferencesService,
        INavigationService navigationService)
    {
        _roastDataService = roastDataService ?? throw new ArgumentNullException(nameof(roastDataService));
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    public Func<string, string, string, Task>? AlertAsync { get; set; }

    public Func<string, string, string?, string[], Task<string>>? ActionSheetAsync { get; set; }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    public async Task OnAppearingAsync()
    {
        EnsureSubscribed();
        await InitializeWithCorrectPathAsync();
        await RefreshAsync();
    }

    public void OnDisappearing()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _appDataService.DataChanged -= HandleAppDataChanged;
        _isSubscribed = false;
    }

    public Task NavigateHomeAsync()
    {
        return _navigationService.GoToAsync(Routes.Main);
    }

    [RelayCommand]
    private Task SearchAsync()
    {
        ApplyFilter();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task AddRoastAsync()
    {
        try
        {
            await _navigationService.GoToAsync(
                Routes.Roast,
                new Dictionary<string, object>
                {
                    ["NewRoast"] = "true"
                });
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Error", $"Could not navigate to roast page: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task EditRoastAsync(RoastData? roast)
    {
        roast ??= SelectedRoast;

        if (roast is null)
        {
            return;
        }

        try
        {
            await _navigationService.GoToAsync(
                Routes.Roast,
                new Dictionary<string, object>
                {
                    ["EditRoastId"] = roast.Id.ToString()
                });
        }
        catch
        {
            await ShowAlertAsync("Error", "Error preparing to edit roast", "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteRoastAsync(RoastData? roast)
    {
        roast ??= SelectedRoast;

        if (roast is null)
        {
            return;
        }

        try
        {
            bool success = await _roastDataService.DeleteRoastLogAsync(roast.Id);

            if (success)
            {
                await RefreshAsync();
                return;
            }

            await ShowAlertAsync("Error", "Failed to delete roast log", "OK");
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Error", $"Failed to delete roast log: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private Task ExportLogAsync()
    {
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task NavigateToImportAsync()
    {
        try
        {
            await _navigationService.GoToAsync(Routes.RoastImport);
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Error", $"Could not navigate to import page: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            var roasts = await _roastDataService.GetAllRoastLogsAsync();
            SetRoasts(roasts);
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Error", $"Failed to load roast logs: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ShowRoastActionsAsync(RoastData? roast)
    {
        if (roast is null || ActionSheetAsync is null)
        {
            return;
        }

        SelectedRoast = roast;

        string action = await ActionSheetAsync(
            $"Roast from {roast.RoastDate:MM/dd/yyyy}",
            "Cancel",
            null,
            ["Edit", "Delete"]);

        switch (action)
        {
            case "Edit":
                await EditRoastAsync(roast);
                break;
            case "Delete":
                await DeleteRoastAsync(roast);
                break;
        }

        SelectedRoast = null;
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

    private async Task InitializeWithCorrectPathAsync()
    {
        if (_hasInitializedPath)
        {
            return;
        }

        string? savedPath = await _preferencesService.GetAppDataFilePathAsync();

        if (!string.IsNullOrWhiteSpace(savedPath) &&
            !string.Equals(savedPath, _appDataService.DataFilePath, StringComparison.Ordinal))
        {
            await _appDataService.SetCustomFilePathAsync(savedPath);
        }

        _hasInitializedPath = true;
    }

    private void HandleAppDataChanged(object? sender, AppData appData)
    {
        SetRoasts(appData.RoastLogs);
    }

    private void SetRoasts(IEnumerable<RoastData>? roasts)
    {
        _allRoasts.Clear();
        _allRoasts.AddRange(
            (roasts ?? [])
                .Where(roast => roast is not null && roast.Id != Guid.Empty)
                .OrderByDescending(roast => roast.RoastDate));

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<RoastData> filteredRoasts = _allRoasts;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filteredRoasts = filteredRoasts.Where(roast =>
                roast.BeanType.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                roast.Notes.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                roast.Summary.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                roast.RoastLevelName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        Roasts = new ObservableCollection<RoastData>(filteredRoasts);
        RecordCount = Roasts.Count;
    }

    private Task ShowAlertAsync(string title, string message, string cancel)
    {
        return AlertAsync?.Invoke(title, message, cancel) ?? Task.CompletedTask;
    }
}
