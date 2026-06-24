using System.Data.SQLite;
using CrossDeviceTracker.Desktop.Core.Helpers;
using CrossDeviceTracker.Desktop.Models;

namespace CrossDeviceTracker.Desktop.Data;

public class SqliteLogRepository : ILogRepository
{
    public async Task InitializeAsync()
    {
        await SqliteConnectionHelper.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Logs (
                Id TEXT PRIMARY KEY,
                AppName TEXT NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT NOT NULL,
                DurationSeconds REAL NOT NULL,
                SyncStatus TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            )");
    }

    public async Task SaveLogAsync(Log log)
    {
        await SqliteConnectionHelper.ExecuteAsync(
            @"INSERT INTO Logs (Id, AppName, StartTime, EndTime, DurationSeconds, SyncStatus, CreatedAt)
              VALUES (@id, @appName, @startTime, @endTime, @duration, @syncStatus, @createdAt)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", log.Id.ToString());
                cmd.Parameters.AddWithValue("@appName", log.AppName);
                cmd.Parameters.AddWithValue("@startTime", log.StartTime.ToString("O"));
                cmd.Parameters.AddWithValue("@endTime", log.EndTime.ToString("O"));
                cmd.Parameters.AddWithValue("@duration", log.Duration.TotalSeconds);
                cmd.Parameters.AddWithValue("@syncStatus", log.SyncStatus.ToString());
                cmd.Parameters.AddWithValue("@createdAt", log.CreatedAt.ToString("O"));
            });
    }

    public async Task<List<Log>> GetPendingLogsAsync()
    {
        return await SqliteConnectionHelper.QueryAsync(
            "SELECT * FROM Logs WHERE SyncStatus IN ('Pending', 'Failed') ORDER BY CreatedAt ASC",
            reader => new Log
            {
                Id = Guid.Parse(reader["Id"].ToString()!),
                AppName = reader["AppName"].ToString()!,
                StartTime = DateTime.Parse(reader["StartTime"].ToString()!),
                EndTime = DateTime.Parse(reader["EndTime"].ToString()!),
                Duration = TimeSpan.FromSeconds(double.Parse(reader["DurationSeconds"].ToString()!)),
                SyncStatus = Enum.Parse<SyncStatus>(reader["SyncStatus"].ToString()!),
                CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString()!)
            });
    }

    public async Task UpdateSyncStatusAsync(Guid logId, SyncStatus status)
    {
        await SqliteConnectionHelper.ExecuteAsync(
            "UPDATE Logs SET SyncStatus = @status WHERE Id = @id",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@status", status.ToString());
                cmd.Parameters.AddWithValue("@id", logId.ToString());
            });
    }

    public async Task DeleteAllLogsAsync()
    {
        await SqliteConnectionHelper.ExecuteAsync("DELETE FROM Logs");
    }
}
