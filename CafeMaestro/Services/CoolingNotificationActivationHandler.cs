using CafeMaestro.Models;
using CafeMaestro.Navigation;

namespace CafeMaestro.Services;

/// <summary>Revalidates a native reminder payload against initialized persisted data.</summary>
public sealed class CoolingNotificationActivationHandler(
    IAppDataService appDataService,
    INavigationService navigationService,
    IOverlayService overlayService,
    IClock clock) : IAppActivationHandler
{
    public async Task HandleAsync(
        AppActivationPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!string.Equals(payload.Kind, "cooling-ready", StringComparison.Ordinal) ||
            !payload.Values.TryGetValue("roastId", out string? rawRoastId) ||
            !Guid.TryParse(rawRoastId, out Guid roastId))
        {
            return;
        }

        await navigationService.GoToAsync(Routes.RoastLog);
        RoastData? roast = appDataService.CurrentData.RoastLogs
            .FirstOrDefault(candidate => candidate.Id == roastId);
        if (roast is null)
        {
            return;
        }

        if (roast.CompletionStatus == RoastCompletionStatus.AwaitingWeight &&
            roast.ReadyToWeighAtUtc is DateTimeOffset readyAt &&
            RoastProjection.EffectiveStatus(roast, clock.UtcNow) == RoastEffectiveStatus.NeedsWeight)
        {
            await overlayService.ShowWeighInAsync(new WeighInRequest
            {
                RoastId = roast.Id,
                BatchNumber = roast.BatchNumber,
                BeanDisplaySnapshot = roast.BeanDisplaySnapshot,
                BatchWeight = roast.BatchWeight,
                DroppedAtUtc = roast.DroppedAtUtc ?? readyAt,
                TotalSeconds = roast.TotalSeconds
            }, cancellationToken);
            return;
        }

        await navigationService.GoToAsync(
            Routes.RoastDetail,
            new Dictionary<string, object> { ["RoastId"] = roast.Id.ToString() });
    }
}
