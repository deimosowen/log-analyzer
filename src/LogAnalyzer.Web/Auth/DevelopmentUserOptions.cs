namespace LogAnalyzer.Web.Auth;

public sealed class DevelopmentUserOptions
{
    public string Id { get; set; } = "local-dev";
    public string Email { get; set; } = "local@dev.local";
    public string DisplayName { get; set; } = "Локальный пользователь";
}
