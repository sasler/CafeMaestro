namespace CafeMaestro.Services;

/// <summary>
/// The single source of "now" for the roast domain, so transitions and recovery stay testable.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
