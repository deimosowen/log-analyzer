using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Web.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapLogAnalyzerAuth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/login", (HttpContext context, IOptions<AppAuthenticationOptions> options) =>
        {
            if (!options.Value.Enabled)
            {
                return Results.Redirect("/");
            }

            var properties = new AuthenticationProperties { RedirectUri = "/" };
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
}
