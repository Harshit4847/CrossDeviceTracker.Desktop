namespace CrossDeviceTracker.Desktop.Core.Helpers;

/// <summary>
/// Base class for services that poll on a fixed interval.
/// Subclasses implement ExecuteAsync for the per-tick work.
/// </summary>
public abstract class PollingServiceBase
{
    private readonly int _intervalMs;
    private readonly string _serviceName;
    private bool _isRunning;

    protected PollingServiceBase(int intervalMs, string serviceName)
    {
        _intervalMs = intervalMs;
        _serviceName = serviceName;
    }

    public async Task StartAsync()
    {
        _isRunning = true;
        OnStarted();

        while (_isRunning)
        {
            try
            {
                await ExecuteAsync();
                await Task.Delay(_intervalMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {_serviceName} error: {ex.Message}");
                await Task.Delay(_intervalMs);
            }
        }
    }

    public virtual async Task StopAsync()
    {
        Console.WriteLine($"⏹️  Stopping {_serviceName}...");
        _isRunning = false;

        try
        {
            await ExecuteAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Final {_serviceName} attempt failed: {ex.Message}");
        }

        Console.WriteLine($"✅ {_serviceName} stopped");
    }

    protected void SetStopped() => _isRunning = false;

    protected abstract Task ExecuteAsync();

    protected virtual void OnStarted()
    {
        Console.WriteLine($"🔄 {_serviceName} started (interval: {_intervalMs / 1000}s)");
    }
}
