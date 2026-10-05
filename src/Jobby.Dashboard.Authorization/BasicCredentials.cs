using System.Security.Cryptography;
using System.Text;

namespace Jobby.Dashboard.Authorization;

/// <summary>
/// Validates a submitted (username, password) against the configured single credential.
/// Both checks are always evaluated (no short-circuit).
/// - the username is compared via fixed-time equality of SHA-256 digests (length-independent)
/// - the password via PBKDF2 verification.
/// </summary>
public static class BasicCredentials
{
    public static bool Validate(
        string? username,
        string? password,
        string expectedUsername,
        string expectedPasswordHash)
    {
        var userOk = FixedTimeEqualsHashed(username ?? string.Empty, expectedUsername);
        var passOk = PasswordHasher.Verify(password ?? string.Empty, expectedPasswordHash);
        return userOk & passOk;
    }

    private static bool FixedTimeEqualsHashed(string a, string b)
    {
        Span<byte> ha = stackalloc byte[32];
        Span<byte> hb = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(a), ha);
        SHA256.HashData(Encoding.UTF8.GetBytes(b), hb);
        return CryptographicOperations.FixedTimeEquals(ha, hb);
    }
}