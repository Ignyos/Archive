using Microsoft.Data.Sqlite;

namespace Archive.Infrastructure.Configuration;

public sealed class SqliteConnectionStringResolver
{
    private readonly string _baseDirectory;

    public SqliteConnectionStringResolver(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    public string Resolve(string? connectionString)
    {
        var candidate = string.IsNullOrWhiteSpace(connectionString)
            ? "Data Source=archive.db"
            : connectionString;

        var builder = new SqliteConnectionStringBuilder(candidate);
        var dataSource = builder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource))
        {
            dataSource = "archive.db";
        }

        if (!Path.IsPathRooted(dataSource))
        {
            Directory.CreateDirectory(_baseDirectory);
            dataSource = Path.Combine(_baseDirectory, dataSource);
        }

        builder.DataSource = dataSource;
        return builder.ToString();
    }
}