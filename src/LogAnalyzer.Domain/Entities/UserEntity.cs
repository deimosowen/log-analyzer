namespace LogAnalyzer.Domain;

public sealed record UserEntity(
    string Id,
    string Provider,
    string ProviderUserId,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastLoginAt);
