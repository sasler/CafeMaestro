using System.ComponentModel;
using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class RoastPage : ContentPage
{
    private readonly RoastPageViewModel _viewModel;
    private readonly IDispatcherTimer _ticker;
    private Window? _window;
    private bool _isAppeared;
    private bool _isWindowStopped;

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
        _isAppeared = true;
        SubscribeWindowLifecycle();
        await _viewModel.OnAppearingAsync();
        UpdateChrome();
        UpdateTicker();
    }

    protected override async void OnDisappearing()
    {
        _isAppeared = false;
        UnsubscribeWindowLifecycle();
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
    }

    private void UpdateChrome()
    {
        bool showTabs = _viewModel.PresentationState is
            Models.RoastPresentationState.Setup or Models.RoastPresentationState.Handoff;
        Shell.SetTabBarIsVisible(this, showTabs);
        NavigationPage.SetHasNavigationBar(this, showTabs);
    }

    private void UpdateTicker()
    {
        bool shouldRun = _isAppeared && !_isWindowStopped &&
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

    private void SubscribeWindowLifecycle()
    {
        Window? window = Window;
        if (ReferenceEquals(_window, window) || window is null)
        {
            return;
        }

        UnsubscribeWindowLifecycle();
        _window = window;
        _window.Stopped += OnWindowStopped;
        _window.Resumed += OnWindowResumed;
    }

    private void UnsubscribeWindowLifecycle()
    {
        if (_window is null)
        {
            return;
        }

        _window.Stopped -= OnWindowStopped;
        _window.Resumed -= OnWindowResumed;
        _window = null;
    }

    private async void OnWindowStopped(object? sender, EventArgs e)
    {
        _isWindowStopped = true;
        UpdateTicker();
        await _viewModel.OnWindowStoppedAsync();
    }

    private async void OnWindowResumed(object? sender, EventArgs e)
    {
        _isWindowStopped = false;
        await _viewModel.OnWindowResumedAsync();
        UpdateTicker();
    }
}
