using System.Text;
using System.Text.Json;

namespace CrossDeviceTracker.Desktop.Core.Helpers;

/// <summary>
/// Shared HTTP utilities for JSON POST requests and error response parsing.
/// </summary>
public static class HttpHelper
{
    public static StringContent CreateJsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    public static async Task<string> ExtractErrorMessageAsync(HttpResponseMessage response, string fallbackMessage = "Request failed.")
    {
        var errorContent = await response.Content.ReadAsStringAsync();

        try
        {
            using var doc = JsonDocument.Parse(errorContent);
            if (doc.RootElement.TryGetProperty("message", out var msgProp))
            {
                return msgProp.GetString() ?? fallbackMessage;
            }
            if (doc.RootElement.TryGetProperty("error", out var errProp))
            {
                return errProp.GetString() ?? fallbackMessage;
            }
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(errorContent))
            {
                return errorContent;
            }
        }

        return fallbackMessage;
    }
}
