using System.Text.Json;

namespace CrossDeviceTracker.Desktop.Core.Helpers;

/// <summary>
/// Shared JsonSerializerOptions instances to avoid duplicate allocations.
/// </summary>
public static class JsonDefaults
{
    public static JsonSerializerOptions ReadOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static JsonSerializerOptions WriteOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
