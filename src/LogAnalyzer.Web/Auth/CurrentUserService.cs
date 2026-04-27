using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Web.Auth;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly AppAuthenticationOptions _options;

    public CurrentUserService(
        AuthenticationStateProvider authenticationStateProvider,
        IOptions<AppAuthenticationOptions> options)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _options = options.Value;
    }

    public async Task<CurrentUser> GetAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new CurrentUser(
                _options.DevelopmentUser.Id,
                _options.DevelopmentUser.Email,
                _options.DevelopmentUser.DisplayName,
                IsAuthenticated: true);
        }

        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var principal = state.User;
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
