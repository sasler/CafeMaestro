using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

public partial class RoastDetailPageViewModel : ObservableObject, IQueryAttributable
{
    private readonly IRoastQueryService _queryService;
    private readonly IAppDataService _appDataService;
    private readonly INavigationService _navigationService;
    private readonly IOverlayService _overlayService;
    private readonly IRoastDataService _roastDataService;
    private readonly IAlertService _alertService;
    private Guid _roastId;
    private bool _isSubscribed;

    [ObservableProperty]
    public partial RoastLogCard? Card { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; set; }

    [ObservableProperty]
    public partial string LoadErrorMessage { get; set; } = string.Empty;

    public bool HasCard => Card is not null;
    public bool CanEditRoast => Card?.Roast is not null;
    public bool CanEditFinalWeight => Card?.IsComplete == true || Card?.IsNeedsWeight == true;
    public string FinalWeightActionText => Card?.IsComplete == true ? "EDIT FINAL WEIGHT" : "WEIGH IN";
    public bool HasNotes => !string.IsNullOrWhiteSpace(Card?.Roast?.Notes);
    public string NotesDisplay => HasNotes ? Card!.Roast!.Notes : "No notes recorded.";

    public RoastDetailPageViewModel(
        IRoastQueryService queryService,
        IAppDataService appDataService,
        INavigationService navigationService,
        IOverlayService overlayService,
        IRoastDataService roastDataService,
        IAlertService alertService)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
        _roastDataService = roastDataService ?? throw new ArgumentNullException(nameof(roastDataService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
    }

    partial void OnCardChanged(RoastLogCard? value)
    {
        OnPropertyChanged(nameof(HasCard));
        OnPropertyChanged(nameof(CanEditRoast));
        OnPropertyChanged(nameof(CanEditFinalWeight));
        OnPropertyChanged(nameof(FinalWeightActionText));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(NotesDisplay));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("RoastId", out object? value) &&
            Guid.TryParse(value?.ToString(), out Guid roastId))
        {
            _roastId = roastId;
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

        _appDataService.DataChanged -= HandleDataChanged;
        _isSubscribed = false;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_roastId == Guid.Empty)
        {
            HasLoadError = true;
            LoadErrorMessage = "This roast could not be identified.";
            return;
        }

        try
        {
            IsLoading = true;
            Task<RoastData?> roastTask = _queryService.GetRoastAsync(_roastId);
            Task<IReadOnlyList<RoastWorkItem>> workTask = _queryService.GetOpenWorkAsync();
            await Task.WhenAll(roastTask, workTask);
            RoastData? roast = await roastTask;
            RoastWorkItem? work = (await workTask).FirstOrDefault(item => item.RoastId == _roastId);
            Card = work is not null
                ? RoastLogCard.FromWork(work, roast)
                : roast is not null
                    ? RoastLogCard.FromHistory(roast)
                    : null;
            HasLoadError = Card is null;
            LoadErrorMessage = Card is null ? "This roast is no longer in the log." : string.Empty;
        }
        catch
        {
            HasLoadError = true;
            LoadErrorMessage = "Roast details could not be refreshed.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task EditRoastAsync() => Card?.Roast is null
        ? Task.CompletedTask
        : _navigationService.GoToAsync(
            Routes.RoastEdit,
            new Dictionary<string, object> { ["EditRoastId"] = Card.RoastId.ToString() });

    [RelayCommand]
    private async Task EditFinalWeightAsync()
    {
        if (Card is null || !CanEditFinalWeight)
        {
            return;
        }

        WeighInRequest request = Card.WorkItem is RoastWorkItem work
            ? new WeighInRequest
            {
                RoastId = work.RoastId,
                BatchNumber = work.BatchNumber,
                BeanDisplaySnapshot = work.BeanDisplaySnapshot,
                BatchWeight = work.BatchWeight,
                DroppedAtUtc = work.DroppedAtUtc,
                TotalSeconds = work.TotalSeconds
            }
            : new WeighInRequest
            {
                RoastId = Card.RoastId,
                BatchNumber = Card.Roast!.BatchNumber,
                BeanDisplaySnapshot = Card.BeanDisplay,
                BatchWeight = Card.Roast.BatchWeight,
                DroppedAtUtc = Card.Roast.DroppedAtUtc ?? new DateTimeOffset(Card.Roast.RoastDate),
                TotalSeconds = Card.Roast.TotalSeconds,
                InitialFinalWeight = Card.Roast.FinalWeight
            };
        await _overlayService.ShowWeighInAsync(request);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Card is null || !await _alertService.ShowConfirmationAsync(
                "Delete roast?",
                $"Delete {Card.BeanDisplay}, {Card.DateDisplay}? This cannot be undone.",
                "Delete",
                "Cancel"))
        {
            return;
        }

        if (await _roastDataService.DeleteRoastLogAsync(Card.RoastId))
        {
            await _navigationService.GoBackAsync();
            return;
        }

        await _alertService.ShowAlertAsync("Delete roast", "The roast could not be deleted.", "OK");
    }

    private void EnsureSubscribed()
    {
        if (_isSubscribed)
        {
            return;
        }

        _appDataService.DataChanged += HandleDataChanged;
        _isSubscribed = true;
    }

    private void HandleDataChanged(object? sender, AppData appData) => _ = RefreshAsync();
}
