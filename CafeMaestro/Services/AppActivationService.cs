namespace CafeMaestro.Services;

public sealed class AppActivationService : IAppActivationService
{
    private readonly object _sync = new();
    private readonly IAppActivationHandler _handler;
    private AppActivationPayload? _pending;
    private bool _isReady;

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

    public void SetReady()
    {
        lock (_sync)
        {
            _isReady = true;
        }
    }

    public async Task HandlePendingAsync(CancellationToken cancellationToken = default)
    {
        AppActivationPayload? payload;
        lock (_sync)
        {
            if (!_isReady)
            {
                return;
            }
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
