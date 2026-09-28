using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Pebble.Identity;

public sealed class AdminIdentityPasswordService(IPasswordHasher<AdminIdentityUser> hasher)
    : IAdminIdentityPasswordService
{
    public string Hash(AdminIdentityUser user, string password) => hasher.HashPassword(user, password);

    public bool Verify(AdminIdentityUser user, string password, out bool requiresRehash)
    {
        requiresRehash = false;
        if (user.PasswordHash.StartsWith("pbkdf2-sha256$", StringComparison.Ordinal))
        {
            requiresRehash = VerifyQuickProxyHash(user.PasswordHash, password);
            return requiresRehash;
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        requiresRehash = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result != PasswordVerificationResult.Failed;
    }

    public string TemporaryPassword()
    {
        return $"{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}!aA1";
    }

    private static bool VerifyQuickProxyHash(string passwordHash, string password)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations <= 0) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations,
                HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
