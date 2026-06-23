using System.Data.SQLite;
using CrossDeviceTracker.Desktop.Models;

namespace CrossDeviceTracker.Desktop.Data;

public class SqliteLogRepository : ILogRepository
{
    private const string DatabasePath = "logs.db";
    private const string ConnectionString = "Data Source=logs.db;Version=3;";

    public async Task InitializeAsync()
    {
        using (var connection = new SQLiteConnection(ConnectionString))
        {
            await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Logs (
                        Id TEXT PRIMARY KEY,
                        AppName TEXT NOT NULL,
                        StartTime TEXT NOT NULL,
                        EndTime TEXT NOT NULL,
                        DurationSeconds REAL NOT NULL,
                        SyncStatus TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL
                    )";
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task SaveLogAsync(Log log)
    {
        using (var connection = new SQLiteConnection(ConnectionString))
        {
            await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    INSERT INTO Logs (Id, AppName, StartTime, EndTime, DurationSeconds, SyncStatus, CreatedAt)
                    VALUES (@id, @appName, @startTime, @endTime, @duration, @syncStatus, @createdAt)";

                command.Parameters.AddWithValue("@id", log.Id.ToString());
                command.Parameters.AddWithValue("@appName", log.AppName);
                command.Parameters.AddWithValue("@startTime", log.StartTime.ToString("O"));
                command.Parameters.AddWithValue("@endTime", log.EndTime.ToString("O"));
                command.Parameters.AddWithValue("@duration", log.Duration.TotalSeconds);
                command.Parameters.AddWithValue("@syncStatus", log.SyncStatus.ToString());
                command.Parameters.AddWithValue("@createdAt", log.CreatedAt.ToString("O"));

                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task<List<Log>> GetPendingLogsAsync()
    {
        var logs = new List<Log>();

        using (var connection = new SQLiteConnection(ConnectionString))
        {
            await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM Logs WHERE SyncStatus IN ('Pending', 'Failed') ORDER BY CreatedAt ASC";
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        logs.Add(new Log
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
                }
            }
        }

        return logs;
    }

    public async Task UpdateSyncStatusAsync(Guid logId, SyncStatus status)
    {
        using (var connection = new SQLiteConnection(ConnectionString))
        {
            await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE Logs SET SyncStatus = @status WHERE Id = @id";
                command.Parameters.AddWithValue("@status", status.ToString());
                command.Parameters.AddWithValue("@id", logId.ToString());

                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task DeleteAllLogsAsync()
    {
        using (var connection = new SQLiteConnection(ConnectionString))
        {
            await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM Logs";
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
