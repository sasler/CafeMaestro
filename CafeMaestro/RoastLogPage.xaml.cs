using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class RoastLogPage : ContentPage
{
    /// <summary>Above this width the log shows a batch beside the list instead of navigating.</summary>
    private const double WideLayoutThreshold = 600;

    private readonly RoastLogPageViewModel _viewModel;
    private IDispatcherTimer? _ticker;

    public RoastLogPage(RoastLogPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public RoastLogPageViewModel ViewModel => _viewModel;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
        EnsureTicker();
        _ticker!.Start();
    }

    protected override void OnDisappearing()
    {
        _ticker?.Stop();
        if (_ticker is not null)
        {
            _ticker.Tick -= OnTickerTick;
            _ticker = null;
        }
        _viewModel.OnDisappearing();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.NavigateToRoastAsync();
        return true;
    }

    private void EnsureTicker()
    {
        if (_ticker is not null)
        {
            return;
        }

        _ticker = Dispatcher.CreateTimer();
        _ticker.Interval = TimeSpan.FromSeconds(1);
        _ticker.Tick += OnTickerTick;
    }

    private async void OnTickerTick(object? sender, EventArgs e)
    {
        await _viewModel.RefreshTimeProjectionAsync();
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        bool isWide = Width >= WideLayoutThreshold;
        // 3:4 keeps the batch cards readable rather than squeezing them into a third of the
        // page just to give the detail pane room it does not need.
        LogBody.ColumnDefinitions[0].Width = isWide
            ? new GridLength(3, GridUnitType.Star)
            : GridLength.Star;
        LogBody.ColumnDefinitions[1].Width = isWide
            ? new GridLength(4, GridUnitType.Star)
            : new GridLength(0);
        DetailPane.IsVisible = isWide;
        _viewModel.SetWideLayout(isWide);
    }
}
