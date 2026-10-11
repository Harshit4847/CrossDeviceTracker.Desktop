namespace CrossDeviceTracker.Desktop.Models;

public enum SyncStatus
{
    Pending,
    Sent,
    Failed
}

public class Log
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AppName { get; set; }
    public required DateTime StartTime { get; set; }
    public required DateTime EndTime { get; set; }
    public required TimeSpan Duration { get; set; }
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Client-generated identifier for this usage session, sent to the backend as
    /// <c>clientSessionId</c>. The backend treats (DeviceId, ClientSessionId) as unique, so
    /// re-sending the same session after a failed sync is deduplicated instead of creating a
    /// duplicate row. It is generated once when the session is finalized, persisted with the
    /// log, and reused for every retry — it must never change between sync attempts.
    /// </summary>
    public string ClientSessionId { get; set; } = Guid.NewGuid().ToString();

    public override string ToString()
    {
        return $"{AppName}: {StartTime:yyyy-MM-dd HH:mm:ss} → {EndTime:yyyy-MM-dd HH:mm:ss} ({Duration.TotalSeconds}s)";
    }
}
