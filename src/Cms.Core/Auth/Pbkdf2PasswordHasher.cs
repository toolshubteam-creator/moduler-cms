namespace Cms.Core.Auth;

using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 100_000;
    private const KeyDerivationPrf Prf = KeyDerivationPrf.HMACSHA256;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = KeyDerivation.Pbkdf2(password, salt, Prf, Iterations, HashBytes);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(encodedHash))
        {
            return false;
        }

        var parts = encodedHash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = KeyDerivation.Pbkdf2(password, salt, Prf, iterations, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
