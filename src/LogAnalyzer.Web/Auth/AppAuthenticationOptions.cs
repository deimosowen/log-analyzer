namespace LogAnalyzer.Web.Auth;

public sealed class AppAuthenticationOptions
{
    public bool Enabled { get; set; }
    public string? PublicOrigin { get; set; }
    public string[] AllowedEmailDomains { get; set; } = [];
    public DevelopmentUserOptions DevelopmentUser { get; set; } = new();
    public YandexOAuthOptions Yandex { get; set; } = new();
}
