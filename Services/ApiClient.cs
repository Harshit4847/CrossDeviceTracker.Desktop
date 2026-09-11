using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
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
    private string _baseUrl = "https://crossdevicetracker-api-hy-erhyaffahwaufsba.southeastasia-01.azurewebsites.net";
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

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
                Console.WriteLine("⚠️ Device not authenticated. Sync skipped.");
                return false;
            }

            var logs = await _repository.GetPendingLogsAsync();
            if (logs.Count == 0) return true;

            int successCount = 0;
            int failureCount = 0;
            foreach (var log in logs)
            {
                if (await SendLogAsync(log))
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

            Console.WriteLine($"✅ Sync complete: {successCount} succeeded, {failureCount} failed");
            return failureCount == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Sync error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendLogAsync(Log log)
    {
        Debug.WriteLine($">>> SendLogAsync: {log.AppName}");

        try
        {
            var jwt = DeviceJwt;
            if (string.IsNullOrEmpty(jwt)) return false;

            // Log.Id is the stable local identifier. It becomes ClientSessionId on the API.
            var payload = new
            {
                clientSessionId = log.Id.ToString(),
                appName = log.AppName,
                startTimeUtc = log.StartTime,
                endTimeUtc = log.EndTime,
                durationSeconds = (int)log.Duration.TotalSeconds,
                createdAtUtc = log.CreatedAt
            };

            Debug.WriteLine("=== SYNC REQUEST ===");
            Debug.WriteLine(JsonSerializer.Serialize(payload));

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            using var response = await _httpClient.PostAsync($"{_baseUrl}{TimelogsEndpoint}", content);
            var body = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"Status: {response.StatusCode}");
            Debug.WriteLine(body);

            if (response.IsSuccessStatusCode) return true;

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                DeviceUnauthorized?.Invoke(this, EventArgs.Empty);
                Console.WriteLine("Device authorization expired. Relink required.");
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Send log error: {ex.Message}");
            return false;
        }
    }

    public async Task<LinkDesktopResponse> LinkDeviceAsync(string linkToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(linkToken))
                throw new ArgumentException("Link token is required.", nameof(linkToken));

            var payload = new
            {
                linkToken = linkToken.Trim(),
                deviceName = Environment.MachineName,
                platform = "Windows"
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/devices/link", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<LinkDesktopResponse>(responseContent, _jsonOptions);
                if (result == null || string.IsNullOrWhiteSpace(result.DeviceJwt))
                    throw new Exception("Invalid response from server: Device JWT is empty.");
                return result;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception(string.IsNullOrWhiteSpace(errorContent) ? "Failed to link device." : errorContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Link device error: {ex.Message}");
            throw;
        }
    }
}
