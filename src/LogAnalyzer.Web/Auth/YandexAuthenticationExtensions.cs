using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using LogAnalyzer.Application;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Web.Auth;

public static class YandexAuthenticationExtensions
{
    private const string YandexScheme = "Yandex";
    private const string YandexAuthorizationEndpoint = "https://oauth.yandex.ru/authorize";
    private const string YandexTokenEndpoint = "https://oauth.yandex.ru/token";
    private const string YandexUserInformationEndpoint = "https://login.yandex.ru/info?format=json";

    public static IServiceCollection AddLogAnalyzerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AppAuthenticationOptions>(configuration.GetSection("Authentication"));
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        var authOptions = configuration.GetSection("Authentication").Get<AppAuthenticationOptions>() ?? new();
        if (!authOptions.Enabled)
        {
            return services;
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = YandexScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.AccessDeniedPath = "/access-denied";
            })
            .AddOAuth(YandexScheme, options =>
            {
                options.ClientId = authOptions.Yandex.ClientId;
                options.ClientSecret = authOptions.Yandex.ClientSecret;
                options.CallbackPath = authOptions.Yandex.CallbackPath;
                options.AuthorizationEndpoint = YandexAuthorizationEndpoint;
                options.TokenEndpoint = YandexTokenEndpoint;
                options.UserInformationEndpoint = YandexUserInformationEndpoint;
                options.Scope.Add("login:email");
                options.Scope.Add("login:info");
                options.SaveTokens = false;
                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = CreateYandexTicketAsync
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static WebApplication UseLogAnalyzerAuthentication(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<AppAuthenticationOptions>>().Value;
        if (!options.Enabled)
        {
            return app;
        }

        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    private static async Task CreateYandexTicketAsync(OAuthCreatingTicketContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", context.AccessToken);

        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted);
        using var payload = await JsonDocument.ParseAsync(stream, cancellationToken: context.HttpContext.RequestAborted);
        var root = payload.RootElement;

        var providerUserId = ReadRequiredString(root, "id");
        var email = ReadEmail(root);
        if (string.IsNullOrWhiteSpace(email))
        {
            context.Fail("Yandex profile does not contain an email address.");
            return;
        }

        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<AppAuthenticationOptions>>().Value;
        if (!IsAllowedEmailDomain(email, options.AllowedEmailDomains))
        {
            context.Fail("Email domain is not allowed.");
            return;
        }

        var displayName = ReadFirstString(root, "real_name", "display_name", "login") ?? email;
        var metadata = context.HttpContext.RequestServices.GetRequiredService<IMetadataRepository>();
        var user = await metadata.UpsertUserAsync(
            new UserProfile(YandexScheme.ToLowerInvariant(), providerUserId, email, displayName),
            context.HttpContext.RequestAborted);

        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        identity.AddClaim(new Claim(AppClaimTypes.UserId, user.Id));
        identity.AddClaim(new Claim(AppClaimTypes.Provider, user.Provider));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));
    }

    private static string ReadRequiredString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) ? value.ToString() : string.Empty;
    }

    private static string? ReadFirstString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && !string.IsNullOrWhiteSpace(value.ToString()))
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static string ReadEmail(JsonElement root)
    {
        var defaultEmail = ReadFirstString(root, "default_email");
        if (!string.IsNullOrWhiteSpace(defaultEmail))
        {
            return defaultEmail;
        }

        if (root.TryGetProperty("emails", out var emails) && emails.ValueKind == JsonValueKind.Array)
        {
            foreach (var email in emails.EnumerateArray())
            {
                if (!string.IsNullOrWhiteSpace(email.ToString()))
                {
                    return email.ToString();
                }
            }
        }

        return string.Empty;
    }

    private static bool IsAllowedEmailDomain(string email, IReadOnlyCollection<string> allowedDomains)
    {
        if (allowedDomains.Count == 0)
        {
            return true;
        }

        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1)
        {
            return false;
        }

        var domain = email[(atIndex + 1)..];
        return allowedDomains.Any(allowed => string.Equals(allowed.Trim(), domain, StringComparison.OrdinalIgnoreCase));
    }
}
