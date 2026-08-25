using CafeMaestro.Models;
using CafeMaestro.ViewModels.Popups;
using CommunityToolkit.Maui;

namespace CafeMaestro.Services;

/// <summary>The only Roast Console service permitted to access the current Shell for overlays.</summary>
public sealed class OverlayService(IPopupService popupService) : IOverlayService
{
    public async Task<WeighInOutcome> ShowWeighInAsync(
        WeighInRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await popupService.ShowPopupAsync<WeighInViewModel, WeighInOutcome>(
            GetShell(), PopupOptions.Empty, Parameters(nameof(WeighInViewModel.Request), request), cancellationToken);
        return result.WasDismissedByTappingOutsideOfPopup || result.Result is null
            ? WeighInOutcome.Cancelled
            : result.Result;
    }

    public async Task<BatchChoiceOutcome> ChooseBatchAsync(
        IReadOnlyList<BatchChoice> choices,
        CancellationToken cancellationToken = default)
    {
        var result = await popupService.ShowPopupAsync<ChooseBatchViewModel, BatchChoiceOutcome>(
            GetShell(), PopupOptions.Empty, Parameters(nameof(ChooseBatchViewModel.Choices), choices), cancellationToken);
        return result.WasDismissedByTappingOutsideOfPopup || result.Result is null
            ? BatchChoiceOutcome.Cancelled
            : result.Result;
    }

    public async Task<DiscardOutcome> ShowDiscardAsync(
        DiscardRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await popupService.ShowPopupAsync<DiscardRoastViewModel, DiscardOutcome>(
            GetShell(), PopupOptions.Empty, Parameters(nameof(DiscardRoastViewModel.Request), request), cancellationToken);
        return result.WasDismissedByTappingOutsideOfPopup || result.Result is null
            ? DiscardOutcome.Cancelled
            : result.Result;
    }

    public async Task<NavigationChoice> ConfirmNavigationAsync(CancellationToken cancellationToken = default)
    {
        var result = await popupService.ShowPopupAsync<ConfirmNavigationViewModel, NavigationChoice>(
            GetShell(), PopupOptions.Empty, null, cancellationToken);
        return result.WasDismissedByTappingOutsideOfPopup
            ? NavigationChoice.KeepRoasting
            : result.Result;
    }

    public async Task<bool> ConfirmResetAsync(bool hasFirstCrack, CancellationToken cancellationToken = default)
    {
        var result = await popupService.ShowPopupAsync<ConfirmResetViewModel, bool>(
            GetShell(), PopupOptions.Empty,
            Parameters(nameof(ConfirmResetViewModel.HasFirstCrack), hasFirstCrack), cancellationToken);
        return !result.WasDismissedByTappingOutsideOfPopup && result.Result;
    }

    public async Task<TimeCorrectionOutcome> ShowTimeCorrectionAsync(
        TimeCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await popupService.ShowPopupAsync<TimeCorrectionViewModel, TimeCorrectionOutcome>(
            GetShell(), PopupOptions.Empty,
            Parameters(nameof(TimeCorrectionViewModel.Request), request), cancellationToken);
        return result.WasDismissedByTappingOutsideOfPopup || result.Result is null
            ? TimeCorrectionOutcome.Cancelled
            : result.Result;
    }

    public Task CloseAsync<T>(T result, CancellationToken cancellationToken = default) =>
        popupService.ClosePopupAsync(GetShell(), result, cancellationToken);

    private static Shell GetShell() =>
        Shell.Current ?? throw new InvalidOperationException("Shell.Current is not available for an overlay.");

    private static Dictionary<string, object> Parameters(string key, object value) => new()
    {
        [key] = value
    };
}
