using CafeMaestro.Models;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels.Popups;

public partial class WeighInViewModel(
    IRoastSessionService sessionService,
    IOverlayService overlayService) : ObservableObject, IQueryAttributable
{
    [ObservableProperty]
    public partial WeighInRequest? Request { get; set; }

    [ObservableProperty]
    public partial string FinalWeightText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ValidationMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LiveResult { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public bool CanSave => !IsBusy && Request is not null &&
                           WeighInInputValidator.Validate(FinalWeightText, Request.BatchWeight).IsValid;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(Request), out object? request))
        {
            Request = request as WeighInRequest;
        }
    }

    partial void OnFinalWeightTextChanged(string value)
    {
        if (Request is null)
        {
            return;
        }

        WeightValidationResult result = WeighInInputValidator.Validate(value, Request.BatchWeight);
        ValidationMessage = result.Error ?? string.Empty;
        LiveResult = result.IsValid && result.Grams is double grams
            ? $"{grams:0.0} g output · {(Request.BatchWeight - grams) / Request.BatchWeight * 100:0.0}% loss"
            : string.Empty;
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (Request is null)
        {
            return;
        }

        WeightValidationResult validation = WeighInInputValidator.Validate(FinalWeightText, Request.BatchWeight);
        if (!validation.IsValid || validation.Grams is not double grams)
        {
            ValidationMessage = validation.Error ?? "Enter a valid final weight.";
            return;
        }

        IsBusy = true;
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
        TransitionResult result = await sessionService.SaveFinalWeightAsync(Request.RoastId, grams);
        IsBusy = false;
        if (!result.Success)
        {
            ValidationMessage = result.Message ?? "The final weight could not be saved. Retry.";
            OnPropertyChanged(nameof(CanSave));
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        await overlayService.CloseAsync(new WeighInOutcome(WeighInOutcomeKind.Saved, grams));
    }

    [RelayCommand]
    private async Task MarkUnweighedAsync()
    {
        if (Request is null)
        {
            return;
        }

        TransitionResult result = await sessionService.MarkUnweighedAsync(Request.RoastId);
        if (!result.Success)
        {
            ValidationMessage = result.Message ?? "The batch could not be marked unweighed.";
            return;
        }

        await overlayService.CloseAsync(new WeighInOutcome(WeighInOutcomeKind.MarkedUnweighed));
    }

    [RelayCommand]
    private Task CancelAsync() => overlayService.CloseAsync(WeighInOutcome.Cancelled);
}
