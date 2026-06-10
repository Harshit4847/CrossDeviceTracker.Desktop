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

    public override string ToString()
    {
        return $"{AppName}: {StartTime:yyyy-MM-dd HH:mm:ss} → {EndTime:yyyy-MM-dd HH:mm:ss} ({Duration.TotalSeconds}s)";
    }
}
