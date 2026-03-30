using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class BeanEditPage : ContentPage
{
    private readonly BeanEditPageViewModel _viewModel;

    public BeanEditPage(BeanEditPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.AlertAsync = (title, message, cancel) => DisplayAlertAsync(title, message, cancel);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.CancelCommand.ExecuteAsync(null);
        return true;
    }
}
