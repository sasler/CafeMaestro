using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>
/// Keeps Ticket 02's recovery-decision shape out of the Roast Console. The implementation is the
/// single integration point for corrected elapsed/start inputs after the domain API lands.
/// </summary>
public interface IRoastRecoveryAdapter
{
    Task<TransitionResult> KeepRoastingAsync(
        ActiveRoastSnapshot activeRoast,
        double? correctedElapsedSeconds = null,
        CancellationToken cancellationToken = default);

    Task<TransitionResult> EndedAtAsync(
        ActiveRoastSnapshot activeRoast,
        DateTimeOffset endedAtUtc,
        double? correctedElapsedSeconds = null,
        CancellationToken cancellationToken = default);
}
