using System.ComponentModel;
using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class RoastPage : ContentPage
{
    private readonly RoastPageViewModel _viewModel;
    private readonly IDispatcherTimer _ticker;

    public RoastPage(RoastPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _ticker = Dispatcher.CreateTimer();
        _ticker.Interval = TimeSpan.FromMilliseconds(250);
        _ticker.Tick += OnTick;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
        UpdateChrome();
        _ticker.Start();
    }

    protected override async void OnDisappearing()
    {
        _ticker.Stop();
        await _viewModel.OnDisappearingAsync();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.PresentationState is Models.RoastPresentationState.Active or
            Models.RoastPresentationState.Recovery)
        {
            MainThread.BeginInvokeOnMainThread(async () => await _viewModel.HandleBackNavigationAsync());
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _viewModel.Tick();
        ActiveView.InvalidateInstrument();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoastPageViewModel.PresentationState))
        {
            UpdateChrome();
        }
    }

    private void UpdateChrome()
    {
        bool showTabs = _viewModel.PresentationState is
            Models.RoastPresentationState.Setup or Models.RoastPresentationState.Handoff;
        Shell.SetTabBarIsVisible(this, showTabs);
        NavigationPage.SetHasNavigationBar(this, showTabs);
    }
}
