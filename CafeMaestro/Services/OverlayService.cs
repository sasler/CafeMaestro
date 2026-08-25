using CafeMaestro.Models;
using CafeMaestro.ViewModels.Popups;
using CafeMaestro.Views.Popups;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro.Services;

/// <summary>
/// The only Roast Console service permitted to access the current Shell for overlays.
/// The view and its ViewModel are resolved and bound here rather than through popup
/// query attributes: a trimmed Release build leaves that implicit wiring unbound, which shows
/// up on device as an overlay with no batch, no title and dead buttons.
/// </summary>
public sealed class OverlayService(IServiceProvider services) : IOverlayService
{
    public async Task<WeighInOutcome> ShowWeighInAsync(
        WeighInRequest request,
        CancellationToken cancellationToken = default)
    {
        WeighInOutcome? result = await ShowAsync<WeighInPopup, WeighInViewModel, WeighInOutcome>(
            viewModel => viewModel.Request = request, cancellationToken);
        return result ?? WeighInOutcome.Cancelled;
    }

    public async Task<BatchChoiceOutcome> ChooseBatchAsync(
        IReadOnlyList<BatchChoice> choices,
        CancellationToken cancellationToken = default)
    {
        BatchChoiceOutcome? result = await ShowAsync<ChooseBatchPopup, ChooseBatchViewModel, BatchChoiceOutcome>(
            viewModel => viewModel.SetChoices(choices), cancellationToken);
        return result ?? BatchChoiceOutcome.Cancelled;
    }

    public async Task<DiscardOutcome> ShowDiscardAsync(
        DiscardRequest request,
        CancellationToken cancellationToken = default)
    {
        DiscardOutcome? result = await ShowAsync<DiscardRoastPopup, DiscardRoastViewModel, DiscardOutcome>(
            viewModel => viewModel.Request = request, cancellationToken);
        return result ?? DiscardOutcome.Cancelled;
    }

    public async Task<NavigationChoice> ConfirmNavigationAsync(CancellationToken cancellationToken = default)
    {
        NavigationChoice result = await ShowAsync<ConfirmNavigationPopup, ConfirmNavigationViewModel, NavigationChoice>(
            _ => { }, cancellationToken);
        return result;
    }

    public async Task<bool> ConfirmResetAsync(bool hasFirstCrack, CancellationToken cancellationToken = default) =>
        await ShowAsync<ConfirmResetPopup, ConfirmResetViewModel, bool>(
            viewModel => viewModel.HasFirstCrack = hasFirstCrack, cancellationToken);

    public async Task<TimeCorrectionOutcome> ShowTimeCorrectionAsync(
        TimeCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        TimeCorrectionOutcome? result =
            await ShowAsync<TimeCorrectionPopup, TimeCorrectionViewModel, TimeCorrectionOutcome>(
                viewModel => viewModel.Request = request, cancellationToken);
        return result ?? TimeCorrectionOutcome.Cancelled;
    }

    public Task CloseAsync<T>(T result, CancellationToken cancellationToken = default) =>
        services.GetRequiredService<IPopupService>().ClosePopupAsync(GetShell(), result, cancellationToken);

    private async Task<TResult?> ShowAsync<TView, TViewModel, TResult>(
        Action<TViewModel> configure,
        CancellationToken cancellationToken)
        where TView : View
        where TViewModel : notnull
    {
        TViewModel viewModel = services.GetRequiredService<TViewModel>();
        configure(viewModel);
        TView view = services.GetRequiredService<TView>();
        view.BindingContext = viewModel;

        IPopupResult<TResult> result =
            await GetShell().ShowPopupAsync<TResult>(view, PopupOptions.Empty, null, cancellationToken);
        return result.WasDismissedByTappingOutsideOfPopup ? default : result.Result;
    }

    private static Shell GetShell() =>
        Shell.Current ?? throw new InvalidOperationException("Shell.Current is not available for an overlay.");
}
