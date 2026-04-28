using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Web.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapLogAnalyzerAuth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/auth/yandex", (HttpContext context, IOptions<AppAuthenticationOptions> options) =>
        {
            if (!options.Value.Enabled)
            {
                return Results.Redirect(ReadSafeReturnUrl(context) ?? "/");
            }

            var properties = new AuthenticationProperties { RedirectUri = ReadSafeReturnUrl(context) ?? "/" };
            return Results.Challenge(properties, ["Yandex"]);
        });

        endpoints.MapGet("/logout", async (HttpContext context, IOptions<AppAuthenticationOptions> options) =>
        {
            if (options.Value.Enabled)
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            return Results.Redirect("/");
        });

        endpoints.MapGet("/access-denied", () => Results.Content(
            "Доступ запрещен: домен почты не входит в список разрешенных.",
            "text/plain; charset=utf-8"));

        return endpoints;
    }

    private static string? ReadSafeReturnUrl(HttpContext context)
    {
        var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault()
            ?? context.Request.Query["ReturnUrl"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        return Uri.TryCreate(returnUrl, UriKind.Relative, out _) && returnUrl.StartsWith("/", StringComparison.Ordinal)
            ? returnUrl
            : null;
    }
}
