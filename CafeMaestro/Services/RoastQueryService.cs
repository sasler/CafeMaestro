using CafeMaestro.Models;

namespace CafeMaestro.Services;

public sealed class RoastQueryService : IRoastQueryService
{
    private readonly IAppDataService _appDataService;
    private readonly IClock _clock;

    public RoastQueryService(IAppDataService appDataService, IClock clock)
    {
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<RoastSetupSuggestion> GetSetupSuggestionAsync(
        Guid beanId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<RoastData> history = GetRoastsForBean(beanId);

        // Setup values follow the newest usable roast, even if it still needs a weight.
        RoastData? carryForwardSource = history.FirstOrDefault(roast =>
            roast.CompletionStatus != RoastCompletionStatus.Discarded);
        RoastData? lastCompleted = history.FirstOrDefault(roast =>
            roast.CompletionStatus == RoastCompletionStatus.Complete);

        int newerAwaitingWeight = lastCompleted is null
            ? history.Count(roast => roast.CompletionStatus == RoastCompletionStatus.AwaitingWeight)
            : history
                .TakeWhile(roast => roast.Id != lastCompleted.Id)
                .Count(roast => roast.CompletionStatus == RoastCompletionStatus.AwaitingWeight);

        return Task.FromResult(new RoastSetupSuggestion
        {
            BeanId = beanId,
            Temperature = carryForwardSource?.Temperature,
            BatchWeight = carryForwardSource?.BatchWeight,
            LastCompletedRoast = lastCompleted,
            NewerAwaitingWeightCount = newerAwaitingWeight
        });
    }

    public Task<RoastData?> GetLastCompletedRoastForBeanAsync(
        Guid beanId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetRoastsForBean(beanId)
            .FirstOrDefault(roast => roast.CompletionStatus == RoastCompletionStatus.Complete));
    }

    public Task<IReadOnlyList<RoastData>> GetRoastsForBeanAsync(
        Guid beanId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<RoastData>>(GetRoastsForBean(beanId));
    }

    public Task<IReadOnlyList<RoastWorkItem>> GetOpenWorkAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<RoastWorkItem>>(
            RoastProjection.OpenWork(_appDataService.CurrentData, _clock.UtcNow));
    }

    /// <summary>
    /// Newest first. Same-bean back-to-back batches are disambiguated by batch number, since two
    /// drops can share a drop timestamp once truncated.
    /// </summary>
    private List<RoastData> GetRoastsForBean(Guid beanId)
    {
        AppData data = _appDataService.CurrentData;
        List<BeanData> beans = data.Beans ?? [];
        BeanData? bean = beans.FirstOrDefault(candidate => candidate.Id == beanId);
        if (bean is null)
        {
            return [];
        }

        return (data.RoastLogs ?? [])
            .Where(roast => RoastProjection.BelongsToBean(roast, bean, beans))
            .OrderByDescending(RoastProjection.DroppedAtUtc)
            .ThenByDescending(roast => roast.BatchNumber ?? 0)
            .ToList();
    }
}
