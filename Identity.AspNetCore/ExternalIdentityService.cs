using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace Pebble.Identity;

public sealed class ExternalIdentityService(
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<AdminIdentityOptions> options) : IExternalIdentityService
{
    private readonly IDataProtector stateProtector = dataProtectionProvider.CreateProtector("Pebble.Identity.OidcState.v1");

    public Task<ExternalAuthenticationResult?> AuthenticatePasswordAsync(AdminIdentityProvider provider,
        string username, string password, CancellationToken cancellationToken)
    {
        if (provider.Type != IdentityProviderType.Ldap) return Task.FromResult<ExternalAuthenticationResult?>(null);
        return Task.Run(() => AuthenticateLdap(provider, username, password), cancellationToken);
    }

    public async Task<string> CreateChallengeAsync(HttpContext context, AdminIdentityProvider provider,
        string returnUrl, CancellationToken cancellationToken)
    {
        if (provider.Type == IdentityProviderType.Ldap) throw new InvalidOperationException("LDAP uses password sign-in.");
        var metadata = await GetMetadataAsync(provider, cancellationToken);
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        var expires = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var state = stateProtector.Protect(JsonSerializer.Serialize(new OidcState(provider.Id, returnUrl, verifier, nonce, expires)));
        var redirectUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{options.Value.ExternalCallbackPath(provider.Id)}";
        return QueryHelpers.AddQueryString(metadata.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = Setting(provider, "clientId"), ["response_type"] = "code",
            ["redirect_uri"] = redirectUri, ["scope"] = Setting(provider, "scopes", "openid profile email"),
            ["state"] = state, ["nonce"] = nonce, ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        });
    }

    public async Task<(ExternalAuthenticationResult Result, string ReturnUrl)> CompleteChallengeAsync(
        HttpContext context, AdminIdentityProvider provider, CancellationToken cancellationToken)
    {
        var code = context.Request.Query["code"].ToString();
        var protectedState = context.Request.Query["state"].ToString();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(protectedState))
            throw new InvalidOperationException("The identity-provider callback is incomplete.");
        var state = JsonSerializer.Deserialize<OidcState>(stateProtector.Unprotect(protectedState))
                    ?? throw new InvalidOperationException("The identity-provider state is invalid.");
        if (state.ProviderId != provider.Id || state.ExpiresAt < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            throw new InvalidOperationException("The identity-provider state has expired.");

        var metadata = await GetMetadataAsync(provider, cancellationToken);
        var redirectUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{options.Value.ExternalCallbackPath(provider.Id)}";
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirectUri,
            ["client_id"] = Setting(provider, "clientId"), ["code_verifier"] = state.Verifier
        };
        var secret = UnprotectSecret(provider);
        if (!string.IsNullOrWhiteSpace(secret)) form["client_secret"] = secret;
        using var response = await httpClientFactory.CreateClient(nameof(ExternalIdentityService))
            .PostAsync(metadata.TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        response.EnsureSuccessStatusCode();
        using var tokenPayload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var idToken = tokenPayload.RootElement.GetProperty("id_token").GetString()
                      ?? throw new InvalidOperationException("The identity provider did not return an ID token.");
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = metadata.Issuer, ValidateAudience = true,
            ValidAudience = Setting(provider, "clientId"), ValidateLifetime = true, ValidateIssuerSigningKey = true,
            IssuerSigningKeys = metadata.SigningKeys
        });
        if (!validation.IsValid) throw new InvalidOperationException("The identity provider returned an invalid ID token.", validation.Exception);
        var claims = validation.ClaimsIdentity.Claims.ToLookup(claim => claim.Type, claim => claim.Value);
        if (claims["nonce"].FirstOrDefault() != state.Nonce) throw new InvalidOperationException("The identity-provider nonce is invalid.");
        var subject = claims[Setting(provider, "subjectClaim", "sub")].FirstOrDefault() ?? string.Empty;
        var username = claims[Setting(provider, "emailClaim", provider.Type == IdentityProviderType.Entra ? "preferred_username" : "email")].FirstOrDefault() ?? string.Empty;
        var name = claims[Setting(provider, "nameClaim", "name")].FirstOrDefault();
        var roles = claims[Setting(provider, "roleClaim", "roles")].ToArray();
        var verified = provider.Type == IdentityProviderType.Entra || claims["email_verified"].Contains("true", StringComparer.OrdinalIgnoreCase);
        return (new ExternalAuthenticationResult(subject, username, name, verified, roles), SafeReturnUrl(state.ReturnUrl));
    }

    public async Task<(bool Succeeded, string Message)> TestAsync(AdminIdentityProvider provider, string? secret,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(secret)) provider.ProtectedSecret = ProtectSecret(secret);
            if (provider.Type == IdentityProviderType.Ldap)
            {
                using var connection = CreateLdapConnection(provider);
                BindServiceAccount(connection, provider);
            }
            else
            {
                await GetMetadataAsync(provider, cancellationToken);
            }
            return (true, "Connection succeeded.");
        }
        catch (Exception exception)
        {
            return (false, exception.Message);
        }
    }

    public string ProtectSecret(string secret) => dataProtectionProvider.CreateProtector("Pebble.Identity.ProviderSecret.v1").Protect(secret);
    private string UnprotectSecret(AdminIdentityProvider provider) => string.IsNullOrEmpty(provider.ProtectedSecret) ? string.Empty : dataProtectionProvider.CreateProtector("Pebble.Identity.ProviderSecret.v1").Unprotect(provider.ProtectedSecret);

    private ExternalAuthenticationResult? AuthenticateLdap(AdminIdentityProvider provider, string username,
        string password)
    {
        using var connection = CreateLdapConnection(provider);
        BindServiceAccount(connection, provider);
        var filter = Setting(provider, "userFilter", "(mail={username})").Replace("{username}", EscapeLdap(username), StringComparison.Ordinal);
        var attributes = new[] { Setting(provider, "emailAttribute", "mail"), Setting(provider, "nameAttribute", "displayName"), Setting(provider, "groupAttribute", "memberOf") };
        var response = (SearchResponse)connection.SendRequest(new SearchRequest(Setting(provider, "baseDn"), filter, SearchScope.Subtree, attributes));
        if (response.Entries.Count != 1) return null;
        var entry = response.Entries[0];
        using var userConnection = CreateLdapConnection(provider);
        userConnection.Bind(new NetworkCredential(entry.DistinguishedName, password));
        var resolvedUsername = Attribute(entry, attributes[0]) ?? username;
        var displayName = Attribute(entry, attributes[1]);
        var groups = AttributeValues(entry, attributes[2]);
        return new ExternalAuthenticationResult(entry.DistinguishedName, resolvedUsername, displayName, true, groups);
    }

    private LdapConnection CreateLdapConnection(AdminIdentityProvider provider)
    {
        var connection = new LdapConnection(new LdapDirectoryIdentifier(Setting(provider, "server"), int.Parse(Setting(provider, "port", "389"))));
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = bool.TryParse(Setting(provider, "useSsl"), out var ssl) && ssl;
        connection.AuthType = AuthType.Basic;
        return connection;
    }

    private void BindServiceAccount(LdapConnection connection, AdminIdentityProvider provider)
    {
        var bindDn = Setting(provider, "bindDn");
        connection.Bind(string.IsNullOrWhiteSpace(bindDn) ? null : new NetworkCredential(bindDn, UnprotectSecret(provider)));
    }

    private async Task<OidcMetadata> GetMetadataAsync(AdminIdentityProvider provider, CancellationToken cancellationToken)
    {
        var url = Setting(provider, "metadataUrl");
        if (string.IsNullOrWhiteSpace(url)) url = $"{Setting(provider, "authority").TrimEnd('/')}/.well-known/openid-configuration";
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(url,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(httpClientFactory.CreateClient(nameof(ExternalIdentityService))) { RequireHttps = true });
        var configuration = await manager.GetConfigurationAsync(cancellationToken);
        return new OidcMetadata(configuration.AuthorizationEndpoint, configuration.TokenEndpoint,
            configuration.Issuer, configuration.SigningKeys);
    }

    private static string Setting(AdminIdentityProvider provider, string name, string fallback = "") => provider.Settings.TryGetValue(name, out var value) ? value : fallback;
    private static string? Attribute(SearchResultEntry entry, string name) => entry.Attributes[name]?.Count > 0 ? entry.Attributes[name][0]?.ToString() : null;
    private static IReadOnlyCollection<string> AttributeValues(SearchResultEntry entry, string name) => entry.Attributes[name]?.GetValues(typeof(string)).Cast<string>().ToArray() ?? [];
    private static string EscapeLdap(string value) => value.Replace("\\", "\\5c", StringComparison.Ordinal).Replace("*", "\\2a", StringComparison.Ordinal).Replace("(", "\\28", StringComparison.Ordinal).Replace(")", "\\29", StringComparison.Ordinal).Replace("\0", "\\00", StringComparison.Ordinal);
    private static string SafeReturnUrl(string value) => Uri.TryCreate(value, UriKind.Relative, out _) && value.StartsWith('/') && !value.StartsWith("//") ? value : "/admin/";
    private sealed record OidcState(string ProviderId, string ReturnUrl, string Verifier, string Nonce, long ExpiresAt);
    private sealed record OidcMetadata(string AuthorizationEndpoint, string TokenEndpoint, string Issuer,
        ICollection<SecurityKey> SigningKeys);
}
