using CafeMaestro.Models;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels.Popups;

public partial class DiscardRoastViewModel(IOverlayService overlayService) : ObservableObject, IQueryAttributable
{
    [ObservableProperty]
    public partial DiscardRequest? Request { get; set; }

    [ObservableProperty]
    public partial bool BeansWereUsed { get; set; } = true;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(Request), out object? request))
        {
            Request = request as DiscardRequest;
        }
    }

    [RelayCommand]
    private Task DiscardAsync() => overlayService.CloseAsync(
        new DiscardOutcome(DiscardOutcomeKind.Discard, BeansWereUsed));

    [RelayCommand]
    private Task KeepLogAsync() => overlayService.CloseAsync(
        new DiscardOutcome(DiscardOutcomeKind.KeepLog, BeansWereUsed));

    [RelayCommand]
    private Task CancelAsync() => overlayService.CloseAsync(DiscardOutcome.Cancelled);
}
