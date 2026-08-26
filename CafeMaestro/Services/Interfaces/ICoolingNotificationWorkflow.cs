using CafeMaestro.Models;

namespace CafeMaestro.Services;

public interface ICoolingNotificationWorkflow
{
    Task ReconcileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the post-persistence drop policy, including first-drop onboarding and the
    /// batch-aware native schedule. The persisted roast is authoritative; a notification failure
    /// is returned as a non-blocking warning rather than rolling back the drop.
    /// </summary>
    Task<string?> HandleSuccessfulDropAsync(
        RoastData droppedRoast,
        CancellationToken cancellationToken = default);

    Task CancelAsync(Guid roastId, CancellationToken cancellationToken = default);
}
