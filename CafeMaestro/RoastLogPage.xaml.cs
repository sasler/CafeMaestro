using CafeMaestro.Layouts;
using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class RoastLogPage : ContentPage
{
    /// <summary>Above this width the log shows a batch beside the list instead of navigating.</summary>
    private const double WideLayoutThreshold = 600;

    /// <summary>
    /// The list's share of a wide split before <c>ListPaneMaxWidth</c> caps it. Below the cap the
    /// list gets three sevenths, which keeps batch cards readable at tablet-portrait widths.
    /// </summary>
    private const double ListPaneShare = 3d / 7d;

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
        // The list takes its share of the width up to ListPaneMaxWidth and the detail pane keeps
        // the rest, so a very wide window widens the detail side rather than stretching batch cards.
        LogBody.ColumnDefinitions[0].Width = isWide
            ? new GridLength(
                ResponsiveLayout.ComputeListPaneWidth(
                    Width,
                    ListPaneShare,
                    ResponsiveLayout.TokenOrDefault("ListPaneMaxWidth", 460)),
                GridUnitType.Absolute)
            : GridLength.Star;
        LogBody.ColumnDefinitions[1].Width = isWide
            ? GridLength.Star
            : new GridLength(0);
        DetailPane.IsVisible = isWide;
        _viewModel.SetWideLayout(isWide);
    }
}
