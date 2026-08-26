using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class ImportPage : ContentPage
{
    private const double WideLayoutThreshold = 600;

    private readonly ImportPageViewModel _viewModel;

    public ImportPage(ImportPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public ImportPageViewModel ViewModel => _viewModel;

    protected override void OnDisappearing()
    {
        _viewModel.OnDisappearing();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsNavigationGuarded)
        {
            // A commit is in flight; leaving now would hide its result.
            return true;
        }

        _ = _viewModel.CancelCommand.ExecuteAsync(null);
        return true;
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        // Phone keeps field and column stacked; a tablet or landscape pairs them side by side.
        _viewModel.SetWideLayout(Width >= WideLayoutThreshold);
    }
}
