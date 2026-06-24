using System.Data.SQLite;

namespace CrossDeviceTracker.Desktop.Core.Helpers;

/// <summary>
/// Reduces boilerplate for SQLite operations by encapsulating the
/// connection-open-command lifecycle.
/// </summary>
public static class SqliteConnectionHelper
{
    private const string ConnectionString = "Data Source=logs.db;Version=3;";

    public static async Task ExecuteAsync(string sql, Action<SQLiteCommand>? configureCommand = null)
    {
        using var connection = new SQLiteConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        configureCommand?.Invoke(command);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<List<T>> QueryAsync<T>(string sql, Func<SQLiteDataReader, T> mapper, Action<SQLiteCommand>? configureCommand = null)
    {
        var results = new List<T>();

        using var connection = new SQLiteConnection(ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        configureCommand?.Invoke(command);

        using var reader = (SQLiteDataReader)await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(mapper(reader));
        }

        return results;
    }
}
