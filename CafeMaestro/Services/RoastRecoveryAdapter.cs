using CafeMaestro.Models;

namespace CafeMaestro.Services;

public sealed class RoastRecoveryAdapter(IRoastSessionService sessionService) : IRoastRecoveryAdapter
{
    public Task<TransitionResult> KeepRoastingAsync(
        ActiveRoastSnapshot activeRoast,
        double? correctedElapsedSeconds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activeRoast);
        RecoveryDecision decision = correctedElapsedSeconds.HasValue
            ? RecoveryDecision.KeepRoasting(correctedElapsedSeconds.Value)
            : RecoveryDecision.KeepRoasting();
        return sessionService.RecoverAsync(decision, cancellationToken);
    }

    public Task<TransitionResult> EndedAtAsync(
        ActiveRoastSnapshot activeRoast,
        DateTimeOffset endedAtUtc,
        double? correctedElapsedSeconds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activeRoast);
        RecoveryDecision decision = correctedElapsedSeconds.HasValue
            ? RecoveryDecision.EndedAt(endedAtUtc, correctedElapsedSeconds.Value)
            : RecoveryDecision.EndedAt(endedAtUtc);
        return sessionService.RecoverAsync(decision, cancellationToken);
    }
}
