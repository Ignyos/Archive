using Archive.Infrastructure.Scheduling;
using Microsoft.Data.Sqlite;

namespace Archive.Core.Tests;

public class QuartzSchemaInitializerTests
{
    [Fact]
    public void EnsureCreated_CreatesRequiredQuartzTables()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"archive-quartz-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            QuartzSchemaInitializer.EnsureCreated(connectionString);

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            Assert.True(TableExists(connection, "QRTZ_JOB_DETAILS"));
            Assert.True(TableExists(connection, "QRTZ_TRIGGERS"));
            Assert.True(TableExists(connection, "QRTZ_LOCKS"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
            {
                try
                {
                    File.Delete(databasePath);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);

        var result = command.ExecuteScalar();
        return Convert.ToInt32(result) > 0;
    }
}