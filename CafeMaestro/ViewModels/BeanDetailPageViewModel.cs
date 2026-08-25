using System.Collections.ObjectModel;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

public partial class BeanDetailPageViewModel : ObservableObject, IQueryAttributable
{
    private readonly IBeanDataService _beanService;
    private readonly IRoastQueryService _roastQueryService;
    private readonly IAppDataService _appDataService;
    private readonly INavigationService _navigationService;
    private readonly IAlertService _alertService;
    private Guid _beanId;
    private int _loadVersion;
    private bool _isSubscribed;

    [ObservableProperty]
    public partial BeanData? Bean { get; set; }

    [ObservableProperty]
    public partial RoastData? LatestCompletedRoast { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<RoastData> RecentIncompleteRoasts { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; set; }

    [ObservableProperty]
    public partial string LoadErrorMessage { get; set; } = string.Empty;

    public bool HasBean => Bean is not null;
    public bool HasLatestCompletedRoast => LatestCompletedRoast is not null;
    public bool HasNoLatestCompletedRoast => LatestCompletedRoast is null;
    public bool HasRecentIncompleteRoasts => RecentIncompleteRoasts.Count > 0;
    public bool CanStartRoast => Bean is { IsAvailable: true };

    public BeanDetailPageViewModel(
        IBeanDataService beanService,
        IRoastQueryService roastQueryService,
        IAppDataService appDataService,
        INavigationService navigationService,
        IAlertService alertService)
    {
        _beanService = beanService ?? throw new ArgumentNullException(nameof(beanService));
        _roastQueryService = roastQueryService ?? throw new ArgumentNullException(nameof(roastQueryService));
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
    }

    partial void OnBeanChanged(BeanData? value)
    {
        OnPropertyChanged(nameof(HasBean));
        OnPropertyChanged(nameof(CanStartRoast));
    }

    partial void OnLatestCompletedRoastChanged(RoastData? value)
    {
        OnPropertyChanged(nameof(HasLatestCompletedRoast));
        OnPropertyChanged(nameof(HasNoLatestCompletedRoast));
    }

    partial void OnRecentIncompleteRoastsChanged(ObservableCollection<RoastData> value)
    {
        OnPropertyChanged(nameof(HasRecentIncompleteRoasts));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("BeanId", out object? value) &&
            Guid.TryParse(value?.ToString(), out Guid beanId) &&
            beanId != Guid.Empty)
        {
            _beanId = beanId;
        }
    }

    public async Task OnAppearingAsync()
    {
        EnsureSubscribed();
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

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_beanId == Guid.Empty)
        {
            HasLoadError = true;
            LoadErrorMessage = "This bean could not be identified.";
            return;
        }

        int loadVersion = Interlocked.Increment(ref _loadVersion);
        try
        {
            IsLoading = true;
            BeanData? bean = await _beanService.GetBeanByIdAsync(_beanId);
            IReadOnlyList<RoastData> roasts = bean is null
                ? []
                : await _roastQueryService.GetRoastsForBeanAsync(_beanId);
            if (loadVersion != _loadVersion)
            {
                return;
            }

            Bean = bean;
            LatestCompletedRoast = roasts.FirstOrDefault(roast => roast.CompletionStatus == RoastCompletionStatus.Complete);
            RecentIncompleteRoasts = new ObservableCollection<RoastData>(roasts
                .Where(roast => roast.CompletionStatus is RoastCompletionStatus.AwaitingWeight or RoastCompletionStatus.Unweighed)
                .Take(3));
            HasLoadError = bean is null;
            LoadErrorMessage = bean is null ? "This bean is no longer in inventory." : string.Empty;
        }
        catch
        {
            HasLoadError = true;
            LoadErrorMessage = "Bean details could not be refreshed.";
        }
        finally
        {
            if (loadVersion == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private Task EditAsync()
    {
        return Bean is null
            ? Task.CompletedTask
            : _navigationService.GoToAsync(
                Routes.BeanEdit,
                new Dictionary<string, object> { ["BeanId"] = Bean.Id.ToString() });
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Bean is null)
        {
            return;
        }

        bool confirmed = await _alertService.ShowConfirmationAsync(
            "Delete bean?",
            $"Delete {Bean.DisplayName} from inventory? Historical roasts will remain in the log.",
            "Delete",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        if (await _beanService.DeleteBeanAsync(Bean.Id))
        {
            await _navigationService.GoBackAsync();
            return;
        }

        await _alertService.ShowAlertAsync("Error", "Failed to delete bean.", "OK");
    }

    [RelayCommand]
    private async Task StartRoastAsync()
    {
        if (Bean is null)
        {
            return;
        }

        if (Bean.IsOutOfStock)
        {
            await _alertService.ShowAlertAsync(
                "Out of stock",
                "Add inventory before starting a roast with this bean.",
                "OK");
            return;
        }

        await _navigationService.GoToAsync(
            Routes.Roast,
            new Dictionary<string, object>
            {
                ["BeanId"] = Bean.Id.ToString(),
                ["NewRoast"] = bool.TrueString
            });
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
        _ = RefreshAsync();
    }
}
