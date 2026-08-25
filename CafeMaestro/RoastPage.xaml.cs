using System.ComponentModel;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class RoastPage : ContentPage
{
    private readonly RoastPageViewModel _viewModel;
    private readonly IDispatcherTimer _ticker;
    private bool _isAppeared;
    private bool _isObservingViewModel;

    public RoastPage(RoastPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _ticker = Dispatcher.CreateTimer();
        _ticker.Interval = TimeSpan.FromMilliseconds(250);
        _ticker.Tick += OnTick;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isAppeared = true;
        SubscribeViewModel();
        await _viewModel.OnAppearingAsync();
        UpdateChrome();
        UpdateTicker();
    }

    protected override async void OnDisappearing()
    {
        _isAppeared = false;
        UnsubscribeViewModel();
        _ticker.Stop();
        await _viewModel.OnDisappearingAsync();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.PresentationState is Models.RoastPresentationState.Active or
            Models.RoastPresentationState.Recovery or
            Models.RoastPresentationState.PersistenceError)
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
            UpdateTicker();
        }
        else if (e.PropertyName == nameof(RoastPageViewModel.IsWindowStopped))
        {
            UpdateTicker();
        }
    }

    private void SubscribeViewModel()
    {
        if (_isObservingViewModel)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _isObservingViewModel = true;
    }

    private void UnsubscribeViewModel()
    {
        if (!_isObservingViewModel)
        {
            return;
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _isObservingViewModel = false;
    }

    private void UpdateChrome()
    {
        bool showTabs = RoastChromePolicy.IsTabBarVisible(_viewModel.PresentationState);
        Shell.SetTabBarIsVisible(this, showTabs);
        NavigationPage.SetHasNavigationBar(this, showTabs);
    }

    private void UpdateTicker()
    {
        bool shouldRun = _isAppeared && !_viewModel.IsWindowStopped &&
            _viewModel.PresentationState is Models.RoastPresentationState.Active or
                Models.RoastPresentationState.Handoff;
        if (shouldRun)
        {
            _ticker.Start();
        }
        else
        {
            _ticker.Stop();
        }
    }

}
