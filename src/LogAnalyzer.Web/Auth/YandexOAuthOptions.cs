namespace LogAnalyzer.Web.Auth;

public sealed class YandexOAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/signin-yandex";
}
