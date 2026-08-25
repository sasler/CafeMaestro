using System.Collections.ObjectModel;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

public enum BeanInventoryFilter
{
    All,
    Available,
    Low,
    OutOfStock
}

public partial class BeanInventoryPageViewModel : ObservableObject
{
    private readonly IBeanDataService _beanService;
    private readonly IAppDataService _appDataService;
    private readonly INavigationService _navigationService;
    private readonly IAlertService _alertService;
    private readonly IRoastQueryService _roastQueryService;
    private readonly List<BeanData> _allBeans = [];
    private CancellationTokenSource? _searchCancellation;
    private int _refreshVersion;
    private bool _isSubscribed;

    [ObservableProperty]
    public partial ObservableCollection<BeanData> Beans { get; set; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial BeanInventoryFilter SelectedFilter { get; set; }

    [ObservableProperty]
    public partial BeanData? SelectedBean { get; set; }

    [ObservableProperty]
    public partial RoastData? SelectedLatestCompletedRoast { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<RoastData> SelectedRecentIncompleteRoasts { get; set; } = [];

    [ObservableProperty]
    public partial bool IsWideLayout { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; set; }

    [ObservableProperty]
    public partial string LoadErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int RecordCount { get; set; }

    [ObservableProperty]
    public partial double TotalRemainingKilograms { get; set; }

    public bool HasSelectedBean => SelectedBean is not null;
    public bool HasNoSelectedBean => SelectedBean is null;
    public bool HasSelectedLatestCompletedRoast => SelectedLatestCompletedRoast is not null;
    public bool HasSelectedRecentIncompleteRoasts => SelectedRecentIncompleteRoasts.Count > 0;
    public bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);
    public bool HasInventory => _allBeans.Count > 0;
    public bool HasVisibleBeans => Beans.Count > 0;
    public bool IsSearchNoResults => HasInventory && HasSearch && !HasVisibleBeans;
    public bool IsFilterNoResults => HasInventory && !HasSearch && SelectedFilter != BeanInventoryFilter.All && !HasVisibleBeans;
    public bool IsEmptyInventory => !HasInventory && !IsLoading && !HasLoadError;
    public string InventorySummary => $"{_allBeans.Count} {(_allBeans.Count == 1 ? "bean" : "beans")} · {FormatTotalQuantity(TotalRemainingKilograms)}";
    public string FilterSummary => SelectedFilter switch
    {
        BeanInventoryFilter.Available => "Available",
        BeanInventoryFilter.Low => "Low",
        BeanInventoryFilter.OutOfStock => "Out of stock",
        _ => "All"
    };

    public Func<string, string, string?, string[], Task<string>>? ActionSheetAsync { get; set; }

    public BeanInventoryPageViewModel(
        IBeanDataService beanService,
        IAppDataService appDataService,
        IPreferencesService preferencesService,
        INavigationService navigationService,
        IAlertService alertService,
        IRoastQueryService roastQueryService)
    {
        _beanService = beanService ?? throw new ArgumentNullException(nameof(beanService));
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _ = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _roastQueryService = roastQueryService ?? throw new ArgumentNullException(nameof(roastQueryService));
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearch));
        ScheduleFilter();
    }

    partial void OnSelectedBeanChanged(BeanData? value)
    {
        OnPropertyChanged(nameof(HasSelectedBean));
        OnPropertyChanged(nameof(HasNoSelectedBean));
    }

    partial void OnSelectedLatestCompletedRoastChanged(RoastData? value)
    {
        OnPropertyChanged(nameof(HasSelectedLatestCompletedRoast));
    }

    partial void OnSelectedRecentIncompleteRoastsChanged(ObservableCollection<RoastData> value)
    {
        OnPropertyChanged(nameof(HasSelectedRecentIncompleteRoasts));
    }

    partial void OnSelectedFilterChanged(BeanInventoryFilter value)
    {
        OnPropertyChanged(nameof(FilterSummary));
    }

    public async Task OnAppearingAsync()
    {
        EnsureSubscribed();
        await RefreshAsync();
    }

    public void OnDisappearing()
    {
        _searchCancellation?.Cancel();
        if (!_isSubscribed)
        {
            return;
        }

        _appDataService.DataChanged -= HandleAppDataChanged;
        _isSubscribed = false;
    }

    public void SetWideLayout(bool isWideLayout)
    {
        IsWideLayout = isWideLayout;
    }

    public Task NavigateHomeAsync() => _navigationService.GoToAsync(Routes.Main);

    [RelayCommand]
    private Task SearchAsync()
    {
        _searchCancellation?.Cancel();
        ApplyFilter();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ClearSearchAsync()
    {
        SearchText = string.Empty;
        ApplyFilter();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SelectFilterAsync(BeanInventoryFilter filter)
    {
        SelectedFilter = filter;
        ApplyFilter();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenBeanAsync(BeanData? bean)
    {
        if (bean is null)
        {
            return;
        }

        if (IsWideLayout)
        {
            await SelectBeanForDetailAsync(bean);
            return;
        }

        await _navigationService.GoToAsync(
            Routes.BeanDetail,
            new Dictionary<string, object> { ["BeanId"] = bean.Id.ToString() });
    }

    [RelayCommand]
    private Task OpenSelectedDetailAsync()
    {
        return SelectedBean is null
            ? Task.CompletedTask
            : _navigationService.GoToAsync(
                Routes.BeanDetail,
                new Dictionary<string, object> { ["BeanId"] = SelectedBean.Id.ToString() });
    }

    [RelayCommand]
    private async Task StartSelectedRoastAsync()
    {
        if (SelectedBean is null)
        {
            return;
        }

        await _navigationService.GoToAsync(
            Routes.Roast,
            new Dictionary<string, object>
            {
                ["BeanId"] = SelectedBean.Id.ToString(),
                ["NewRoast"] = bool.TrueString
            });
    }

    [RelayCommand]
    private async Task AddBeanAsync()
    {
        try
        {
            await _navigationService.GoToAsync(
                Routes.BeanEdit,
                new Dictionary<string, object> { ["IsNewBean"] = true });
        }
        catch
        {
            await _alertService.ShowAlertAsync("Error", "Unable to open bean editor.", "OK");
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

        BeanData? freshBean = await _beanService.GetBeanByIdAsync(bean.Id);
        if (freshBean is null)
        {
            await _alertService.ShowAlertAsync("Error", "Bean not found. Please refresh and try again.", "OK");
            return;
        }

        await _navigationService.GoToAsync(
            Routes.BeanEdit,
            new Dictionary<string, object> { ["BeanId"] = freshBean.Id.ToString() });
    }

    [RelayCommand]
    private async Task DeleteBeanAsync(BeanData? bean)
    {
        bean ??= SelectedBean;
        if (bean is null)
        {
            return;
        }

        bool confirmed = await _alertService.ShowConfirmationAsync(
            "Delete bean?",
            $"Delete {bean.DisplayName} from inventory? Historical roasts will remain in the log.",
            "Delete",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        if (!await _beanService.DeleteBeanAsync(bean.Id))
        {
            await _alertService.ShowAlertAsync("Error", "Failed to delete bean.", "OK");
            return;
        }

        if (SelectedBean?.Id == bean.Id)
        {
            SelectedBean = null;
            SelectedLatestCompletedRoast = null;
            SelectedRecentIncompleteRoasts = [];
        }

        await RefreshAsync();
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
            await _alertService.ShowAlertAsync("Error", "Unable to open import page.", "OK");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        int refreshVersion = Interlocked.Increment(ref _refreshVersion);
        try
        {
            IsLoading = true;
            List<BeanData> beans = await _beanService.GetAllBeansAsync();
            if (refreshVersion != _refreshVersion)
            {
                return;
            }

            SetBeans(beans);
            HasLoadError = false;
            LoadErrorMessage = string.Empty;
        }
        catch
        {
            if (refreshVersion == _refreshVersion)
            {
                HasLoadError = true;
                LoadErrorMessage = "Beans could not be refreshed.";
            }
        }
        finally
        {
            if (refreshVersion == _refreshVersion)
            {
                IsLoading = false;
                RaiseStateProperties();
            }
        }
    }

    [RelayCommand]
    private async Task ShowBeanActionsAsync(BeanData? bean)
    {
        if (bean is null || ActionSheetAsync is null)
        {
            return;
        }

        string action = await ActionSheetAsync(bean.DisplayName, "Cancel", null, ["Edit", "Delete"]);
        if (action == "Edit")
        {
            await EditBeanAsync(bean);
        }
        else if (action == "Delete")
        {
            await DeleteBeanAsync(bean);
        }
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

    private void HandleAppDataChanged(object? sender, AppData appData)
    {
        Interlocked.Increment(ref _refreshVersion);
        IsLoading = false;
        SetBeans(appData.Beans);
        HasLoadError = false;
        LoadErrorMessage = string.Empty;
        RaiseStateProperties();
        if (SelectedBean is not null)
        {
            BeanData? selected = appData.Beans.FirstOrDefault(bean => bean.Id == SelectedBean.Id);
            if (selected is null)
            {
                SelectedBean = null;
            }
            else
            {
                _ = SelectBeanForDetailAsync(selected);
            }
        }
    }

    private void SetBeans(IEnumerable<BeanData>? beans)
    {
        _allBeans.Clear();
        _allBeans.AddRange((beans ?? [])
            .Where(bean => bean is not null && bean.Id != Guid.Empty)
            .OrderByDescending(bean => bean.PurchaseDate));
        TotalRemainingKilograms = _allBeans.Sum(bean => Math.Max(0, bean.RemainingQuantity));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<BeanData> filtered = _allBeans;
        string search = SearchText.Trim();
        if (search.Length > 0)
        {
            filtered = filtered.Where(bean =>
                bean.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                bean.Country.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                bean.CoffeeName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                bean.Variety.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                bean.Process.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                bean.Notes.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        filtered = SelectedFilter switch
        {
            BeanInventoryFilter.Available => filtered.Where(bean => bean.IsAvailable && !bean.IsLowStock),
            BeanInventoryFilter.Low => filtered.Where(bean => bean.IsLowStock),
            BeanInventoryFilter.OutOfStock => filtered.Where(bean => bean.IsOutOfStock),
            _ => filtered
        };

        Beans = new ObservableCollection<BeanData>(filtered);
        RecordCount = Beans.Count;
        RaiseStateProperties();
    }

    private async Task SelectBeanForDetailAsync(BeanData bean)
    {
        SelectedBean = bean;
        IReadOnlyList<RoastData> roasts = await _roastQueryService.GetRoastsForBeanAsync(bean.Id);
        if (SelectedBean?.Id != bean.Id)
        {
            return;
        }

        SelectedLatestCompletedRoast = roasts.FirstOrDefault(roast => roast.CompletionStatus == RoastCompletionStatus.Complete);
        SelectedRecentIncompleteRoasts = new ObservableCollection<RoastData>(roasts
            .Where(roast => roast.CompletionStatus is RoastCompletionStatus.AwaitingWeight or RoastCompletionStatus.Unweighed)
            .Take(3));
    }

    private void ScheduleFilter()
    {
        _searchCancellation?.Cancel();
        CancellationTokenSource cancellation = new();
        _searchCancellation = cancellation;
        _ = ApplyFilterAfterDelayAsync(cancellation.Token);
    }

    private async Task ApplyFilterAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(150, cancellationToken);
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RaiseStateProperties()
    {
        OnPropertyChanged(nameof(HasInventory));
        OnPropertyChanged(nameof(HasVisibleBeans));
        OnPropertyChanged(nameof(IsSearchNoResults));
        OnPropertyChanged(nameof(IsFilterNoResults));
        OnPropertyChanged(nameof(IsEmptyInventory));
        OnPropertyChanged(nameof(InventorySummary));
    }

    private static string FormatTotalQuantity(double kilograms) => kilograms < 1
        ? $"{kilograms * 1000:0} g"
        : $"{kilograms:0.##} kg";
}
