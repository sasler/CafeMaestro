using System.Collections.ObjectModel;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

public enum RoastLogFilter
{
    All,
    Complete,
    NeedsWeight,
    Unweighed
}

public partial class RoastLogPageViewModel : ObservableObject
{
    private readonly IRoastDataService _roastDataService;
    private readonly IRoastQueryService _roastQueryService;
    private readonly IAppDataService _appDataService;
    private readonly INavigationService _navigationService;
    private readonly IOverlayService _overlayService;
    private readonly IAlertService _alertService;
    private readonly IUserFileService _userFileService;
    private readonly List<RoastLogCard> _allOpenWork = [];
    private readonly List<RoastLogCard> _allHistory = [];
    private CancellationTokenSource? _searchCancellation;
    private bool _isSubscribed;

    [ObservableProperty]
    public partial ObservableCollection<RoastLogCard> OpenWork { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<RoastLogCard> History { get; set; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; set; }

    [ObservableProperty]
    public partial string LoadErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int RecordCount { get; set; }

    [ObservableProperty]
    public partial RoastLogFilter SelectedFilter { get; set; }

    public bool HasOpenWork => OpenWork.Count > 0;
    public bool HasHistory => History.Count > 0;
    public bool IsEmpty => !HasOpenWork && !HasHistory && !IsLoading;
    public bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);
    public bool IsAllSelected => SelectedFilter == RoastLogFilter.All;
    public bool IsCompleteSelected => SelectedFilter == RoastLogFilter.Complete;
    public bool IsNeedsWeightSelected => SelectedFilter == RoastLogFilter.NeedsWeight;
    public bool IsUnweighedSelected => SelectedFilter == RoastLogFilter.Unweighed;
    public string EmptyTitle => HasSearch
        ? $"No roasts match “{SearchText.Trim()}”"
        : SelectedFilter == RoastLogFilter.All
            ? "No roasts yet"
            : $"No {FilterDisplay(SelectedFilter)} roasts";
    public string EmptyBody => HasSearch || SelectedFilter != RoastLogFilter.All
        ? "Clear the search or choose All to see the full log."
        : "Start your first roast to build a searchable history.";
    public bool CanClearEmptyState => HasSearch || SelectedFilter != RoastLogFilter.All;
    public bool ShowStartEmptyState => !CanClearEmptyState;

    /// <summary>Exposed for deterministic lifecycle tests; UI callers never need to await event handlers.</summary>
    public Task LastRefreshTask { get; private set; } = Task.CompletedTask;

    public RoastLogPageViewModel(
        IRoastDataService roastDataService,
        IRoastQueryService roastQueryService,
        IAppDataService appDataService,
        INavigationService navigationService,
        IOverlayService overlayService,
        IAlertService alertService,
        IUserFileService userFileService)
    {
        _roastDataService = roastDataService ?? throw new ArgumentNullException(nameof(roastDataService));
        _roastQueryService = roastQueryService ?? throw new ArgumentNullException(nameof(roastQueryService));
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _userFileService = userFileService ?? throw new ArgumentNullException(nameof(userFileService));
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearch));
        NotifyEmptyState();
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        _ = DebounceSearchAsync(_searchCancellation.Token);
    }

    partial void OnOpenWorkChanged(ObservableCollection<RoastLogCard> value) => NotifyCollectionState();
    partial void OnHistoryChanged(ObservableCollection<RoastLogCard> value) => NotifyCollectionState();
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnSelectedFilterChanged(RoastLogFilter value)
    {
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsCompleteSelected));
        OnPropertyChanged(nameof(IsNeedsWeightSelected));
        OnPropertyChanged(nameof(IsUnweighedSelected));
        NotifyEmptyState();
        ApplyFilter();
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

    /// <summary>Hardware back on the Log tab returns to Roast, the launch destination.</summary>
    public Task NavigateToRoastAsync() => _navigationService.GoToAsync(Routes.Roast);

    /// <summary>Called by the one page-owned ticker; cells never own timers or subscriptions.</summary>
    public async Task RefreshTimeProjectionAsync()
    {
        if (_allOpenWork.Count == 0)
        {
            return;
        }

        try
        {
            IReadOnlyList<RoastWorkItem> openWork = await _roastQueryService.GetOpenWorkAsync();
            SetOpenWork(openWork);
        }
        catch
        {
            // The full refresh surface owns read errors; a transient ticker failure keeps cached rows.
        }
    }

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
    private void SelectFilter(RoastLogFilter filter) => SelectedFilter = filter;

    [RelayCommand]
    private void ClearEmptyState()
    {
        SearchText = string.Empty;
        SelectedFilter = RoastLogFilter.All;
        ApplyFilter();
    }

    [RelayCommand]
    private Task AddRoastAsync() => _navigationService.GoToAsync(
        Routes.Roast,
        new Dictionary<string, object> { ["NewRoast"] = bool.TrueString });

    [RelayCommand]
    private Task OpenDetailAsync(RoastLogCard? card) => card is null
        ? Task.CompletedTask
        : _navigationService.GoToAsync(
            Routes.RoastDetail,
            new Dictionary<string, object> { ["RoastId"] = card.RoastId.ToString() });

    [RelayCommand]
    private Task EditRoastAsync(RoastLogCard? card) => card?.Roast is null
        ? Task.CompletedTask
        : _navigationService.GoToAsync(
            Routes.RoastEdit,
            new Dictionary<string, object> { ["EditRoastId"] = card.RoastId.ToString() });

    [RelayCommand]
    private async Task WeighAsync(RoastLogCard? requestedCard)
    {
        List<RoastWorkItem> ready = _allOpenWork
            .Where(card => card.IsNeedsWeight && card.WorkItem is not null)
            .Select(card => card.WorkItem!)
            .ToList();
        if (ready.Count == 0)
        {
            return;
        }

        RoastWorkItem? selected;
        if (ready.Count > 1)
        {
            BatchChoiceOutcome outcome = await _overlayService.ChooseBatchAsync(ready.Select(ToBatchChoice).ToList());
            selected = outcome.Choice is null
                ? null
                : ready.FirstOrDefault(item => item.RoastId == outcome.Choice.RoastId);
        }
        else
        {
            selected = requestedCard?.WorkItem?.IsReadyToWeigh == true
                ? requestedCard.WorkItem
                : ready[0];
        }

        if (selected is null)
        {
            return;
        }

        await _overlayService.ShowWeighInAsync(ToWeighInRequest(selected, ready.Count > 1));
    }

    [RelayCommand]
    private async Task DeleteRoastAsync(RoastLogCard? card)
    {
        if (card is null)
        {
            return;
        }

        bool confirmed = await _alertService.ShowConfirmationAsync(
            "Delete roast?",
            $"Delete {card.BeanDisplay}, {card.DateDisplay}? This cannot be undone.",
            "Delete",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        if (!await _roastDataService.DeleteRoastLogAsync(card.RoastId))
        {
            await _alertService.ShowAlertAsync("Delete roast", "The roast could not be deleted.", "OK");
        }
    }

    [RelayCommand]
    private async Task ExportLogAsync()
    {
        try
        {
            await using var stream = new MemoryStream();
            await _roastDataService.ExportRoastLogAsync(stream);
            stream.Position = 0;
            DocumentSaveResult result = await _userFileService.SaveFileAsync(
                $"CafeMaestro_RoastLog_{DateTime.Now:yyyy-MM-dd}.csv",
                "text/csv",
                stream);
            if (!result.IsCanceled && !result.IsSuccessful)
            {
                throw result.Exception ?? new IOException("The roast log could not be saved.");
            }
        }
        catch
        {
            await _alertService.ShowAlertAsync("Export roast log", "CafeMaestro could not export the roast log.", "OK");
        }
    }

    [RelayCommand]
    private Task NavigateToImportAsync() => _navigationService.GoToAsync(Routes.RoastImport);

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
            Task<IReadOnlyList<RoastWorkItem>> openTask = _roastQueryService.GetOpenWorkAsync();
            Task<IReadOnlyList<RoastData>> historyTask = _roastQueryService.GetHistoryAsync();
            await Task.WhenAll(openTask, historyTask);
            SetOpenWork(await openTask);
            SetHistory(await historyTask);
            HasLoadError = false;
            LoadErrorMessage = string.Empty;
        }
        catch
        {
            HasLoadError = true;
            LoadErrorMessage = "Roast Log could not be refreshed. Your last loaded rows are still shown.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DebounceSearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
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
        LastRefreshTask = RefreshAsync();
    }

    private void SetOpenWork(IEnumerable<RoastWorkItem> items)
    {
        _allOpenWork.Clear();
        _allOpenWork.AddRange(items.Select(item => RoastLogCard.FromWork(item)));
        ApplyOpenWorkFilter();
    }

    private void SetHistory(IEnumerable<RoastData> roasts)
    {
        _allHistory.Clear();
        _allHistory.AddRange(roasts.Select(RoastLogCard.FromHistory));
        ApplyHistoryFilter();
    }

    private void ApplyFilter()
    {
        ApplyOpenWorkFilter();
        ApplyHistoryFilter();
    }

    private void ApplyOpenWorkFilter()
    {
        IEnumerable<RoastLogCard> open = SelectedFilter switch
        {
            RoastLogFilter.All => _allOpenWork,
            RoastLogFilter.NeedsWeight => _allOpenWork.Where(card => card.IsNeedsWeight),
            _ => []
        };
        OpenWork = new ObservableCollection<RoastLogCard>(open.Where(card => card.Matches(SearchText)));
        UpdateRecordCount();
    }

    private void ApplyHistoryFilter()
    {
        IEnumerable<RoastLogCard> history = SelectedFilter switch
        {
            RoastLogFilter.All => _allHistory,
            RoastLogFilter.Complete => _allHistory.Where(card => card.IsComplete),
            RoastLogFilter.Unweighed => _allHistory.Where(card => card.IsUnweighed),
            _ => []
        };
        History = new ObservableCollection<RoastLogCard>(history.Where(card => card.Matches(SearchText)));
        UpdateRecordCount();
    }

    private void UpdateRecordCount()
    {
        RecordCount = OpenWork.Count + History.Count;
        NotifyEmptyState();
    }

    private void NotifyCollectionState()
    {
        OnPropertyChanged(nameof(HasOpenWork));
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void NotifyEmptyState()
    {
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyBody));
        OnPropertyChanged(nameof(CanClearEmptyState));
        OnPropertyChanged(nameof(ShowStartEmptyState));
    }

    private static string FilterDisplay(RoastLogFilter filter) => filter switch
    {
        RoastLogFilter.Complete => "complete",
        RoastLogFilter.NeedsWeight => "needs weight",
        RoastLogFilter.Unweighed => "unweighed",
        _ => string.Empty
    };

    private static BatchChoice ToBatchChoice(RoastWorkItem item) => new()
    {
        RoastId = item.RoastId,
        BatchNumber = item.BatchNumber,
        BeanDisplaySnapshot = item.BeanDisplaySnapshot,
        BatchWeight = item.BatchWeight,
        DroppedAtUtc = item.DroppedAtUtc,
        TotalSeconds = item.TotalSeconds
    };

    private static WeighInRequest ToWeighInRequest(RoastWorkItem item, bool hasAnother) => new()
    {
        RoastId = item.RoastId,
        BatchNumber = item.BatchNumber,
        BeanDisplaySnapshot = item.BeanDisplaySnapshot,
        BatchWeight = item.BatchWeight,
        DroppedAtUtc = item.DroppedAtUtc,
        TotalSeconds = item.TotalSeconds,
        HasAnotherBatchWaiting = hasAnother
    };
}
