using System.Security.Cryptography;

namespace OpenPortalKit.Modules.Identity.Authentication;

public sealed class PasswordCredentialHasher
{
    public const int DefaultIterations = 210_000;
    private const int SaltSize = 32;
    private const int HashSize = 32;
    private const string Algorithm = "pbkdf2-sha512";

    public string Hash(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (iterations < 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "PBKDF2 iterations must be at least 100000.");
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, HashSize);
        return string.Join('$', Algorithm, iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(encodedHash))
        {
            return false;
        }

        var parts = encodedHash.Split('$', StringSplitOptions.None);
        if (parts.Length != 4 || !string.Equals(parts[0], Algorithm, StringComparison.Ordinal) ||
            !int.TryParse(parts[1], out var iterations) || iterations < 100_000)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltSize || expected.Length != HashSize)
            {
                return false;
            }
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
