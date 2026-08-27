using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class BeanInventoryPage : ContentPage
{
    /// <summary>Above this width a bean opens beside the list instead of navigating away.</summary>
    private const double WideLayoutThreshold = 600;

    private readonly BeanInventoryPageViewModel _viewModel;

    public BeanInventoryPage(BeanInventoryPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.ActionSheetAsync = (title, cancel, destruction, buttons) =>
            DisplayActionSheetAsync(title, cancel, destruction, buttons);
    }

    public BeanInventoryPageViewModel ViewModel => _viewModel;

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
        _ = _viewModel.NavigateToRoastAsync();
        return true;
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        bool isWide = Width >= WideLayoutThreshold;
        // 3:4 keeps the bean rows readable rather than squeezing them into a third of the page
        // just to give the detail pane room it does not need.
        InventoryBody.ColumnDefinitions[0].Width = isWide
            ? new GridLength(3, GridUnitType.Star)
            : GridLength.Star;
        InventoryBody.ColumnDefinitions[1].Width = isWide
            ? new GridLength(4, GridUnitType.Star)
            : new GridLength(0);
        DetailPane.IsVisible = isWide;
        _viewModel.SetWideLayout(isWide);
    }
}
