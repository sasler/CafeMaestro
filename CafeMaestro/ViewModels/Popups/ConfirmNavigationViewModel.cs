using CafeMaestro.Models;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels.Popups;

public partial class ConfirmNavigationViewModel(IOverlayService overlayService)
{
    [RelayCommand]
    private Task KeepRoastingAsync() => overlayService.CloseAsync(NavigationChoice.KeepRoasting);

    [RelayCommand]
    private Task DiscardBatchAsync() => overlayService.CloseAsync(NavigationChoice.DiscardBatch);
}
