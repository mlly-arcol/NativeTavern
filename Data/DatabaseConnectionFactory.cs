using Microsoft.Data.Sqlite;
using NativeTavern.Helpers;

namespace NativeTavern.Data;

public sealed class DatabaseConnectionFactory
{
    private readonly string _connectionString;

    public DatabaseConnectionFactory() : this(AppPaths.DatabaseFile) { }

    public DatabaseConnectionFactory(string databaseFile)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(_connectionString);
}
