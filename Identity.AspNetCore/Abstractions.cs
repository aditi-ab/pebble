using System.Security.Claims;

namespace Pebble.Identity;

public interface IAdminIdentityStore
{
    Task<IReadOnlyList<AdminIdentityUser>> ListUsersAsync(CancellationToken cancellationToken);
    Task<AdminIdentityUser?> FindUserAsync(Guid id, CancellationToken cancellationToken);
    Task<AdminIdentityUser?> FindUserByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken);
    Task SaveUserAsync(AdminIdentityUser user, CancellationToken cancellationToken);
    Task DeleteUserAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminIdentityProvider>> ListProvidersAsync(CancellationToken cancellationToken);
    Task<AdminIdentityProvider?> FindProviderAsync(string id, CancellationToken cancellationToken);
    Task SaveProviderAsync(AdminIdentityProvider provider, CancellationToken cancellationToken);
    Task DeleteProviderAsync(string id, CancellationToken cancellationToken);
}

public interface IProductRoleCatalog
{
    IReadOnlyList<string> Roles { get; }
}

public interface IAdminIdentityAuditSink
{
    Task WriteAsync(string action, string target, string outcome, ClaimsPrincipal? actor,
        CancellationToken cancellationToken);
}

public sealed class NullAdminIdentityAuditSink : IAdminIdentityAuditSink
{
    public Task WriteAsync(string action, string target, string outcome, ClaimsPrincipal? actor,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

public interface IAdminIdentityPasswordService
{
    string Hash(AdminIdentityUser user, string password);
    bool Verify(AdminIdentityUser user, string password, out bool requiresRehash);
    string TemporaryPassword();
}

public interface IExternalIdentityService
{
    Task<ExternalAuthenticationResult?> AuthenticatePasswordAsync(AdminIdentityProvider provider, string username,
        string password, CancellationToken cancellationToken);
    Task<string> CreateChallengeAsync(HttpContext context, AdminIdentityProvider provider, string returnUrl,
        CancellationToken cancellationToken);
    Task<(ExternalAuthenticationResult Result, string ReturnUrl)> CompleteChallengeAsync(HttpContext context,
        AdminIdentityProvider provider, CancellationToken cancellationToken);
    Task<(bool Succeeded, string Message)> TestAsync(AdminIdentityProvider provider, string? secret,
        CancellationToken cancellationToken);
}
