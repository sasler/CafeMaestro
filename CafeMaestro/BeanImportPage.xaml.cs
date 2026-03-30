using CafeMaestro.ViewModels;
using Microsoft.Maui.Storage;

namespace CafeMaestro;

public partial class BeanImportPage : ContentPage
{
    private readonly BeanImportPageViewModel _viewModel;

    public BeanImportPage(BeanImportPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PickFileAsync = PickFileAsync;
    }

    protected override void OnDisappearing()
    {
        _viewModel.PickFileAsync = null;
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.CancelCommand.ExecuteAsync(null);
        return true;
    }

    private static async Task<string?> PickFileAsync()
    {
        var customFileType = new FilePickerFileType(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, [".csv"] },
                { DevicePlatform.Android, ["text/csv", "text/comma-separated-values", "application/csv", "*/*"] },
                { DevicePlatform.iOS, ["public.comma-separated-values-text"] },
                { DevicePlatform.MacCatalyst, ["public.comma-separated-values-text"] }
            });

        var options = new PickOptions
        {
            PickerTitle = "Select CSV file with bean data",
            FileTypes = customFileType
        };

        FileResult? result = await FilePicker.Default.PickAsync(options);
        return result?.FullPath;
    }
}
