using System.Security.Cryptography;
using System.Text;
using Pebble.Identity;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Pebble.Identity.AspNetCore.Tests;

public sealed class PasswordServiceTests
{
    private readonly AdminIdentityPasswordService _service = new(new PasswordHasher<AdminIdentityUser>());

    [Fact]
    public void AspNetIdentityHashRoundTrips()
    {
        var user = new AdminIdentityUser();
        user.PasswordHash = _service.Hash(user, "Correct-Horse-42!");

        Assert.True(_service.Verify(user, "Correct-Horse-42!", out var rehash));
        Assert.False(rehash);
        Assert.False(_service.Verify(user, "wrong", out _));
    }

    [Fact]
    public void QuickProxyPbkdf2HashIsAcceptedAndMarkedForUpgrade()
    {
        const string password = "Legacy-Password-42!";
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100_000,
            HashAlgorithmName.SHA256, 32);
        var user = new AdminIdentityUser
        {
            PasswordHash = $"pbkdf2-sha256$100000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}"
        };

        Assert.True(_service.Verify(user, password, out var rehash));
        Assert.True(rehash);
    }

    [Fact]
    public void TemporaryPasswordMeetsSharedPolicyShape()
    {
        var password = _service.TemporaryPassword();

        Assert.True(password.Length >= 12);
        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, character => !char.IsLetterOrDigit(character));
    }
}
