using System.Text.Json;
using CrossDeviceTracker.Desktop.Core.Helpers;
using CrossDeviceTracker.Desktop.Data;
using CrossDeviceTracker.Desktop.Models;

namespace CrossDeviceTracker.Desktop.Services;

public interface IApiClient
{
    event EventHandler? DeviceUnauthorized;
    string? DeviceJwt { get; set; }
    Task<bool> SyncPendingLogsAsync();
    Task<bool> SendLogAsync(Log log);
    Task<LinkDesktopResponse> LinkDeviceAsync(string linkToken);
}

public class ApiClient : IApiClient
{
    private const string TimelogsEndpoint = "/api/timelogs";
    private readonly HttpClient _httpClient;
    private readonly ILogRepository _repository;
    private readonly string _baseUrl = "https://crossdevicetracker-api-hy-erhyaffahwaufsba.southeastasia-01.azurewebsites.net";

    public event EventHandler? DeviceUnauthorized;
    public string? DeviceJwt { get; set; }

    public ApiClient(ILogRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<bool> SyncPendingLogsAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(DeviceJwt))
            {
                Console.WriteLine("⚠️  Device not authenticated. Sync skipped. Please link device first.");
                return false;
            }

            var logs = await _repository.GetPendingLogsAsync();
            if (logs.Count == 0)
            {
                return true;
            }

            Console.WriteLine($"\n🔄 Sync Service: Syncing {logs.Count} log(s)...");
            Console.WriteLine($"   Base URL: {_baseUrl}{TimelogsEndpoint}");

            int successCount = 0;
            int failureCount = 0;

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
            var jwt = DeviceJwt;

            if (string.IsNullOrEmpty(jwt))
            {
                Console.WriteLine("❌ Missing authentication credentials");
                return false;
            }

            var payload = new
            {
                deviceId = (string?)null,
                appName = log.AppName,
                startTime = log.StartTime.ToString("O"),
                durationSeconds = (int)log.Duration.TotalSeconds
            };

            var content = HttpHelper.CreateJsonContent(payload);

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            var response = await _httpClient.PostAsync(
                $"{_baseUrl}{TimelogsEndpoint}",
                content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"  ✓ Sent: {log.AppName} ({log.Duration.TotalSeconds:F0}s)");
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                DeviceUnauthorized?.Invoke(this, EventArgs.Empty);
                Console.WriteLine("  Device authorization expired. Relink required.");
                return false;
            }
            else
            {
                var errorMessage = await HttpHelper.ExtractErrorMessageAsync(response, $"Failed to send log for {log.AppName}");
                Console.WriteLine($"  ❌ Failed: {log.AppName} - {response.StatusCode} - {errorMessage}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Error sending {log.AppName}: {ex.Message}");
            return false;
        }
    }

    public async Task<LinkDesktopResponse> LinkDeviceAsync(string linkToken)
    {
        if (string.IsNullOrWhiteSpace(linkToken))
        {
            throw new ArgumentException("Link token is required.", nameof(linkToken));
        }

        var payload = new
        {
            linkToken = linkToken.Trim(),
            deviceName = Environment.MachineName,
            platform = "Windows"
        };

        var content = HttpHelper.CreateJsonContent(payload);
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/devices/link", content);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LinkDesktopResponse>(responseContent, JsonDefaults.ReadOptions);
            if (result == null || string.IsNullOrWhiteSpace(result.DeviceJwt))
            {
                throw new Exception("Invalid response from server: Device JWT is empty.");
            }
            return result;
        }
        else
        {
            var errorMessage = await HttpHelper.ExtractErrorMessageAsync(response, "Failed to link device.");
            throw new Exception(errorMessage);
        }
    }
}
