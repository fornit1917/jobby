using System.Security.Cryptography;
using System.Text;

namespace Jobby.Dashboard.Authorization;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing for the built-in dashboard credential.
/// </summary>
public static class PasswordHasher
{
    private const string Prefix = "JDPBKDF2";
    private const string Version = "v1";
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Prefix}${Version}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(subkey)}";
    }

    /// <summary>Constant-time verification. Returns false for any malformed/unsupported encoded value.</summary>
    public static bool Verify(string password, string? encoded)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        var parts = encoded.Split('$');
        if (parts is not [Prefix, Version, _, _, _])
        {
            return false;
        }

        if (!int.TryParse(parts[2], out var iterations) || iterations < 1)
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expected.Length == 0)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Validates the stored hash shape at registration so malformed credentials fail at startup.</summary>
    public static bool IsValidEncoding(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        var parts = encoded.Split('$');
        if (parts is not [Prefix, Version, _, _, _])
        {
            return false;
        }

        if (!int.TryParse(parts[2], out var iterations) || iterations < 1)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var hash = Convert.FromBase64String(parts[4]);
            return salt.Length == SaltBytes && hash.Length == HashBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}