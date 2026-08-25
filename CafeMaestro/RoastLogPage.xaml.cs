using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class RoastLogPage : ContentPage
{
    private readonly RoastLogPageViewModel _viewModel;
    private IDispatcherTimer? _ticker;

    public RoastLogPage(RoastLogPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

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
        _viewModel.OnDisappearing();
        base.OnDisappearing();
    }

    private void EnsureTicker()
    {
        if (_ticker is not null)
        {
            return;
        }

        _ticker = Dispatcher.CreateTimer();
        _ticker.Interval = TimeSpan.FromSeconds(1);
        _ticker.Tick += async (_, _) => await _viewModel.RefreshTimeProjectionAsync();
    }
}
