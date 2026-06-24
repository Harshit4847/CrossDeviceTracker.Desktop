using CrossDeviceTracker.Desktop.Core.Helpers;

namespace CrossDeviceTracker.Desktop.Services;

public interface ISyncService
{
    Task StartAsync();
    Task StopAsync();
    Task SyncOnceAsync();
}

public class SyncService : PollingServiceBase, ISyncService
{
    private const int DefaultSyncIntervalMs = 30000; // 30 seconds
    private readonly IApiClient _apiClient;

    public SyncService(IApiClient apiClient)
        : base(DefaultSyncIntervalMs, "Sync Service")
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public Task SyncOnceAsync() => ExecuteAsync();

    protected override async Task ExecuteAsync()
    {
        await _apiClient.SyncPendingLogsAsync();
    }
}
