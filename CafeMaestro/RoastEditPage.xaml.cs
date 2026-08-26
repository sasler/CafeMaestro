using System.ComponentModel;
using CafeMaestro.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace CafeMaestro;

public partial class RoastEditPage : ContentPage
{
    private readonly RoastEditPageViewModel _viewModel;

    public RoastEditPage(RoastEditPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        await _viewModel.OnAppearingAsync();
    }

    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnDisappearing();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RoastEditPageViewModel.FocusField) ||
            string.IsNullOrWhiteSpace(_viewModel.FocusField))
        {
            return;
        }

        string field = _viewModel.FocusField;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            VisualElement? target = field switch
            {
                nameof(RoastEditPageViewModel.SelectedBean) => BeanPicker,
                nameof(RoastEditPageViewModel.TemperatureText) => TemperatureEntry,
                nameof(RoastEditPageViewModel.BatchWeightText) => BatchWeightEntry,
                nameof(RoastEditPageViewModel.FinalWeightText) => FinalWeightEntry,
                nameof(RoastEditPageViewModel.RoastTimeText) => RoastTimeEntry,
                nameof(RoastEditPageViewModel.FirstCrackTimeText) => FirstCrackEntry,
                _ => null
            };
            target?.Focus();
            _viewModel.FocusField = string.Empty;
        });
    }
}
