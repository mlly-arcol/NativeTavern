using Microsoft.Data.Sqlite;
using NativeTavern.Helpers;

namespace NativeTavern.Data;

public sealed class DatabaseConnectionFactory
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = AppPaths.DatabaseFile,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        ForeignKeys = true
    }.ToString();

    public SqliteConnection CreateConnection() => new(_connectionString);
}
