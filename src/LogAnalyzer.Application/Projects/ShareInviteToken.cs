using System.Security.Cryptography;

namespace LogAnalyzer.Application.Projects;

public static class ShareInviteToken
{
    /// <summary>
    /// Криптографически случайный идентификатор приглашения (base64url, 43 символа).
    /// </summary>
    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
