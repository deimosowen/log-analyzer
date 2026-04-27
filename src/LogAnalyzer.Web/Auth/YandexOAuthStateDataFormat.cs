using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace LogAnalyzer.Web.Auth;

public sealed class YandexOAuthStateDataFormat : ISecureDataFormat<AuthenticationProperties>
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private const string StatePrefix = "ya_";
    private const int StateKeyBytes = 32;

    private readonly IMemoryCache _cache;

    public YandexOAuthStateDataFormat(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Protect(AuthenticationProperties data)
    {
        return Protect(data, purpose: null);
    }

    public string Protect(AuthenticationProperties data, string? purpose)
    {
        var state = StatePrefix + Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Encode(
            RandomNumberGenerator.GetBytes(StateKeyBytes));
        _cache.Set(
            state,
            data,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = StateLifetime
            });

        return state;
    }

    public AuthenticationProperties? Unprotect(string? protectedText)
    {
        return Unprotect(protectedText, purpose: null);
    }

    public AuthenticationProperties? Unprotect(string? protectedText, string? purpose)
    {
        if (string.IsNullOrWhiteSpace(protectedText) ||
            !protectedText.StartsWith(StatePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        if (!_cache.TryGetValue(protectedText, out AuthenticationProperties? properties))
        {
            return null;
        }

        _cache.Remove(protectedText);
        return properties;
    }
}
