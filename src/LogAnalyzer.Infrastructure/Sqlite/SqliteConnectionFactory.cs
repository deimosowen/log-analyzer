using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Infrastructure.Sqlite;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Абсолютный путь к файлу SQLite (метаданные и события в одном файле при стандартной конфигурации).
    /// </summary>
    public string DatabasePath { get; }

    public SqliteConnectionFactory(IOptions<SqliteOptions> options, IHostEnvironment hostEnvironment)
    {
        var configured = options.Value.DatabasePath.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Sqlite:DatabasePath is not configured.");
        }

        DatabasePath = Path.IsPathFullyQualified(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, configured));
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
