using CrossDeviceTracker.Desktop;
using CrossDeviceTracker.Desktop.Data;
using CrossDeviceTracker.Desktop.Services;

Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
Application.ThreadException += (_, e) =>
{
    Console.WriteLine($"Unhandled UI thread exception: {e.Exception}");
    MessageBox.Show(
        $"An unexpected error occurred:\n\n{e.Exception.Message}",
        "CrossDeviceTracker Error",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
};
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    var ex = e.ExceptionObject as Exception;
    Console.WriteLine($"Unhandled exception (terminating={e.IsTerminating}): {ex}");
    MessageBox.Show(
        $"A fatal error occurred:\n\n{ex?.Message ?? "Unknown error"}",
        "CrossDeviceTracker Fatal Error",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
};

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
