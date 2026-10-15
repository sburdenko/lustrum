namespace Lustrum.Core.Watching;

public sealed class DebouncedTrigger : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly Timer _timer;
    private volatile bool _disposed;

    public DebouncedTrigger(TimeSpan delay, Action action)
    {
        _delay = delay;
        _timer = new Timer(_ =>
        {
            if (!_disposed)
            {
                action();
            }
        }, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Signal()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }
}
