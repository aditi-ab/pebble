using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Pebble.Identity;

public sealed class AdminIdentityService(
    IAdminIdentityStore store,
    IAdminIdentityPasswordService passwords,
    IProductRoleCatalog roleCatalog,
    IAdminIdentityAuditSink audit,
    IOptions<AdminIdentityOptions> options)
{
    public async Task<AdminIdentityUser> BootstrapAsync(string username, string password,
        CancellationToken cancellationToken)
    {
        if ((await store.ListUsersAsync(cancellationToken)).Count != 0)
            throw new IdentityOperationException("users_exist", "An administrator already exists.", 409);
        ValidatePassword(password);
        var user = NewUser(username, null, [options.Value.AdministratorRole]);
        user.PasswordHash = passwords.Hash(user, password);
        await store.SaveUserAsync(user, cancellationToken);
        await audit.WriteAsync("UserBootstrapped", user.Id.ToString(), "Succeeded", null, cancellationToken);
        return user;
    }

    public async Task<AdminIdentityUser> PasswordSignInAsync(string username, string password, string? providerId,
        IExternalIdentityService external, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            var provider = await store.FindProviderAsync(providerId, cancellationToken)
                           ?? throw new IdentityOperationException("provider_not_found", "The identity provider was not found.", 404);
            if (!provider.Enabled || provider.Type != IdentityProviderType.Ldap)
                throw InvalidCredentials();
            var result = await external.AuthenticatePasswordAsync(provider, username, password, cancellationToken)
                         ?? throw InvalidCredentials();
            return await ResolveExternalUserAsync(provider, result, cancellationToken);
        }

        var normalized = NormalizeUsername(username);
        var user = await store.FindUserByUsernameAsync(normalized, cancellationToken);
        if (user is null || !user.Enabled || !passwords.Verify(user, password, out var rehash))
            throw InvalidCredentials();
        if (rehash)
        {
            user.PasswordHash = passwords.Hash(user, password);
            Touch(user, false);
        }
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await store.SaveUserAsync(user, cancellationToken);
        await audit.WriteAsync("LocalSignIn", user.Id.ToString(), "Succeeded", null, cancellationToken);
        return user;
    }

    public async Task<AdminIdentityUser> ResolveExternalUserAsync(AdminIdentityProvider provider,
        ExternalAuthenticationResult result, CancellationToken cancellationToken)
    {
        var users = await store.ListUsersAsync(cancellationToken);
        var user = users.FirstOrDefault(candidate => candidate.ExternalIdentities.Any(identity =>
            identity.ProviderId.Equals(provider.Id, StringComparison.OrdinalIgnoreCase) &&
            identity.Subject.Equals(result.Subject, StringComparison.Ordinal)));
        if (user is null && result.VerifiedUsername)
            user = users.FirstOrDefault(candidate => candidate.NormalizedUsername == NormalizeUsername(result.Username));
        if (user is null)
        {
            if (!provider.AutoProvision)
                throw new IdentityOperationException("external_access_denied", "The external identity is not provisioned.", 403);
            user = NewUser(result.Username, result.DisplayName, []);
        }
        if (!user.Enabled) throw new IdentityOperationException("user_disabled", "The user is disabled.", 403);
        if (!user.ExternalIdentities.Any(identity => identity.ProviderId.Equals(provider.Id, StringComparison.OrdinalIgnoreCase) && identity.Subject == result.Subject))
            user.ExternalIdentities.Add(new AdminExternalIdentity(provider.Id, result.Subject));
        user.DisplayName = string.IsNullOrWhiteSpace(result.DisplayName) ? user.DisplayName : result.DisplayName.Trim();
        user.RoleGrants.RemoveAll(grant => grant.Source == "provider" && grant.SourceId == provider.Id);
        var mapped = provider.RoleMappings.Where(mapping => result.RoleValues.Contains(mapping.Source, StringComparer.OrdinalIgnoreCase)).Select(mapping => mapping.Role);
        foreach (var role in provider.DefaultRoles.Concat(mapped).Distinct(StringComparer.OrdinalIgnoreCase))
            if (roleCatalog.Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
                user.RoleGrants.Add(new AdminRoleGrant(role, "provider", provider.Id));
        if (EffectiveRoles(user).Count == 0)
            throw new IdentityOperationException("external_access_denied", "The external identity has no product role.", 403);
        user.LastLoginAt = DateTimeOffset.UtcNow;
        Touch(user, false);
        await store.SaveUserAsync(user, cancellationToken);
        await audit.WriteAsync("ExternalSignIn", user.Id.ToString(), "Succeeded", null, cancellationToken);
        return user;
    }

    public async Task<(AdminIdentityUser User, string TemporaryPassword)> CreateUserAsync(string username, string? displayName,
        IReadOnlyCollection<string> roles, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        ValidateRoles(roles);
        if (await store.FindUserByUsernameAsync(NormalizeUsername(username), cancellationToken) is not null)
            throw new IdentityOperationException("duplicate_username", "The username already exists.", 409);
        var user = NewUser(username, displayName, roles);
        var temporary = passwords.TemporaryPassword();
        user.PasswordHash = passwords.Hash(user, temporary);
        user.MustChangePassword = true;
        await store.SaveUserAsync(user, cancellationToken);
        await audit.WriteAsync("UserCreated", user.Id.ToString(), "Succeeded", actor, cancellationToken);
        return (user, temporary);
    }

    public async Task<AdminIdentityUser> UpdateUserAsync(Guid id, string? displayName, IReadOnlyCollection<string> roles,
        bool enabled, string expectedVersion, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        ValidateRoles(roles);
        var user = await RequireUserAsync(id, cancellationToken);
        EnsureVersion(user, expectedVersion);
        if (user.Enabled && EffectiveRoles(user).Contains(options.Value.AdministratorRole, StringComparer.OrdinalIgnoreCase) &&
            (!enabled || !roles.Contains(options.Value.AdministratorRole, StringComparer.OrdinalIgnoreCase)))
            await EnsureAnotherAdministratorAsync(id, cancellationToken);
        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        user.Enabled = enabled;
        user.RoleGrants.RemoveAll(grant => grant.Source == "local");
        user.RoleGrants.AddRange(roles.Distinct(StringComparer.OrdinalIgnoreCase).Select(role => new AdminRoleGrant(role, "local")));
        Touch(user, true);
        await store.SaveUserAsync(user, cancellationToken);
        await audit.WriteAsync("UserUpdated", id.ToString(), "Succeeded", actor, cancellationToken);
        return user;
    }

    public async Task DeleteUserAsync(Guid id, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(id, cancellationToken);
        if (user.Enabled && EffectiveRoles(user).Contains(options.Value.AdministratorRole, StringComparer.OrdinalIgnoreCase))
            await EnsureAnotherAdministratorAsync(id, cancellationToken);
        await store.DeleteUserAsync(id, cancellationToken);
        await audit.WriteAsync("UserDeleted", id.ToString(), "Succeeded", actor, cancellationToken);
    }

    public async Task<string> ResetPasswordAsync(Guid id, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(id, cancellationToken);
        var temporary = passwords.TemporaryPassword();
        user.PasswordHash = passwords.Hash(user, temporary);
        user.MustChangePassword = true;
        Touch(user, true);
        await store.SaveUserAsync(user, cancellationToken);
        await audit.WriteAsync("UserPasswordReset", id.ToString(), "Succeeded", actor, cancellationToken);
        return temporary;
    }

    public async Task ChangePasswordAsync(Guid id, string currentPassword, string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(id, cancellationToken);
        if (!passwords.Verify(user, currentPassword, out _)) throw InvalidCredentials();
        ValidatePassword(newPassword);
        user.PasswordHash = passwords.Hash(user, newPassword);
        user.MustChangePassword = false;
        Touch(user, true);
        await store.SaveUserAsync(user, cancellationToken);
        await audit.WriteAsync("UserPasswordChanged", id.ToString(), "Succeeded", null, cancellationToken);
    }

    public IReadOnlyList<string> EffectiveRoles(AdminIdentityUser user) => user.RoleGrants.Select(grant => grant.Role)
        .Where(role => roleCatalog.Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(role => role).ToArray();

    public async Task SignInAsync(HttpContext context, AdminIdentityUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.Username),
            new(options.Value.SecurityStampClaim, user.SecurityStamp),
            new(options.Value.MustChangePasswordClaim, user.MustChangePassword ? "true" : "false")
        };
        claims.AddRange(EffectiveRoles(user).Select(role => new Claim(ClaimTypes.Role, role)));
        await context.SignInAsync(options.Value.CookieScheme, new ClaimsPrincipal(new ClaimsIdentity(claims,
            options.Value.CookieScheme)), new AuthenticationProperties
        {
            IsPersistent = true, AllowRefresh = true, ExpiresUtc = DateTimeOffset.UtcNow.Add(options.Value.SessionLifetime)
        });
    }

    private AdminIdentityUser NewUser(string username, string? displayName, IReadOnlyCollection<string> roles)
    {
        var normalized = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(normalized) || username.Trim().Length > 320)
            throw new IdentityOperationException("validation_error", "Username is required and must be at most 320 characters.");
        return new AdminIdentityUser
        {
            Username = username.Trim(), NormalizedUsername = normalized,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            RoleGrants = roles.Select(role => new AdminRoleGrant(role, "local")).ToList()
        };
    }

    private void ValidateRoles(IReadOnlyCollection<string> roles)
    {
        if (roles.Count == 0 || roles.Any(role => !roleCatalog.Roles.Contains(role, StringComparer.OrdinalIgnoreCase)))
            throw new IdentityOperationException("validation_error", "At least one valid product role is required.");
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 12 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) || !password.Any(character => !char.IsLetterOrDigit(character)))
            throw new IdentityOperationException("validation_error", "Passwords must contain at least 12 characters with upper-case, lower-case, number, and symbol characters.");
    }

    private async Task<AdminIdentityUser> RequireUserAsync(Guid id, CancellationToken cancellationToken) =>
        await store.FindUserAsync(id, cancellationToken) ?? throw new IdentityOperationException("user_not_found", "The user was not found.", 404);
    private async Task EnsureAnotherAdministratorAsync(Guid id, CancellationToken cancellationToken)
    {
        var users = await store.ListUsersAsync(cancellationToken);
        if (!users.Any(user => user.Id != id && user.Enabled &&
            EffectiveRoles(user).Contains(options.Value.AdministratorRole, StringComparer.OrdinalIgnoreCase)))
            throw new IdentityOperationException("last_administrator", "The final enabled administrator cannot be changed.", 409);
    }
    private static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();
    private static void EnsureVersion(AdminIdentityUser user, string expectedVersion)
    {
        if (!user.Version.Equals(expectedVersion, StringComparison.Ordinal))
            throw new IdentityOperationException("concurrency_conflict", "The user was changed by another administrator.", 409);
    }
    private static void Touch(AdminIdentityUser user, bool securitySensitive)
    {
        user.Version = Guid.NewGuid().ToString("D");
        if (securitySensitive) user.SecurityStamp = Guid.NewGuid().ToString("N");
    }
    private static IdentityOperationException InvalidCredentials() => new("invalid_credentials", "The username or password is incorrect.", 401);
}

public sealed class IdentityOperationException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
