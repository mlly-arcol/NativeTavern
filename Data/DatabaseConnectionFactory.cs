using Microsoft.Data.Sqlite;
using NativeTavern.Helpers;

namespace NativeTavern.Data;

public sealed class DatabaseConnectionFactory
{
    private readonly string _connectionString;

    public DatabaseConnectionFactory() : this(AppPaths.DatabaseFile) { }

    public DatabaseConnectionFactory(string databaseFile)
    {
        // Private cache per Microsoft.Data.Sqlite guidance: shared cache can deadlock
        // (SQLITE_LOCKED) when streaming requests, suggestions and UI hit the database
        // through separate connections at the same time.
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(_connectionString);
}
