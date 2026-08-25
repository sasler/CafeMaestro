namespace CafeMaestro.Services;

public sealed class AppActivationService : IAppActivationService
{
    private readonly object _sync = new();
    private readonly IAppActivationHandler _handler;
    private AppActivationPayload? _pending;

    public AppActivationService(IAppActivationHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void Queue(AppActivationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        lock (_sync)
        {
            _pending = payload;
        }
    }

    public async Task HandlePendingAsync(CancellationToken cancellationToken = default)
    {
        AppActivationPayload? payload;
        lock (_sync)
        {
            payload = _pending;
            _pending = null;
        }

        if (payload is null)
        {
            return;
        }

        try
        {
            await _handler.HandleAsync(payload, cancellationToken);
        }
        catch
        {
            lock (_sync)
            {
                _pending ??= payload;
            }

            throw;
        }
    }
}

/// <summary>Cross-platform placeholder until a platform activation destination is implemented.</summary>
public sealed class NoOpAppActivationHandler : IAppActivationHandler
{
    public Task HandleAsync(
        AppActivationPayload payload,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
