using CafeMaestro.Models;

namespace CafeMaestro.Services;

public interface IOverlayService
{
    Task<WeighInOutcome> ShowWeighInAsync(
        WeighInRequest request,
        CancellationToken cancellationToken = default);

    Task<BatchChoiceOutcome> ChooseBatchAsync(
        IReadOnlyList<BatchChoice> choices,
        CancellationToken cancellationToken = default);

    Task<DiscardOutcome> ShowDiscardAsync(
        DiscardRequest request,
        CancellationToken cancellationToken = default);

    Task<NavigationChoice> ConfirmNavigationAsync(CancellationToken cancellationToken = default);

    Task<bool> ConfirmResetAsync(bool hasFirstCrack, CancellationToken cancellationToken = default);

    Task<TimeCorrectionOutcome> ShowTimeCorrectionAsync(
        TimeCorrectionRequest request,
        CancellationToken cancellationToken = default);

    Task CloseAsync<T>(T result, CancellationToken cancellationToken = default);
}
