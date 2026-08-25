using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>
/// Read-only projections over the roast log: carry-forward setup values, the reference result,
/// and the open-work queue. Never mutates data.
/// </summary>
public interface IRoastQueryService
{
    /// <summary>
    /// Carry-forward values for a bean plus its newest completed result. Temperature and batch
    /// weight come from the newest usable roast; the reference result comes from the newest
    /// completed one, so a cooling batch never displaces the last real result.
    /// </summary>
    Task<RoastSetupSuggestion> GetSetupSuggestionAsync(
        Guid beanId,
        CancellationToken cancellationToken = default);

    Task<RoastData?> GetLastCompletedRoastForBeanAsync(
        Guid beanId,
        CancellationToken cancellationToken = default);

    /// <summary>Every roast attributable to the bean, newest first.</summary>
    Task<IReadOnlyList<RoastData>> GetRoastsForBeanAsync(
        Guid beanId,
        CancellationToken cancellationToken = default);

    /// <summary>Dropped roasts still owed a final weight, oldest drop first.</summary>
    Task<IReadOnlyList<RoastWorkItem>> GetOpenWorkAsync(
        CancellationToken cancellationToken = default);
}
