using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Infrastructure.ClickHouse;

public sealed class ClickHouseSqlClient
{
    private readonly HttpClient _httpClient;

    public ClickHouseSqlClient(HttpClient httpClient, IOptions<ClickHouseOptions> options)
    {
        var clickHouseOptions = options.Value;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(clickHouseOptions.Endpoint);

        if (!string.IsNullOrWhiteSpace(clickHouseOptions.Username))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clickHouseOptions.Username}:{clickHouseOptions.Password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        Database = ClickHouseSql.Identifier(clickHouseOptions.Database);
    }

    public string Database { get; }

    public string EventTable => $"{Database}.log_events";

    public string MigrationTable => $"{Database}.schema_migrations";

    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        using var content = new StringContent(sql, Encoding.UTF8, "text/plain");
        using var response = await _httpClient.PostAsync(string.Empty, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"ClickHouse request failed: {(int)response.StatusCode} {body}");
        }
    }

    public async Task<IReadOnlyList<JsonElement>> QueryJsonEachRowAsync(
        string sql,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(sql, Encoding.UTF8, "text/plain");
        using var response = await _httpClient.PostAsync(string.Empty, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"ClickHouse query failed: {(int)response.StatusCode} {body}");
        }

        var rows = new List<JsonElement>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using var document = JsonDocument.Parse(line);
            rows.Add(document.RootElement.Clone());
        }

        return rows;
    }
}
