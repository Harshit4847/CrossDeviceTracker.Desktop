using System.Diagnostics;
using System.Runtime.InteropServices;
using CrossDeviceTracker.Desktop.Models;
using CrossDeviceTracker.Desktop.Data;

namespace CrossDeviceTracker.Desktop.Core;

public class AppTracker
{
    private const int PollingIntervalMs = 2000;

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
        _previousApp = GetCurrentApp();
        _currentApp = _previousApp;
        _sessionStartTime = DateTime.UtcNow;

        Console.WriteLine("🎯 App Tracker started");
        Console.WriteLine($"Initial app: {_previousApp}");
        Console.WriteLine("Polling every 2 seconds...\n");

        while (_isRunning)
        {
            try
            {
                var currentApp = GetCurrentApp();
                _currentApp = currentApp;
                var currentTime = DateTime.UtcNow;

                if (currentApp != _previousApp)
                {
                    if (_previousApp != null)
                    {
                        await FinalizeSessionAsync(currentTime);
                    }

                    _sessionStartTime = currentTime;
                    _previousApp = currentApp;
                    Console.WriteLine($"→ App changed to: {currentApp}");
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
            await FinalizeSessionAsync(DateTime.UtcNow);
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
        catch
        {
            return null;
        }
    }
}
