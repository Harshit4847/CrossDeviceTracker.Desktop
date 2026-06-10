using CrossDeviceTracker.Desktop.Data;
using CrossDeviceTracker.Desktop.Models;
using CrossDeviceTracker.Desktop.Services;

namespace CrossDeviceTracker.Desktop;

/// <summary>
/// Helper class for testing and debugging the sync flow.
/// This provides utilities to inspect database state and sync status.
/// </summary>
public class SyncDebugHelper
{
    private readonly ILogRepository _repository;
    private readonly IDeviceAuthService _deviceAuthService;

    public SyncDebugHelper(ILogRepository repository, IDeviceAuthService deviceAuthService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _deviceAuthService = deviceAuthService ?? throw new ArgumentNullException(nameof(deviceAuthService));
    }

    /// <summary>
    /// Print current device and authentication status
    /// </summary>
    public async Task PrintDeviceStatusAsync()
    {
        Console.WriteLine("\n========== DEVICE STATUS ==========");

        try
        {
            var deviceId = await _deviceAuthService.GetDeviceIdAsync();
            var isAuthenticated = await _deviceAuthService.IsAuthenticatedAsync();

            Console.WriteLine($"Device ID: {deviceId}");
            Console.WriteLine($"Authenticated: {(isAuthenticated ? "✅ Yes" : "❌ No")}");

            if (isAuthenticated)
            {
                var jwt = await _deviceAuthService.GetDeviceJwtAsync();
                Console.WriteLine($"JWT Token: {(jwt?.Substring(0, 20) + "...")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }

        Console.WriteLine("====================================\n");
    }

    /// <summary>
    /// Print all logs in the database with their sync status
    /// </summary>
    public async Task PrintAllLogsAsync()
    {
        Console.WriteLine("\n========== ALL LOGS IN DATABASE ==========");

        try
        {
            var logs = await _repository.GetPendingLogsAsync();

            if (logs.Count == 0)
            {
                Console.WriteLine("No logs found in database.");
                Console.WriteLine("==========================================\n");
                return;
            }

            Console.WriteLine($"Total logs: {logs.Count}\n");
            Console.WriteLine($"{"#",-5} | {"App Name",-20} | {"Time",-10} | {"Duration",-10} | {"Status",-10}");
            Console.WriteLine(new string('-', 70));

            foreach (var log in logs.OrderByDescending(l => l.StartTime))
            {
                var index = logs.IndexOf(log) + 1;
                Console.WriteLine(
                    $"{index,-5} | {log.AppName,-20} | {log.StartTime:HH:mm:ss,-10} | {log.Duration.TotalSeconds,6:F0}s | {log.SyncStatus,-10}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }

        Console.WriteLine("==========================================\n");
    }

    /// <summary>
    /// Print sync statistics
    /// </summary>
    public async Task PrintSyncStatisticsAsync()
    {
        Console.WriteLine("\n========== SYNC STATISTICS ==========");

        try
        {
            var allLogs = await _repository.GetPendingLogsAsync();
            
            var sent = allLogs.Count(l => l.SyncStatus == SyncStatus.Sent);
            var pending = allLogs.Count(l => l.SyncStatus == SyncStatus.Pending);
            var failed = allLogs.Count(l => l.SyncStatus == SyncStatus.Failed);

            Console.WriteLine($"Total Logs: {allLogs.Count}");
            Console.WriteLine($"  ✅ Sent:    {sent}");
            Console.WriteLine($"  ⏳ Pending: {pending}");
            Console.WriteLine($"  ❌ Failed:  {failed}");

            if (pending > 0)
            {
                Console.WriteLine($"\n⚠️  {pending} log(s) waiting to sync. Next sync in ~30 seconds.");
            }
            else if (sent > 0)
            {
                Console.WriteLine("\n✅ All logs successfully synced!");
            }
            else
            {
                Console.WriteLine("\n📝 No logs to sync yet. Switch between applications to generate logs.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }

        Console.WriteLine("=====================================\n");
    }

    /// <summary>
    /// Print complete diagnostic report
    /// </summary>
    public async Task PrintDiagnosticReportAsync()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          CROSSDEVICETRACKER - DIAGNOSTIC REPORT                ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

        await PrintDeviceStatusAsync();
        await PrintSyncStatisticsAsync();
        await PrintAllLogsAsync();

        Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");
    }
}
