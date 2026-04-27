namespace LogAnalyzer.Web.Auth;

public sealed record CurrentUser(
    string Id,
    string Email,
    string DisplayName,
    bool IsAuthenticated);
