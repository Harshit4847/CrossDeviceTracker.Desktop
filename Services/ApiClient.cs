using System.Text;
using System.Text.Json;
using CrossDeviceTracker.Desktop.Data;
using CrossDeviceTracker.Desktop.Models;

namespace CrossDeviceTracker.Desktop.Services;

public interface IApiClient
{
    Task<bool> SyncPendingLogsAsync();
    Task<bool> SendLogAsync(Log log);
}

public class ApiClient : IApiClient
{
    private const string TimelogsEndpoint = "/api/timelogs";
    private readonly HttpClient _httpClient;
    private readonly IDeviceAuthService _authService;
    private readonly ILogRepository _repository;
    private string _baseUrl = "https://crossdevicetracker-api-hy-erhyaffahwaufsba.southeastasia-01.azurewebsites.net";

    public ApiClient(IDeviceAuthService authService, ILogRepository repository)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<bool> SyncPendingLogsAsync()
    {
        try
        {
            // Check if device is authenticated
            var isAuthenticated = await _authService.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                Console.WriteLine("⚠️  Device not authenticated. Sync skipped. Please link device first.");
                return false;
            }

            // Get pending logs
            var logs = await _repository.GetPendingLogsAsync();
            if (logs.Count == 0)
            {
                // Silent - don't spam console every 30 seconds with no logs
                return true;
            }

            Console.WriteLine($"\n🔄 Sync Service: Syncing {logs.Count} log(s)...");
            Console.WriteLine($"   Base URL: {_baseUrl}{TimelogsEndpoint}");

            int successCount = 0;
            int failureCount = 0;

            // Send each log individually (batch endpoint not yet available)
            foreach (var log in logs)
            {
                var success = await SendLogAsync(log);
                if (success)
                {
                    successCount++;
                    await _repository.UpdateSyncStatusAsync(log.Id, SyncStatus.Sent);
                }
                else
                {
                    failureCount++;
                    await _repository.UpdateSyncStatusAsync(log.Id, SyncStatus.Failed);
                }
            }

            Console.WriteLine($"✅ Sync complete: {successCount} succeeded, {failureCount} failed\n");
            return failureCount == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Sync error: {ex.Message}\n");
            return false;
        }
    }

    public async Task<bool> SendLogAsync(Log log)
    {
        try
        {
            var jwt = await _authService.GetDeviceJwtAsync();
            var deviceId = await _authService.GetDeviceIdAsync();

            if (string.IsNullOrEmpty(jwt) || string.IsNullOrEmpty(deviceId))
            {
                Console.WriteLine("❌ Missing authentication credentials");
                return false;
            }

            // Build the request body
            var payload = new
            {
                deviceId = deviceId,
                appName = log.AppName,
                startTime = log.StartTime.ToString("O"),
                durationSeconds = (int)log.Duration.TotalSeconds
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add authorization header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            // Send request
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}{TimelogsEndpoint}",
                content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"  ✓ Sent: {log.AppName} ({log.Duration.TotalSeconds:F0}s)");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"  ❌ Failed: {log.AppName} - {response.StatusCode} - {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Error sending {log.AppName}: {ex.Message}");
            return false;
        }
    }
}
