namespace CafeMaestro.Services;

/// <summary>
/// Platform-neutral payload captured by a native activation adapter. Ticket 10 supplies the
/// Android payload producer and domain-aware handler.
/// </summary>
public sealed record AppActivationPayload(
    string Kind,
    IReadOnlyDictionary<string, string> Values);

public interface IAppActivationService
{
    void Queue(AppActivationPayload payload);

    /// <summary>Marks data initialization and Shell presentation complete.</summary>
    void SetReady();

    /// <summary>Runs only after data initialization and Shell presentation have completed.</summary>
    Task HandlePendingAsync(CancellationToken cancellationToken = default);
}

public interface IAppActivationHandler
{
    Task HandleAsync(AppActivationPayload payload, CancellationToken cancellationToken = default);
}
