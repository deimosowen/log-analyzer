using Microsoft.Extensions.Options;
using Npgsql;

namespace LogAnalyzer.Infrastructure.Postgres;

public sealed class PostgresConnectionFactory : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresConnectionFactory(IOptions<PostgresOptions> options)
    {
        _dataSource = NpgsqlDataSource.Create(BuildConnectionString(options.Value));
    }

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        return await _dataSource.OpenConnectionAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _dataSource.DisposeAsync();
    }

    private static string BuildConnectionString(PostgresOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return options.ConnectionString;
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Database,
            Username = options.Username,
            Password = options.Password,
            Pooling = options.Pooling
        }.ConnectionString;
    }
}
