using CafeMaestro.Models;

namespace CafeMaestro.Services;

public interface ICoolingNotificationWorkflow
{
    Task ReconcileAsync(CancellationToken cancellationToken = default);

    Task AfterSuccessfulDropAsync(
        RoastWorkItem droppedRoast,
        CancellationToken cancellationToken = default);
}
