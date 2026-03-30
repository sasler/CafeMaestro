using CafeMaestro.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CafeMaestro;

public partial class SettingsPage : ContentPage,
    IRecipient<SettingsAlertMessage>,
    IRecipient<PickDataFileRequestMessage>,
    IRecipient<SettingsActionSheetRequestMessage>,
    IRecipient<SettingsConfirmationRequestMessage>
{
    private readonly SettingsPageViewModel _viewModel;
    private bool _isMessengerRegistered;

    public SettingsPage(SettingsPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RegisterMessenger();
        await _viewModel.OnAppearingAsync();

        if (_viewModel.ShouldHighlightDataFileSection)
        {
            await HighlightDataFileSectionAsync();
            _viewModel.MarkDataFileSectionHighlighted();
        }
    }

    protected override void OnDisappearing()
    {
        UnregisterMessenger();
        _viewModel.OnDisappearing();
        base.OnDisappearing();
    }

    public void Receive(SettingsAlertMessage message)
    {
        _ = DisplayAlert(message.Value.Title, message.Value.Message, message.Value.Cancel);
    }

    public void Receive(PickDataFileRequestMessage message)
    {
        _ = HandlePickDataFileRequestAsync(message);
    }

    public void Receive(SettingsActionSheetRequestMessage message)
    {
        _ = HandleActionSheetRequestAsync(message);
    }

    public void Receive(SettingsConfirmationRequestMessage message)
    {
        _ = HandleConfirmationRequestAsync(message);
    }

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.GoBackAsync();
        return true;
    }

    private void RegisterMessenger()
    {
        if (_isMessengerRegistered)
        {
            return;
        }

        WeakReferenceMessenger.Default.RegisterAll(this);
        _isMessengerRegistered = true;
    }

    private void UnregisterMessenger()
    {
        if (!_isMessengerRegistered)
        {
            return;
        }

        WeakReferenceMessenger.Default.UnregisterAll(this);
        _isMessengerRegistered = false;
    }

    private async Task HighlightDataFileSectionAsync()
    {
        if (DataFileSection == null)
        {
            return;
        }

        var dataFileSection = DataFileSection;
        var originalColor = dataFileSection.BackgroundColor;
        dataFileSection.BackgroundColor = GetResourceColor("HighlightColor", originalColor);
        dataFileSection.Scale = 0.97;
        await dataFileSection.FadeTo(0.85, 250);
        await dataFileSection.FadeTo(1, 250);
        await dataFileSection.ScaleTo(1.02, 150);
        await dataFileSection.ScaleTo(1.0, 150);
        await Task.Delay(500);
        dataFileSection.BackgroundColor = originalColor;
    }

    private async Task HandlePickDataFileRequestAsync(PickDataFileRequestMessage message)
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".json"] },
                    { DevicePlatform.Android, ["application/json"] },
                    { DevicePlatform.iOS, ["public.json"] },
                    { DevicePlatform.MacCatalyst, ["public.json"] }
                });

            var options = new PickOptions
            {
                PickerTitle = message.PickerTitle,
                FileTypes = customFileType
            };

            var result = await FilePicker.PickAsync(options);
            message.Reply(result?.FullPath);
        }
        catch
        {
            message.Reply((string?)null);
        }
    }

    private async Task HandleActionSheetRequestAsync(SettingsActionSheetRequestMessage message)
    {
        string? result = await DisplayActionSheet(message.Title, message.Cancel, null, message.Buttons.ToArray());
        message.Reply(result);
    }

    private async Task HandleConfirmationRequestAsync(SettingsConfirmationRequestMessage message)
    {
        bool confirm = await DisplayAlert(message.Title, message.Message, message.Accept, message.Cancel);
        message.Reply(confirm);
    }

    private static Color GetResourceColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out object? value) == true &&
            value is Color color)
        {
            return color;
        }

        return fallback;
    }
}
