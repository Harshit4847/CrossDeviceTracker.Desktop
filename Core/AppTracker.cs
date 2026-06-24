using System.Diagnostics;
using System.Runtime.InteropServices;
using CrossDeviceTracker.Desktop.Models;
using CrossDeviceTracker.Desktop.Data;

namespace CrossDeviceTracker.Desktop.Core;

public class AppTracker
{
    private const int PollingIntervalMs = 2000;
    private const string LockAppProcessName = "LockApp";

    private readonly ILogRepository _repository;
    private string? _previousApp;
    private string? _currentApp;
    private DateTime _sessionStartTime;
    private bool _isRunning;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public AppTracker(ILogRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public string? GetCurrentAppName() => _currentApp;

    public async Task StartAsync()
    {
        await _repository.InitializeAsync();

        _isRunning = true;
        var initialApp = GetCurrentApp();
        _currentApp = initialApp;
        _previousApp = ToTrackableApp(initialApp);
        _sessionStartTime = DateTime.UtcNow;

        Console.WriteLine("🎯 App Tracker started");
        Console.WriteLine($"Initial app: {initialApp ?? "N/A"}");
        if (IsLockApp(initialApp))
            Console.WriteLine("Lock screen active — LockApp excluded from tracking");
        Console.WriteLine("Polling every 2 seconds...\n");

        while (_isRunning)
        {
            try
            {
                var rawApp = GetCurrentApp();
                var trackableApp = ToTrackableApp(rawApp);
                _currentApp = rawApp;
                var currentTime = DateTime.UtcNow;

                if (trackableApp != _previousApp)
                {
                    if (_previousApp != null)
                    {
                        await FinalizeSessionAsync(currentTime);
                    }

                    _sessionStartTime = currentTime;
                    _previousApp = trackableApp;

                    if (trackableApp != null)
                    {
                        Console.WriteLine($"→ App changed to: {trackableApp}");
                    }
                    else if (IsLockApp(rawApp))
                    {
                        Console.WriteLine("→ System locked (LockApp ignored)");
                    }
                }

                await Task.Delay(PollingIntervalMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in tracking loop: {ex.Message}");
                await Task.Delay(PollingIntervalMs);
            }
        }
    }

    public async Task StopAsync()
    {
        Console.WriteLine("\n⏹️  Stopping tracker...");

        if (_previousApp != null)
        {
            try
            {
                await FinalizeSessionAsync(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Failed to save final session on shutdown: {ex.Message}");
            }
        }

        _isRunning = false;
        Console.WriteLine("✅ Tracker stopped");
    }

    private async Task FinalizeSessionAsync(DateTime endTime)
    {
        var duration = endTime - _sessionStartTime;

        var log = new Log
        {
            AppName = _previousApp!,
            StartTime = _sessionStartTime,
            EndTime = endTime,
            Duration = duration,
            SyncStatus = SyncStatus.Pending
        };

        await _repository.SaveLogAsync(log);
        Console.WriteLine($"✓ Logged: {log}");
    }

    private static bool IsLockApp(string? appName) =>
        string.Equals(appName, LockAppProcessName, StringComparison.OrdinalIgnoreCase);

    private static string? ToTrackableApp(string? appName) =>
        IsLockApp(appName) ? null : appName;

    private string? GetCurrentApp()
    {
        try
        {
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return null;

            GetWindowThreadProcessId(foregroundWindow, out uint processId);

            if (processId == 0)
                return null;

            var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Could not detect foreground app: {ex.Message}");
            return null;
        }
    }
}
