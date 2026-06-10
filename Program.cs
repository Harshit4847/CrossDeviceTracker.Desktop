using CrossDeviceTracker.Desktop.Core;
using CrossDeviceTracker.Desktop.Data;

var repository = new SqliteLogRepository();
var tracker = new AppTracker(repository);

Console.CancelKeyPress += async (sender, e) =>
{
    e.Cancel = true;
    await tracker.StopAsync();
    Environment.Exit(0);
};

await tracker.StartAsync();
