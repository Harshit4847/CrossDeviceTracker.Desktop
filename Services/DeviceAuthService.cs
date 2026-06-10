using System.Text;
using System.Text.Json;

namespace CrossDeviceTracker.Desktop.Services;

public interface IDeviceAuthService
{
    Task<string?> GetDeviceJwtAsync();
    Task SaveDeviceJwtAsync(string jwt);
    Task<string?> GetDeviceIdAsync();
    Task SaveDeviceIdAsync(string deviceId);
    Task<bool> IsAuthenticatedAsync();
}

public interface IDeviceLinkingService
{
    Task<bool> LinkDeviceAsync(string userEmail, string userPassword);
    Task<bool> UnlinkDeviceAsync();
}

public class DeviceAuthService : IDeviceAuthService
{
    private const string ConfigFileName = "appsettings.json";
    private readonly string _configPath;

    public DeviceAuthService()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
    }

    public async Task<string?> GetDeviceJwtAsync()
    {
        try
        {
            var config = await LoadConfigAsync();
            return config.TryGetValue("Device:Jwt", out var jwt) ? (string?)jwt : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveDeviceJwtAsync(string jwt)
    {
        var config = await LoadConfigAsync();
        config["Device:Jwt"] = jwt;
        await SaveConfigAsync(config);
    }

    public async Task<string?> GetDeviceIdAsync()
    {
        try
        {
            var config = await LoadConfigAsync();
            var deviceId = config.TryGetValue("Device:Id", out var id) ? (string?)id : null;

            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
                config["Device:Id"] = deviceId;
                await SaveConfigAsync(config);
            }

            return deviceId;
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }

    public async Task SaveDeviceIdAsync(string deviceId)
    {
        var config = await LoadConfigAsync();
        config["Device:Id"] = deviceId;
        await SaveConfigAsync(config);
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var jwt = await GetDeviceJwtAsync();
        return !string.IsNullOrEmpty(jwt);
    }

    private async Task<Dictionary<string, object?>> LoadConfigAsync()
    {
        if (!File.Exists(_configPath))
        {
            return new Dictionary<string, object?>
            {
                { "Api:BaseUrl", "https://crossdevicetracker-api-hy-erhyaffahwaufsba.southeastasia-01.azurewebsites.net" },
                { "Api:TimeoutSeconds", 30 },
                { "Api:SyncIntervalSeconds", 30 },
                { "Device:Id", null },
                { "Device:Jwt", null }
            };
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configPath);
            using (var doc = JsonDocument.Parse(json))
            {
                var config = new Dictionary<string, object?>();
                FlattenJsonElement(doc.RootElement, "", config);
                return config;
            }
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private async Task SaveConfigAsync(Dictionary<string, object?> config)
    {
        try
        {
            using (var stream = File.Create(_configPath))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                // Api section
                writer.WritePropertyName("Api");
                writer.WriteStartObject();
                writer.WriteString("BaseUrl", (string?)config["Api:BaseUrl"]);
                writer.WriteNumber("TimeoutSeconds", ((int?)config["Api:TimeoutSeconds"]) ?? 30);
                writer.WriteNumber("SyncIntervalSeconds", ((int?)config["Api:SyncIntervalSeconds"]) ?? 30);
                writer.WriteEndObject();

                // Device section
                writer.WritePropertyName("Device");
                writer.WriteStartObject();
                writer.WriteString("Id", (string?)config["Device:Id"]);
                writer.WriteString("Jwt", (string?)config["Device:Jwt"]);
                writer.WriteEndObject();

                writer.WriteEndObject();
                await writer.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving config: {ex.Message}");
        }
    }

    private void FlattenJsonElement(JsonElement element, string prefix, Dictionary<string, object?> dict)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}:{property.Name}";

            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    FlattenJsonElement(property.Value, key, dict);
                    break;
                case JsonValueKind.String:
                    dict[key] = property.Value.GetString();
                    break;
                case JsonValueKind.Number:
                    dict[key] = property.Value.GetInt32();
                    break;
                case JsonValueKind.Null:
                    dict[key] = null;
                    break;
                default:
                    dict[key] = property.Value.ToString();
                    break;
            }
        }
    }
}

public class DeviceLinkingService : IDeviceLinkingService
{
    private const string DevicesEndpoint = "/api/devices/link";
    private const string BaseUrl = "https://crossdevicetracker-api-hy-erhyaffahwaufsba.southeastasia-01.azurewebsites.net";
    private readonly IDeviceAuthService _deviceAuthService;
    private readonly HttpClient _httpClient;

    public DeviceLinkingService(IDeviceAuthService deviceAuthService)
    {
        _deviceAuthService = deviceAuthService ?? throw new ArgumentNullException(nameof(deviceAuthService));
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<bool> LinkDeviceAsync(string userEmail, string userPassword)
    {
        try
        {
            var deviceId = await _deviceAuthService.GetDeviceIdAsync();

            if (string.IsNullOrEmpty(deviceId))
            {
                Console.WriteLine("❌ Failed to get device ID");
                return false;
            }

            // Prepare the request body
            var payload = new
            {
                email = userEmail,
                password = userPassword,
                deviceId = deviceId,
                deviceName = Environment.MachineName,
                deviceType = "Windows"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Send request to backend
            var response = await _httpClient.PostAsync($"{BaseUrl}{DevicesEndpoint}", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(responseContent))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("token", out var tokenElement))
                    {
                        var jwt = tokenElement.GetString();
                        if (!string.IsNullOrEmpty(jwt))
                        {
                            await _deviceAuthService.SaveDeviceJwtAsync(jwt);
                            Console.WriteLine($"✅ Device linked successfully!");
                            Console.WriteLine($"   Device ID: {deviceId}");
                            Console.WriteLine($"   Device Name: {Environment.MachineName}");
                            return true;
                        }
                    }
                }

                Console.WriteLine("❌ No JWT token in response");
                return false;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Linking failed: {response.StatusCode} - {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during device linking: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UnlinkDeviceAsync()
    {
        try
        {
            var deviceId = await _deviceAuthService.GetDeviceIdAsync();
            var jwt = await _deviceAuthService.GetDeviceJwtAsync();

            if (string.IsNullOrEmpty(jwt))
            {
                Console.WriteLine("⚠️  Device not linked");
                return false;
            }

            // Clear local JWT
            await _deviceAuthService.SaveDeviceJwtAsync("");
            Console.WriteLine("✅ Device unlinked successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during device unlinking: {ex.Message}");
            return false;
        }
    }
}
