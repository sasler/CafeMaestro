using System.Diagnostics;
using System.IO;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly IAppDataService _appDataService;
    private readonly IPreferencesService _preferencesService;
    private readonly INavigationService _navigationService;
    private string _userDataFilePath = string.Empty;
    private bool _isSubscribed;

    [ObservableProperty]
    private string _dataFilePath = string.Empty;

    [ObservableProperty]
    private string _dataFilePathDisplay = "File: Loading...";

    [ObservableProperty]
    private int _beanCount;

    [ObservableProperty]
    private int _roastCount;

    [ObservableProperty]
    private string _dataStatsDisplay = "Beans: --  |  Roasts: --";

    public MainPageViewModel(IAppDataService appDataService, IPreferencesService preferencesService)
        : this(appDataService, preferencesService, new NoOpNavigationService())
    {
    }

    public MainPageViewModel(IAppDataService appDataService, IPreferencesService preferencesService, INavigationService navigationService)
    {
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    partial void OnDataFilePathChanged(string value)
    {
        DataFilePathDisplay = $"File: {GetDisplayFileName(value)}";
    }

    partial void OnBeanCountChanged(int value)
    {
        UpdateDataStatsDisplay();
    }

    partial void OnRoastCountChanged(int value)
    {
        UpdateDataStatsDisplay();
    }

    public async Task OnAppearingAsync()
    {
        EnsureSubscribed();
        await InitializeWithCorrectPathAsync();
        await RefreshStatisticsAsync();
    }

    public void OnDisappearing()
    {
        Unsubscribe();
    }

    public Task RefreshStatisticsAsync()
    {
        try
        {
            RefreshFromAppData(_appDataService.CurrentData, ResolveCurrentFilePath());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error refreshing MainPage statistics: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task StartRoasting()
    {
        return NavigateToAsync(Routes.Roast);
    }

    [RelayCommand]
    private Task GoToBeans()
    {
        return NavigateToAsync(Routes.BeanInventory);
    }

    [RelayCommand]
    private Task GoToRoastLog()
    {
        return NavigateToAsync(Routes.RoastLog);
    }

    [RelayCommand]
    private Task GoToSettings()
    {
        return NavigateToAsync(Routes.Settings);
    }

    protected virtual Task NavigateToAsync(string route)
    {
        return _navigationService.GoToAsync(route);
    }

    private async Task InitializeWithCorrectPathAsync()
    {
        try
        {
            string? savedFilePath = await _preferencesService.GetAppDataFilePathAsync();

            if (!string.IsNullOrWhiteSpace(savedFilePath))
            {
                _userDataFilePath = savedFilePath;

                if (!string.Equals(_appDataService.DataFilePath, savedFilePath, StringComparison.Ordinal))
                {
                    AppData appData = await _appDataService.SetCustomFilePathAsync(savedFilePath);
                    RefreshFromAppData(appData, savedFilePath);
                    return;
                }
            }
            else if (!string.IsNullOrWhiteSpace(_appDataService.DataFilePath))
            {
                _userDataFilePath = _appDataService.DataFilePath;
            }

            RefreshFromAppData(_appDataService.CurrentData, _appDataService.DataFilePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in MainPageViewModel initialization: {ex.Message}");
        }
    }

    private void EnsureSubscribed()
    {
        if (_isSubscribed)
        {
            return;
        }

        _appDataService.DataChanged += HandleAppDataChanged;
        _appDataService.DataFilePathChanged += HandleDataFilePathChanged;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _appDataService.DataChanged -= HandleAppDataChanged;
        _appDataService.DataFilePathChanged -= HandleDataFilePathChanged;
        _isSubscribed = false;
    }

    private void HandleAppDataChanged(object? sender, AppData appData)
    {
        RefreshFromAppData(appData, ResolveCurrentFilePath());
    }

    private void HandleDataFilePathChanged(object? sender, string filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            _userDataFilePath = filePath;
        }

        DataFilePath = filePath;
    }

    private void RefreshFromAppData(AppData appData, string filePath)
    {
        DataFilePath = filePath;
        BeanCount = appData.Beans?.Count ?? 0;
        RoastCount = appData.RoastLogs?.Count ?? 0;
    }

    private void UpdateDataStatsDisplay()
    {
        DataStatsDisplay = $"Beans: {BeanCount}  |  Roasts: {RoastCount}";
    }

    private static string GetDisplayFileName(string filePath)
    {
        return string.IsNullOrWhiteSpace(filePath) ? "Loading..." : Path.GetFileName(filePath);
    }

    private string ResolveCurrentFilePath()
    {
        if (!string.IsNullOrWhiteSpace(DataFilePath))
        {
            return DataFilePath;
        }

        if (!string.IsNullOrWhiteSpace(_userDataFilePath))
        {
            return _userDataFilePath;
        }

        return _appDataService.DataFilePath;
    }

    private sealed class NoOpNavigationService : INavigationService
    {
        public Task GoToAsync(string route) => Task.CompletedTask;

        public Task GoToAsync(string route, IDictionary<string, object> parameters) => Task.CompletedTask;

        public Task GoBackAsync() => Task.CompletedTask;
    }
}
