namespace LogAnalyzer.Application;

public sealed record UserProfile(
    string Provider,
    string ProviderUserId,
    string Email,
    string DisplayName);
