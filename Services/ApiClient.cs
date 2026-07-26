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
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
            // Check if device is authenticated
            Console.WriteLine($"Device JWT exists: {!string.IsNullOrWhiteSpace(DeviceJwt)}");
            if (string.IsNullOrWhiteSpace(DeviceJwt))
            {
                Console.WriteLine("⚠️ Device not authenticated. Sync skipped.");
                return false;
            }

            // Get pending logs
            var logs = await _repository.GetPendingLogsAsync();
            Console.WriteLine($"Pending logs: {logs.Count}");
            if (logs.Count == 0)
            {
                Console.WriteLine("No pending logs.");
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
        Debug.WriteLine($">>> SendLogAsync: {log.AppName}");

        try
        {
            var jwt = DeviceJwt;

            if (string.IsNullOrEmpty(jwt))
            {
                Console.WriteLine("❌ Missing authentication credentials");
                return false;
            }

            // Build the request body
            var payload = new
            {
                appName = log.AppName,
                startTimeUtc = log.StartTime,
                endTimeUtc = log.StartTime.Add(log.Duration),
                durationSeconds = (int)log.Duration.TotalSeconds,
                createdAtUtc = log.CreatedAt
            };

            // Debug: log request
            Debug.WriteLine("=== SYNC REQUEST ===");
            Debug.WriteLine(JsonSerializer.Serialize(payload));
            Debug.WriteLine($"JWT present: {!string.IsNullOrWhiteSpace(jwt)}");

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add authorization header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            // Send request
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}{TimelogsEndpoint}",
                content);

            // Debug: log response
            Debug.WriteLine($"Status: {response.StatusCode}");
            var body = await response.Content.ReadAsStringAsync();
            Debug.WriteLine(body);

            if (response.IsSuccessStatusCode)
            {
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
                Debug.WriteLine($"❌ Failed: {log.AppName} - {response.StatusCode}");
                Debug.WriteLine(body);
                return false;
            }
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
            {
                throw new ArgumentException("Link token is required.", nameof(linkToken));
            }

            var payload = new
            {
                linkToken = linkToken.Trim(),
                deviceName = Environment.MachineName,
                platform = "Windows"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/devices/link", content);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine("=== LINK RESPONSE ===");
            Console.WriteLine(responseContent);
            MessageBox.Show(responseContent, "Raw Server Response");
            var result = JsonSerializer.Deserialize<LinkDesktopResponse>(responseContent, _jsonOptions);
            if (result == null || string.IsNullOrWhiteSpace(result.DeviceJwt))
            {
                throw new Exception("Invalid response from server: Device JWT is empty.");
            }
            return result;
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            string errorMessage = "Failed to link device.";
            try
            {
                using var doc = JsonDocument.Parse(errorContent);
                if (doc.RootElement.TryGetProperty("message", out var msgProp))
                {
                    errorMessage = msgProp.GetString() ?? errorMessage;
                }
                else if (doc.RootElement.TryGetProperty("error", out var errProp))
                {
                    errorMessage = errProp.GetString() ?? errorMessage;
                }
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(errorContent))
                {
                    errorMessage = errorContent;
                }
            }
            throw new Exception(errorMessage);
        }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Link device error: {ex.Message}");
            throw;
        }
    }
}
