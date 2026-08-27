using CafeMaestro.Layouts;
using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class BeanInventoryPage : ContentPage
{
    /// <summary>Above this width a bean opens beside the list instead of navigating away.</summary>
    private const double WideLayoutThreshold = 600;

    /// <summary>
    /// The list's share of a wide split before <c>ListPaneMaxWidth</c> caps it. Below the cap the
    /// list gets three sevenths, which keeps bean rows readable at tablet-portrait widths.
    /// </summary>
    private const double ListPaneShare = 3d / 7d;

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
        // The list takes its share of the width up to ListPaneMaxWidth and the detail pane keeps
        // the rest, so a very wide window widens the detail side rather than stretching bean rows.
        InventoryBody.ColumnDefinitions[0].Width = isWide
            ? new GridLength(
                ResponsiveLayout.ComputeListPaneWidth(
                    Width,
                    ListPaneShare,
                    ResponsiveLayout.TokenOrDefault("ListPaneMaxWidth", 460)),
                GridUnitType.Absolute)
            : GridLength.Star;
        InventoryBody.ColumnDefinitions[1].Width = isWide
            ? GridLength.Star
            : new GridLength(0);
        DetailPane.IsVisible = isWide;
        _viewModel.SetWideLayout(isWide);
    }
}
