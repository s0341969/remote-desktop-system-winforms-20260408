using System.Security.Cryptography;
using System.Text;

namespace RemoteDesktop.Host.Services.Users;

public static class PasswordHasher
{
    public const int SaltSize = 16;
    public const int KeySize = 32;
    public const int DefaultIterations = 100_000;

    public static PasswordHashResult HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return new PasswordHashResult(
            Convert.ToBase64String(hash),
            Convert.ToBase64String(salt),
            DefaultIterations);
    }

    public static bool Verify(string password, string passwordHash, string passwordSalt, int iterations)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(passwordSalt) || iterations <= 0)
        {
            return false;
        }

        var salt = Convert.FromBase64String(passwordSalt);
        var expectedHash = Convert.FromBase64String(passwordHash);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}

public sealed record PasswordHashResult(string Hash, string Salt, int Iterations);
