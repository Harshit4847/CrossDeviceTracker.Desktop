using CrossDeviceTracker.Desktop;
using CrossDeviceTracker.Desktop.Data;
using CrossDeviceTracker.Desktop.Services;

ApplicationConfiguration.Initialize();

var logRepository = new SqliteLogRepository();
var apiClient = new ApiClient(logRepository);
var deviceAuthService = new DeviceAuthService(apiClient);

// Initialize DeviceJwt on apiClient from saved state
var device = await deviceAuthService.LoadDeviceAsync();
if (device != null)
{
    apiClient.DeviceJwt = device.DeviceJwt;
}

if (!await deviceAuthService.IsLinkedAsync())
{
    using var dialog = new DeviceLinkingDialog(deviceAuthService);
    if (dialog.ShowDialog() != DialogResult.OK)
    {
        return;
    }
}

Application.Run(new MainForm(deviceAuthService, apiClient, logRepository));
