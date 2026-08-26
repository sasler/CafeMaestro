using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class AppearanceSettingsPage : ContentPage
{
    private readonly AppearanceSettingsPageViewModel _viewModel;

    public AppearanceSettingsPage(AppearanceSettingsPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }
}
