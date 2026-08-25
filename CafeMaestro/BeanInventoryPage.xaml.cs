using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class BeanInventoryPage : ContentPage
{
    private readonly BeanInventoryPageViewModel _viewModel;

    public BeanInventoryPage(BeanInventoryPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
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

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        bool isWide = Width >= 600;
        InventoryBody.ColumnDefinitions[1].Width = isWide ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        DetailPane.IsVisible = isWide;
        _viewModel.SetWideLayout(isWide);
    }
}
