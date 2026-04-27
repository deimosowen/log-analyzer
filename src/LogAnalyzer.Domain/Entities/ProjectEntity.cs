namespace LogAnalyzer.Domain;

public sealed record ProjectEntity(
    string Id,
    string OwnerUserId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
