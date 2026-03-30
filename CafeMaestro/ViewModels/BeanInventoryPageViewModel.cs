using System.Collections.ObjectModel;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

public partial class BeanInventoryPageViewModel : ObservableObject
{
    private readonly IBeanDataService _beanService;
    private readonly IAppDataService _appDataService;
    private readonly IPreferencesService _preferencesService;
    private readonly INavigationService _navigationService;
    private readonly List<BeanData> _allBeans = [];
    private bool _isSubscribed;
    private bool _hasInitializedPath;

    [ObservableProperty]
    private ObservableCollection<BeanData> _beans = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private BeanData? _selectedBean;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _recordCount;

    public BeanInventoryPageViewModel(
        IBeanDataService beanService,
        IAppDataService appDataService,
        IPreferencesService preferencesService,
        INavigationService navigationService)
    {
        _beanService = beanService ?? throw new ArgumentNullException(nameof(beanService));
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    public Func<string, string, string, Task>? AlertAsync { get; set; }

    public Func<string, string, string, string, Task<bool>>? ConfirmationAsync { get; set; }

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
    private async Task AddBeanAsync()
    {
        try
        {
            await _navigationService.GoToAsync(
                Routes.BeanEdit,
                new Dictionary<string, object>
                {
                    ["IsNewBean"] = true
                });
        }
        catch
        {
            await ShowAlertAsync("Error", "Unable to open bean editor.", "OK");
        }
    }

    [RelayCommand]
    private async Task EditBeanAsync(BeanData? bean)
    {
        bean ??= SelectedBean;

        if (bean is null)
        {
            return;
        }

        try
        {
            var freshBean = await _beanService.GetBeanByIdAsync(bean.Id);

            if (freshBean is null)
            {
                await ShowAlertAsync("Error", "Bean not found. Please refresh and try again.", "OK");
                return;
            }

            await _navigationService.GoToAsync(
                Routes.BeanEdit,
                new Dictionary<string, object>
                {
                    ["BeanId"] = freshBean.Id.ToString()
                });
        }
        catch
        {
            await ShowAlertAsync("Error", "Error preparing to edit bean", "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteBeanAsync(BeanData? bean)
    {
        bean ??= SelectedBean;

        if (bean is null)
        {
            return;
        }

        try
        {
            bool success = await _beanService.DeleteBeanAsync(bean.Id);

            if (success)
            {
                await RefreshAsync();
                return;
            }

            await ShowAlertAsync("Error", "Failed to delete bean", "OK");
        }
        catch
        {
            await ShowAlertAsync("Error", "Failed to delete bean", "OK");
        }
    }

    [RelayCommand]
    private async Task NavigateToImportAsync()
    {
        try
        {
            await _navigationService.GoToAsync(Routes.BeanImport);
        }
        catch
        {
            await ShowAlertAsync("Error", "Unable to open import page", "OK");
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
            var beans = await _beanService.GetAllBeansAsync();
            SetBeans(beans);
        }
        catch
        {
            await ShowAlertAsync("Error", "Failed to load beans", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ShowBeanActionsAsync(BeanData? bean)
    {
        if (bean is null || ActionSheetAsync is null)
        {
            return;
        }

        SelectedBean = bean;

        string action = await ActionSheetAsync(
            bean.DisplayName,
            "Cancel",
            null,
            ["Edit", "Delete"]);

        switch (action)
        {
            case "Edit":
                await EditBeanAsync(bean);
                break;
            case "Delete":
                await DeleteBeanWithConfirmationAsync(bean);
                break;
        }

        SelectedBean = null;
    }

    private async Task DeleteBeanWithConfirmationAsync(BeanData bean)
    {
        if (ConfirmationAsync is not null)
        {
            bool confirm = await ConfirmationAsync(
                "Delete Bean",
                $"Are you sure you want to delete {bean.DisplayName}?",
                "Delete",
                "Cancel");

            if (!confirm)
            {
                return;
            }
        }

        await DeleteBeanAsync(bean);
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
        SetBeans(appData.Beans);
    }

    private void SetBeans(IEnumerable<BeanData>? beans)
    {
        _allBeans.Clear();
        _allBeans.AddRange(
            (beans ?? [])
                .Where(bean => bean is not null && bean.Id != Guid.Empty)
                .OrderByDescending(bean => bean.PurchaseDate));

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<BeanData> filteredBeans = _allBeans;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filteredBeans = filteredBeans.Where(bean =>
                bean.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                bean.Country.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                bean.CoffeeName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                bean.Variety.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                bean.Process.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                bean.Notes.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        Beans = new ObservableCollection<BeanData>(filteredBeans);
        RecordCount = Beans.Count;
    }

    private Task ShowAlertAsync(string title, string message, string cancel)
    {
        return AlertAsync?.Invoke(title, message, cancel) ?? Task.CompletedTask;
    }
}
