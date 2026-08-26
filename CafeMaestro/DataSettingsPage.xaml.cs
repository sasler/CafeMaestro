using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class DataSettingsPage : ContentPage
{
    private readonly DataSettingsPageViewModel _viewModel;

    public DataSettingsPage(DataSettingsPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public DataSettingsPageViewModel ViewModel => _viewModel;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }

    protected override void OnDisappearing()
    {
        _viewModel.OnDisappearing();
        base.OnDisappearing();
    }
}
