using CafeMaestro.Models;
using CafeMaestro.ViewModels.Popups;
using CafeMaestro.Views.Popups;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

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
            await GetShell().ShowPopupAsync<TResult>(view, BuildPopupOptions(), null, cancellationToken);
        return result.WasDismissedByTappingOutsideOfPopup ? default : result.Result;
    }

    /// <summary>
    /// Themed chrome for every overlay in the app. <c>PopupOptions.Empty</c> is not empty: it
    /// carries the toolkit's defaults, whose container shape is a light rounded rectangle with a
    /// 2 px stroke. Against a dark theme that reads as a thick white frame around the popup card,
    /// so the container is made invisible here and each popup's own Direction B Border supplies
    /// the surface, stroke and radius. Options are rebuilt per presentation, which is what keeps
    /// the scrim correct after a light/dark switch.
    /// </summary>
    private static PopupOptions BuildPopupOptions() => new()
    {
        CanBeDismissedByTappingOutsideOfPopup = true,
        PageOverlayColor = ThemeColor("PopupScrimColor") ?? Colors.Transparent,
        Shadow = null,
        Shape = new RoundRectangle
        {
            CornerRadius = new CornerRadius(ThemeDouble("PopupCornerRadiusValue", 20)),
            Fill = Brush.Transparent,
            Stroke = Brush.Transparent,
            StrokeThickness = 0
        }
    };

    private static Color? ThemeColor(string key) =>
        Application.Current?.Resources.TryGetValue(key, out object? value) == true && value is Color color
            ? color
            : null;

    private static double ThemeDouble(string key, double fallback) =>
        Application.Current?.Resources.TryGetValue(key, out object? value) == true &&
        value is double resolved &&
        double.IsFinite(resolved)
            ? resolved
            : fallback;

    private static Shell GetShell() =>
        Shell.Current ?? throw new InvalidOperationException("Shell.Current is not available for an overlay.");
}
