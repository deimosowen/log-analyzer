using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Web.Auth;

public static class HttpCurrentUser
{
    public static CurrentUser Get(HttpContext context, IOptions<AppAuthenticationOptions> options)
    {
        if (!options.Value.Enabled)
        {
            return new CurrentUser(
                options.Value.DevelopmentUser.Id,
                options.Value.DevelopmentUser.Email,
                options.Value.DevelopmentUser.DisplayName,
                IsAuthenticated: true);
        }

        var principal = context.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return new CurrentUser(string.Empty, string.Empty, string.Empty, IsAuthenticated: false);
        }

        return new CurrentUser(
            principal.FindFirstValue(AppClaimTypes.UserId) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            principal.FindFirstValue(ClaimTypes.Name) ?? principal.Identity.Name ?? string.Empty,
            IsAuthenticated: true);
    }
}
