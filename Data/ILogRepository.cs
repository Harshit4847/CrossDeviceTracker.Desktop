using CrossDeviceTracker.Desktop.Models;

namespace CrossDeviceTracker.Desktop.Data;

public interface ILogRepository
{
    Task SaveLogAsync(Log log);
    Task<List<Log>> GetPendingLogsAsync();
    Task UpdateSyncStatusAsync(Guid logId, SyncStatus status);
    Task InitializeAsync();
    Task DeleteAllLogsAsync();
}
