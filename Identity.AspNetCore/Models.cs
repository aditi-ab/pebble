namespace Pebble.Identity;

public enum IdentityProviderType
{
    Ldap,
    Oidc,
    Entra
}

public sealed class AdminIdentityUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool Enabled { get; set; } = true;
    public string PasswordHash { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public string Version { get; set; } = Guid.NewGuid().ToString("D");
    public List<AdminExternalIdentity> ExternalIdentities { get; set; } = [];
    public List<AdminRoleGrant> RoleGrants { get; set; } = [];
}

public sealed record AdminExternalIdentity(string ProviderId, string Subject);
public sealed record AdminRoleGrant(string Role, string Source, string? SourceId = null);
public sealed record AdminRoleMapping(string Source, string Role);

public sealed class AdminIdentityProvider
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IdentityProviderType Type { get; set; }
    public bool Enabled { get; set; } = true;
    public bool AutoProvision { get; set; }
    public List<string> DefaultRoles { get; set; } = [];
    public List<AdminRoleMapping> RoleMappings { get; set; } = [];
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string ProtectedSecret { get; set; } = string.Empty;
    public string Version { get; set; } = Guid.NewGuid().ToString("D");
}

public sealed record ExternalAuthenticationResult(
    string Subject,
    string Username,
    string? DisplayName,
    bool VerifiedUsername,
    IReadOnlyCollection<string> RoleValues);

public sealed class AdminIdentityOptions
{
    public string BasePath { get; set; } = "/admin";
    public AdminIdentityEndpointOptions Endpoints { get; } = new();
    public string CookieScheme { get; set; } = "Pebble.Identity";
    public string CookieName { get; set; } = "__Host-Pebble.Identity";
    public string AdministratorPolicy { get; set; } = "Administrator";
    public string AdministratorRole { get; set; } = "Administrator";
    public string AntiforgeryHeader { get; set; } = "X-CSRF-TOKEN";
    public string SecurityStampClaim { get; set; } = "pebble.security_stamp";
    public string MustChangePasswordClaim { get; set; } = "pebble.must_change_password";
    public bool RegisterCookieScheme { get; set; } = true;
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(8);

    internal string AuthenticationBasePath => CombinePath(BasePath, Endpoints.Authentication);
    internal string ManagementBasePath => CombinePath(BasePath, Endpoints.Management);

    internal string ExternalCallbackPath(string providerId) =>
        CombinePath(AuthenticationBasePath, Endpoints.ExternalCallback)
            .Replace("{providerId}", Uri.EscapeDataString(providerId), StringComparison.Ordinal);

    private static string CombinePath(string left, string right) =>
        $"/{left.Trim('/')}/{right.Trim('/')}".Replace("//", "/", StringComparison.Ordinal);
}

public sealed class AdminIdentityEndpointOptions
{
    public string Authentication { get; set; } = "/auth";
    public string Management { get; set; } = "/identity";
    public string Status { get; set; } = "/status";
    public string Bootstrap { get; set; } = "/bootstrap";
    public string Login { get; set; } = "/login";
    public string Logout { get; set; } = "/logout";
    public string ChangePassword { get; set; } = "/change-password";
    public string ExternalStart { get; set; } = "/external/{providerId}/start";
    public string ExternalCallback { get; set; } = "/external/{providerId}/callback";
    public string Roles { get; set; } = "/roles";
    public string Users { get; set; } = "/users";
    public string User { get; set; } = "/users/{id:guid}";
    public string ResetPassword { get; set; } = "/users/{id:guid}/reset-password";
    public string Providers { get; set; } = "/providers";
    public string Provider { get; set; } = "/providers/{id}";
    public string TestProvider { get; set; } = "/providers/test";
}
