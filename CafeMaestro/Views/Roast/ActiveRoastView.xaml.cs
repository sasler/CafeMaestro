using System.ComponentModel;
using CafeMaestro.Drawing;
using CafeMaestro.ViewModels;

namespace CafeMaestro.Views.Roast;

public partial class ActiveRoastView : ContentView
{
    private RoastInstrumentDrawable? _drawable;
    private RoastPageViewModel? _viewModel;

    public ActiveRoastView()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void InvalidateInstrument()
    {
        SyncDrawable();
        Instrument.Invalidate();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _drawable = new RoastInstrumentDrawable
        {
            TrackColor = ResourceColor("BorderColor"),
            RoastColor = ResourceColor("RoastColor"),
            PausedColor = ResourceColor("MutedTextColor")
        };
        Instrument.Drawable = _drawable;
        Subscribe();
        InvalidateInstrument();
    }

    private void OnUnloaded(object? sender, EventArgs e) => Unsubscribe();

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();
        _viewModel = BindingContext as RoastPageViewModel;
        Subscribe();
        InvalidateInstrument();
    }

    private void Subscribe()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void Unsubscribe()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RoastPageViewModel.ElapsedSweep) or nameof(RoastPageViewModel.IsPaused))
        {
            InvalidateInstrument();
        }
    }

    private void SyncDrawable()
    {
        if (_drawable is null || _viewModel is null)
        {
            return;
        }

        _drawable.Progress = _viewModel.ElapsedSweep;
        _drawable.IsPaused = _viewModel.IsPaused;
    }

    private Color ResourceColor(string key) =>
        (Color)(Resources.TryGetValue(key, out object value) ? value : Application.Current!.Resources[key]);
}
