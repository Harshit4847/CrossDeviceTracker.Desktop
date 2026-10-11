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
                        CreatedAt TEXT NOT NULL,
                        ClientSessionId TEXT NOT NULL
                    )";
                await command.ExecuteNonQueryAsync();
            }

            await EnsureClientSessionIdColumnAsync(connection);
        }
    }

    /// <summary>
    /// Migrates databases created before ClientSessionId existed: adds the column and backfills
    /// a unique value for every existing row, so old logs can still be synced. Safe to run on
    /// every startup — the column check is a no-op once the column is present.
    /// </summary>
    private static async Task EnsureClientSessionIdColumnAsync(SQLiteConnection connection)
    {
        var columnExists = false;

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(Logs)";
            using (var reader = await pragma.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (string.Equals(reader["name"].ToString(), nameof(Log.ClientSessionId), StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }
        }

        if (!columnExists)
        {
            using (var alter = connection.CreateCommand())
            {
                alter.CommandText = "ALTER TABLE Logs ADD COLUMN ClientSessionId TEXT NOT NULL DEFAULT ''";
                await alter.ExecuteNonQueryAsync();
            }
        }

        // Backfill blank values (pre-existing rows) with a unique id so every log carries one.
        using (var backfill = connection.CreateCommand())
        {
            backfill.CommandText = @"
                UPDATE Logs
                SET ClientSessionId = lower(hex(randomblob(16)))
                WHERE ClientSessionId IS NULL OR ClientSessionId = ''";
            await backfill.ExecuteNonQueryAsync();
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
                    INSERT INTO Logs (Id, AppName, StartTime, EndTime, DurationSeconds, SyncStatus, CreatedAt, ClientSessionId)
                    VALUES (@id, @appName, @startTime, @endTime, @duration, @syncStatus, @createdAt, @clientSessionId)";

                command.Parameters.AddWithValue("@id", log.Id.ToString());
                command.Parameters.AddWithValue("@appName", log.AppName);
                command.Parameters.AddWithValue("@startTime", log.StartTime.ToString("O"));
                command.Parameters.AddWithValue("@endTime", log.EndTime.ToString("O"));
                command.Parameters.AddWithValue("@duration", log.Duration.TotalSeconds);
                command.Parameters.AddWithValue("@syncStatus", log.SyncStatus.ToString());
                command.Parameters.AddWithValue("@createdAt", log.CreatedAt.ToString("O"));
                command.Parameters.AddWithValue("@clientSessionId", log.ClientSessionId);

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
                        var id = Guid.Parse(reader["Id"].ToString()!);

                        // Fall back to the log id if a row somehow has no session id, so the
                        // value stays stable across sync attempts (idempotency key).
                        var clientSessionId = reader["ClientSessionId"].ToString();
                        if (string.IsNullOrWhiteSpace(clientSessionId))
                        {
                            clientSessionId = id.ToString();
                        }

                        logs.Add(new Log
                        {
                            Id = id,
                            AppName = reader["AppName"].ToString()!,
                            StartTime = DateTime.Parse(
                                reader["StartTime"].ToString()!,
                                null,
                                System.Globalization.DateTimeStyles.RoundtripKind),
                            EndTime = DateTime.Parse(
                                reader["EndTime"].ToString()!,
                                null,
                                System.Globalization.DateTimeStyles.RoundtripKind),
                            Duration = TimeSpan.FromSeconds(double.Parse(reader["DurationSeconds"].ToString()!)),
                            SyncStatus = Enum.Parse<SyncStatus>(reader["SyncStatus"].ToString()!),
                            CreatedAt = DateTime.Parse(
                                reader["CreatedAt"].ToString()!,
                                null,
                                System.Globalization.DateTimeStyles.RoundtripKind),
                            ClientSessionId = clientSessionId!
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
