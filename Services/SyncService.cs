namespace CrossDeviceTracker.Desktop.Services;

public interface ISyncService
{
    bool IsPaused { get; }
    Task StartAsync();
    Task StopAsync();
    Task SyncOnceAsync();
    void Pause();
    void Resume();
}

public class SyncService : ISyncService
{
    private const int DefaultSyncIntervalMs = 30000; // 30 seconds
    private readonly IApiClient _apiClient;
    private bool _isRunning;
    private bool _isPaused;

    public bool IsPaused => _isPaused;

    public SyncService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public async Task StartAsync()
    {
        _isRunning = true;
        Console.WriteLine("🔄 Sync Service started (interval: 30s)");

        while (_isRunning)
        {
            try
            {
                if (!_isPaused)
                {
                    await SyncOnceAsync();
                }
                await Task.Delay(DefaultSyncIntervalMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Sync service error: {ex.Message}");
                await Task.Delay(DefaultSyncIntervalMs);
            }
        }
    }

    public async Task StopAsync()
    {
        Console.WriteLine("⏹️  Stopping Sync Service...");
        _isRunning = false;

        // Final sync attempt
        try
        {
            await SyncOnceAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Final sync attempt failed: {ex.Message}");
        }

        Console.WriteLine("✅ Sync Service stopped");
    }

    public void Pause()
    {
        _isPaused = true;
        Console.WriteLine("⏸️  Sync Service paused (auth error)");
    }

    public void Resume()
    {
        _isPaused = false;
        Console.WriteLine("▶️  Sync Service resumed");
    }

    public async Task SyncOnceAsync()
    {
        try
        {
            await _apiClient.SyncPendingLogsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during sync: {ex.Message}");
        }
    }
}
