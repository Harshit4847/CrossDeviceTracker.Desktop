using System.Diagnostics;

namespace CrossDeviceTracker.Desktop.Services;

public interface ISyncService
{
    Task StartAsync();
    Task StopAsync();
    Task SyncOnceAsync();
}

public class SyncService : ISyncService
{
    private const int DefaultSyncIntervalMs = 30000; // 30 seconds
    private readonly IApiClient _apiClient;
    private bool _isRunning;

    public SyncService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public async Task StartAsync()
    {
        _isRunning = true;
        Debug.WriteLine("4. StartAsync entered");
        Debug.WriteLine("🔄 Sync Service started (interval: 30s)");

        while (_isRunning)
        {
            try
            {
                Debug.WriteLine(">>> Before SyncOnceAsync");

                await SyncOnceAsync();

                Debug.WriteLine(">>> After SyncOnceAsync");

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

    public async Task SyncOnceAsync()
    {
        Debug.WriteLine(">>> Entered SyncOnceAsync");

        try
        {
            Debug.WriteLine(">>> Before SyncPendingLogsAsync");

            await _apiClient.SyncPendingLogsAsync();

            Debug.WriteLine(">>> After SyncPendingLogsAsync");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ SyncOnceAsync Exception: {ex}");
        }
    }
}
