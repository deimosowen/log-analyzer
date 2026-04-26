namespace LogAnalyzer.Domain;

public sealed record ProjectEntity(
    string Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
