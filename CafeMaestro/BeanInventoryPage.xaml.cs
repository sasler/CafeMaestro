using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class BeanInventoryPage : ContentPage
{
    private readonly BeanInventoryPageViewModel _viewModel;

    public BeanInventoryPage(BeanInventoryPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.AlertAsync = (title, message, cancel) => DisplayAlertAsync(title, message, cancel);
        _viewModel.ConfirmationAsync = (title, message, accept, cancel) =>
            DisplayAlertAsync(title, message, accept, cancel);
        _viewModel.ActionSheetAsync = (title, cancel, destruction, buttons) =>
            DisplayActionSheetAsync(title, cancel, destruction, buttons);
    }

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

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.NavigateHomeAsync();
        return true;
    }
}
