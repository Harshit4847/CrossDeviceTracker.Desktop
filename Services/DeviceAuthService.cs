using System.Text.Json;

namespace CrossDeviceTracker.Desktop.Services;

public interface IDeviceAuthService
{
    Task<bool> IsLinkedAsync();
    Task<string?> LoadDeviceJwtAsync();
    Task<DeviceAuthState?> LoadDeviceAsync();
    Task LinkDeviceAsync(string linkToken);
    Task SaveDeviceJwtAsync(string deviceJwt);
    Task UnlinkAsync();
}

public sealed class DeviceAuthState
{
    public required string DeviceId { get; set; }
    public required string DeviceJwt { get; set; }
    public string? DeviceName { get; set; }
    public DateTime? LinkedAt { get; set; }
    public bool Verify { get; set; }
}

public class DeviceAuthService : IDeviceAuthService
{
    private const string DeviceFileName = "device.json";
    private readonly string _deviceFilePath;
    private readonly IApiClient _apiClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public DeviceAuthService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _deviceFilePath = Path.Combine(AppContext.BaseDirectory, DeviceFileName);
    }

    public async Task<bool> IsLinkedAsync()
    {
        var jwt = await LoadDeviceJwtAsync();
        return !string.IsNullOrWhiteSpace(jwt);
    }

    public async Task<string?> LoadDeviceJwtAsync()
    {
        var device = await LoadDeviceAsync();
        return device?.DeviceJwt;
    }

    public async Task<DeviceAuthState?> LoadDeviceAsync()
    {
        if (!File.Exists(_deviceFilePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(_deviceFilePath);
            var device = await JsonSerializer.DeserializeAsync<DeviceAuthState>(stream, _jsonOptions);
            return string.IsNullOrWhiteSpace(device?.DeviceJwt) ? null : device;
        }
        catch
        {
            return null;
        }
    }

    public async Task LinkDeviceAsync(string linkToken)
    {
        if (string.IsNullOrWhiteSpace(linkToken))
        {
            throw new ArgumentException("Link token is required.", nameof(linkToken));
        }

        var response = await _apiClient.LinkDeviceAsync(linkToken);
        if (string.IsNullOrWhiteSpace(response?.DeviceJwt))
        {
            throw new Exception("Device JWT not received from server.");
        }

        var device = new DeviceAuthState
        {
            DeviceJwt = response.DeviceJwt.Trim(),
            DeviceName = Environment.MachineName,
            LinkedAt = DateTime.UtcNow
        };

        await SaveDeviceAsync(device);
        _apiClient.DeviceJwt = device.DeviceJwt;
    }

    public async Task SaveDeviceJwtAsync(string deviceJwt)
    {
        if (string.IsNullOrWhiteSpace(deviceJwt))
        {
            throw new ArgumentException("Device JWT is required.", nameof(deviceJwt));
        }

        var currentDevice = await LoadDeviceAsync();
        var device = new DeviceAuthState
        {
            DeviceJwt = deviceJwt.Trim(),
            DeviceName = currentDevice?.DeviceName ?? Environment.MachineName,
            LinkedAt = currentDevice?.LinkedAt ?? DateTime.UtcNow
        };

        await SaveDeviceAsync(device);
        _apiClient.DeviceJwt = device.DeviceJwt;
    }

    public Task UnlinkAsync()
    {
        if (File.Exists(_deviceFilePath))
        {
            File.Delete(_deviceFilePath);
        }

        _apiClient.DeviceJwt = null;
        return Task.CompletedTask;
    }

    private async Task SaveDeviceAsync(DeviceAuthState device)
    {
        var directory = Path.GetDirectoryName(_deviceFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_deviceFilePath);
        await JsonSerializer.SerializeAsync(stream, device, _jsonOptions);
    }
}
