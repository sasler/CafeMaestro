using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>
/// The only writer of active-session and roast-workflow state. ViewModels request transitions
/// and render the returned snapshot; they never mutate app data, bean quantity, or notifications.
/// </summary>
public interface IRoastSessionService
{
    /// <summary>
    /// Raised synchronously after any committed change that affects the snapshot.
    /// Async-void subscribers are not supported.
    /// </summary>
    event EventHandler<RoastSessionSnapshot>? SnapshotChanged;

    Task<RoastSessionSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists a durable draft; the roast becomes visible as Roasting only after it commits.</summary>
    Task<TransitionResult> StartAsync(RoastSetup setup, CancellationToken cancellationToken = default);

    Task<TransitionResult> PauseAsync(CancellationToken cancellationToken = default);

    Task<TransitionResult> ResumeAsync(CancellationToken cancellationToken = default);

    Task<TransitionResult> ResetAsync(CancellationToken cancellationToken = default);

    Task<TransitionResult> MarkFirstCrackAsync(CancellationToken cancellationToken = default);

    Task<TransitionResult> CorrectFirstCrackAsync(
        int elapsedSeconds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends the roast once, decrements the bean once, clears the draft and advances the batch
    /// number in one atomic mutation. Retrying after a failed write reuses the same draft id.
    /// </summary>
    Task<TransitionResult> DropAsync(
        DateTimeOffset? correctedDropUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>Commits a previously captured Drop proposal without projecting time again.</summary>
    Task<TransitionResult> DropAsync(
        DropProposal proposal,
        CancellationToken cancellationToken = default);

    Task<TransitionResult> CorrectDropAsync(
        Guid roastId,
        DateTimeOffset correctedDropUtc,
        CancellationToken cancellationToken = default);

    Task<TransitionResult> DiscardAsync(
        bool beansWereUsed,
        bool keepLog,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends one cooling batch early. The persisted cooling window is shortened to the time the
    /// batch has actually cooled, so the batch reads Needs weight from now on — including after a
    /// restart — while its final weight stays missing and every other batch is untouched.
    /// </summary>
    Task<TransitionResult> CompleteCoolingAsync(
        Guid roastId,
        CancellationToken cancellationToken = default);

    Task<TransitionResult> SaveFinalWeightAsync(
        Guid roastId,
        double grams,
        CancellationToken cancellationToken = default);

    Task<TransitionResult> MarkUnweighedAsync(
        Guid roastId,
        CancellationToken cancellationToken = default);

    /// <summary>Ends the sitting. Cooling and Needs-weight roasts are untouched.</summary>
    Task<TransitionResult> FinishSessionAsync(CancellationToken cancellationToken = default);

    Task<TransitionResult> RecoverAsync(
        RecoveryDecision decision,
        CancellationToken cancellationToken = default);
}
