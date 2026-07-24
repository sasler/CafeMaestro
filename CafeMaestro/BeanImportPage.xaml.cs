using CafeMaestro.ViewModels;
using CafeMaestro.Services;

namespace CafeMaestro;

public partial class BeanImportPage : ContentPage
{
    private readonly BeanImportPageViewModel _viewModel;
    private readonly IUserFileService _userFileService;
    private string? _temporaryFilePath;

    public BeanImportPage(BeanImportPageViewModel viewModel, IUserFileService userFileService)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _userFileService = userFileService ?? throw new ArgumentNullException(nameof(userFileService));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PickFileAsync = PickFileAsync;
    }

    protected override void OnDisappearing()
    {
        _viewModel.PickFileAsync = null;
        _userFileService.DeleteTemporaryFile(_temporaryFilePath);
        _temporaryFilePath = null;
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.CancelCommand.ExecuteAsync(null);
        return true;
    }

    private async Task<string?> PickFileAsync()
    {
        _userFileService.DeleteTemporaryFile(_temporaryFilePath);
        UserFileSelection? selection = await _userFileService.PickFileAsync(
            UserFileType.Csv,
            "Select CSV file with bean data");
        _temporaryFilePath = selection?.LocalPath;
        return _temporaryFilePath;
    }
}
