using CafeMaestro.Models;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels.Popups;

public partial class TimeCorrectionViewModel(IOverlayService overlayService) : ObservableObject, IQueryAttributable
{
    [ObservableProperty]
    public partial TimeCorrectionRequest? Request { get; set; }

    [ObservableProperty]
    public partial string TimeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ValidationMessage { get; set; } = string.Empty;

    public bool CanSave => Request is not null && TryParse(TimeText, out int seconds) &&
                           seconds >= 0 && seconds <= Request.MaximumSeconds;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(Request), out object? value) && value is TimeCorrectionRequest request)
        {
            Request = request;
            TimeText = $"{request.CurrentSeconds / 60:D2}:{request.CurrentSeconds % 60:D2}";
        }
    }

    partial void OnTimeTextChanged(string value)
    {
        ValidationMessage = CanSave ? string.Empty : "Enter a time within the roast in mm:ss.";
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync()
    {
        TryParse(TimeText, out int seconds);
        return overlayService.CloseAsync(new TimeCorrectionOutcome(seconds));
    }

    [RelayCommand]
    private Task CancelAsync() => overlayService.CloseAsync(TimeCorrectionOutcome.Cancelled);

    private static bool TryParse(string? text, out int seconds)
    {
        seconds = 0;
        string[] parts = (text ?? string.Empty).Split(':');
        return parts.Length == 2 && int.TryParse(parts[0], out int minutes) &&
               int.TryParse(parts[1], out int remainder) && minutes >= 0 && remainder is >= 0 and < 60 &&
               (seconds = minutes * 60 + remainder) >= 0;
    }
}
