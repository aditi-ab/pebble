using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Pebble.Identity;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapPebbleIdentity(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<AdminIdentityOptions>>().Value;
        var auth = endpoints.MapGroup(options.AuthenticationBasePath);
        auth.MapGet(options.Endpoints.Status, StatusAsync).AllowAnonymous();
        auth.MapPost(options.Endpoints.Bootstrap, BootstrapAsync).AllowAnonymous();
        auth.MapPost(options.Endpoints.Login, LoginAsync).AllowAnonymous();
        auth.MapPost(options.Endpoints.Logout, LogoutAsync).RequireAuthorization();
        auth.MapPost(options.Endpoints.ChangePassword, ChangePasswordAsync).RequireAuthorization();
        MapExternalAuthentication(auth, options.Endpoints);

        return endpoints.MapPebbleIdentityManagement();
    }

    public static IEndpointRouteBuilder MapPebbleIdentityExternalAuthentication(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<AdminIdentityOptions>>().Value;
        MapExternalAuthentication(endpoints.MapGroup(options.AuthenticationBasePath), options.Endpoints);
        return endpoints;
    }

    private static void MapExternalAuthentication(RouteGroupBuilder auth, AdminIdentityEndpointOptions paths)
    {
        auth.MapPost(paths.ExternalStart, StartExternalAsync).AllowAnonymous();
        auth.MapGet(paths.ExternalCallback, CompleteExternalAsync).AllowAnonymous();
    }

    public static IEndpointRouteBuilder MapPebbleIdentityManagement(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<AdminIdentityOptions>>().Value;
        var identity = endpoints.MapGroup(options.ManagementBasePath)
            .RequireAuthorization(options.AdministratorPolicy);
        identity.MapGet(options.Endpoints.Roles, (IProductRoleCatalog catalog) => Results.Ok(catalog.Roles));
        identity.MapGet(options.Endpoints.Users, ListUsersAsync);
        identity.MapPost(options.Endpoints.Users, CreateUserAsync);
        identity.MapPut(options.Endpoints.User, UpdateUserAsync);
        identity.MapPost(options.Endpoints.ResetPassword, ResetPasswordAsync);
        identity.MapDelete(options.Endpoints.User, DeleteUserAsync);
        identity.MapGet(options.Endpoints.Providers, ListProvidersAsync);
        identity.MapPut(options.Endpoints.Provider, SaveProviderAsync);
        identity.MapDelete(options.Endpoints.Provider, DeleteProviderAsync);
        identity.MapPost(options.Endpoints.TestProvider, TestProviderAsync);
        return endpoints;
    }

    private static async Task<IResult> StatusAsync(HttpContext context, IAdminIdentityStore store,
        IAntiforgery antiforgery, CancellationToken cancellationToken)
    {
        var users = await store.ListUsersAsync(cancellationToken);
        var providers = await store.ListProvidersAsync(cancellationToken);
        var user = await CurrentUserAsync(context.User, store, cancellationToken);
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new
        {
            authenticated = user is not null,
            bootstrapRequired = users.Count == 0,
            username = user?.Username,
            mustChangePassword = user?.MustChangePassword ?? false,
            roles = context.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
            providers = providers.Where(provider => provider.Enabled).Select(provider => new
            {
                provider.Id, provider.DisplayName, type = provider.Type.ToString().ToLowerInvariant()
            }),
            antiforgeryToken = tokens.RequestToken
        });
    }

    private static async Task<IResult> BootstrapAsync(HttpContext context, BootstrapRequest request,
        AdminIdentityService service, IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            await antiforgery.ValidateRequestAsync(context);
            var user = await service.BootstrapAsync(request.Username, request.Password, cancellationToken);
            await service.SignInAsync(context, user);
            return Results.NoContent();
        });

    private static async Task<IResult> LoginAsync(HttpContext context, LoginRequest request,
        AdminIdentityService service, IExternalIdentityService external, IAntiforgery antiforgery,
        CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        await antiforgery.ValidateRequestAsync(context);
        var user = await service.PasswordSignInAsync(request.Username, request.Password, request.ProviderId, external,
            cancellationToken);
        await service.SignInAsync(context, user);
        return Results.NoContent();
    });

    private static async Task<IResult> LogoutAsync(HttpContext context, IOptions<AdminIdentityOptions> options,
        IAntiforgery antiforgery)
    {
        await antiforgery.ValidateRequestAsync(context);
        await context.SignOutAsync(options.Value.CookieScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePasswordAsync(HttpContext context, ChangePasswordRequest request,
        AdminIdentityService service, IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            await antiforgery.ValidateRequestAsync(context);
            var id = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await service.ChangePasswordAsync(id, request.CurrentPassword, request.NewPassword, cancellationToken);
            await context.SignOutAsync();
            return Results.NoContent();
        });

    private static async Task<IResult> StartExternalAsync(HttpContext context, string providerId,
        ExternalStartRequest request, IAdminIdentityStore store, IExternalIdentityService external,
        IAntiforgery antiforgery, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        await antiforgery.ValidateRequestAsync(context);
        var provider = await store.FindProviderAsync(providerId, cancellationToken)
                       ?? throw new IdentityOperationException("provider_not_found", "The identity provider was not found.", 404);
        if (!provider.Enabled) throw new IdentityOperationException("provider_disabled", "The identity provider is disabled.", 403);
        return Results.Ok(new { url = await external.CreateChallengeAsync(context, provider, request.ReturnUrl, cancellationToken) });
    });

    private static async Task<IResult> CompleteExternalAsync(HttpContext context, string providerId,
        IAdminIdentityStore store, IExternalIdentityService external, AdminIdentityService service,
        CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        var provider = await store.FindProviderAsync(providerId, cancellationToken)
                       ?? throw new IdentityOperationException("provider_not_found", "The identity provider was not found.", 404);
        var (result, returnUrl) = await external.CompleteChallengeAsync(context, provider, cancellationToken);
        var user = await service.ResolveExternalUserAsync(provider, result, cancellationToken);
        await service.SignInAsync(context, user);
        return Results.Redirect(returnUrl);
    });

    private static async Task<IResult> ListUsersAsync(IAdminIdentityStore store, AdminIdentityService service,
        CancellationToken cancellationToken)
    {
        var users = await store.ListUsersAsync(cancellationToken);
        return Results.Ok(users.OrderBy(user => user.Username).Select(user => UserResponse(user, service)));
    }

    private static async Task<IResult> CreateUserAsync(HttpContext context, CreateUserRequest request,
        AdminIdentityService service, IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            await antiforgery.ValidateRequestAsync(context);
            var result = await service.CreateUserAsync(request.Username, request.DisplayName, request.Roles,
                context.User, cancellationToken);
            return Results.Ok(new { temporaryPassword = result.TemporaryPassword, user = UserResponse(result.User, service) });
        });

    private static async Task<IResult> UpdateUserAsync(HttpContext context, Guid id, UpdateUserRequest request,
        AdminIdentityService service, IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            await antiforgery.ValidateRequestAsync(context);
            var user = await service.UpdateUserAsync(id, request.DisplayName, request.Roles, request.Enabled,
                request.Version, context.User, cancellationToken);
            return Results.Ok(UserResponse(user, service));
        });

    private static async Task<IResult> ResetPasswordAsync(HttpContext context, Guid id, AdminIdentityService service,
        IAntiforgery antiforgery, CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        await antiforgery.ValidateRequestAsync(context);
        return Results.Ok(new { temporaryPassword = await service.ResetPasswordAsync(id, context.User, cancellationToken) });
    });

    private static async Task<IResult> DeleteUserAsync(HttpContext context, Guid id, AdminIdentityService service,
        IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            await antiforgery.ValidateRequestAsync(context);
            if (context.User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
                throw new IdentityOperationException("self_delete", "You cannot delete your own account.", 409);
            await service.DeleteUserAsync(id, context.User, cancellationToken);
            return Results.NoContent();
        });

    private static async Task<IResult> ListProvidersAsync(IAdminIdentityStore store,
        CancellationToken cancellationToken)
    {
        var providers = await store.ListProvidersAsync(cancellationToken);
        return Results.Ok(providers.OrderBy(provider => provider.DisplayName).Select(ProviderResponse));
    }

    private static async Task<IResult> SaveProviderAsync(HttpContext context, string id,
        SaveProviderRequest request, IAdminIdentityStore store, IProductRoleCatalog roleCatalog,
        ExternalIdentityService external, IAdminIdentityAuditSink audit, IAntiforgery antiforgery,
        CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        await antiforgery.ValidateRequestAsync(context);
        ValidateProvider(id, request, roleCatalog);
        var existing = await store.FindProviderAsync(id, cancellationToken);
        if (existing is not null && !string.IsNullOrWhiteSpace(request.Version) && existing.Version != request.Version)
            throw new IdentityOperationException("concurrency_conflict", "The provider was changed by another administrator.", 409);
        var provider = new AdminIdentityProvider
        {
            Id = id.Trim(), DisplayName = request.DisplayName.Trim(), Type = request.Type,
            Enabled = request.Enabled, AutoProvision = request.AutoProvision,
            DefaultRoles = request.DefaultRoles.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RoleMappings = request.RoleMappings.Select(mapping => new AdminRoleMapping(mapping.Source.Trim(), mapping.Role)).ToList(),
            Settings = request.Settings.ToDictionary(pair => pair.Key, pair => JsonValue(pair.Value), StringComparer.OrdinalIgnoreCase),
            ProtectedSecret = string.IsNullOrWhiteSpace(request.Secret) ? existing?.ProtectedSecret ?? string.Empty : external.ProtectSecret(request.Secret),
            Version = Guid.NewGuid().ToString("D")
        };
        await store.SaveProviderAsync(provider, cancellationToken);
        foreach (var user in await store.ListUsersAsync(cancellationToken))
        {
            if (!user.ExternalIdentities.Any(identity => identity.ProviderId.Equals(id, StringComparison.OrdinalIgnoreCase)) &&
                !user.RoleGrants.Any(grant => grant.SourceId?.Equals(id, StringComparison.OrdinalIgnoreCase) == true)) continue;
            user.RoleGrants.RemoveAll(grant => grant.Source == "provider" &&
                                               grant.SourceId?.Equals(id, StringComparison.OrdinalIgnoreCase) == true);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.Version = Guid.NewGuid().ToString("D");
            await store.SaveUserAsync(user, cancellationToken);
        }
        await audit.WriteAsync("ProviderSaved", id, "Succeeded", context.User, cancellationToken);
        return Results.Ok(ProviderResponse(provider));
    });

    private static async Task<IResult> DeleteProviderAsync(HttpContext context, string id,
        IAdminIdentityStore store, IAdminIdentityAuditSink audit, IAntiforgery antiforgery,
        CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        await antiforgery.ValidateRequestAsync(context);
        await store.DeleteProviderAsync(id, cancellationToken);
        foreach (var user in await store.ListUsersAsync(cancellationToken))
        {
            if (!user.ExternalIdentities.Any(identity => identity.ProviderId.Equals(id, StringComparison.OrdinalIgnoreCase)) &&
                !user.RoleGrants.Any(grant => grant.SourceId?.Equals(id, StringComparison.OrdinalIgnoreCase) == true))
                continue;
            user.ExternalIdentities.RemoveAll(identity =>
                identity.ProviderId.Equals(id, StringComparison.OrdinalIgnoreCase));
            user.RoleGrants.RemoveAll(grant => grant.Source == "provider" &&
                                               grant.SourceId?.Equals(id, StringComparison.OrdinalIgnoreCase) == true);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.Version = Guid.NewGuid().ToString("D");
            await store.SaveUserAsync(user, cancellationToken);
        }
        await audit.WriteAsync("ProviderDeleted", id, "Succeeded", context.User, cancellationToken);
        return Results.NoContent();
    });

    private static async Task<IResult> TestProviderAsync(HttpContext context, SaveProviderRequest request,
        ExternalIdentityService external, IAdminIdentityStore store, IAntiforgery antiforgery,
        CancellationToken cancellationToken) => await ExecuteAsync(async () =>
    {
        await antiforgery.ValidateRequestAsync(context);
        var existing = await store.FindProviderAsync(request.Id, cancellationToken);
        var provider = new AdminIdentityProvider
        {
            Id = request.Id, DisplayName = request.DisplayName, Type = request.Type,
            Settings = request.Settings.ToDictionary(pair => pair.Key, pair => JsonValue(pair.Value), StringComparer.OrdinalIgnoreCase),
            ProtectedSecret = existing?.ProtectedSecret ?? string.Empty
        };
        var result = await external.TestAsync(provider, request.Secret, cancellationToken);
        return Results.Ok(new { succeeded = result.Succeeded, message = result.Message });
    });

    private static object UserResponse(AdminIdentityUser user, AdminIdentityService service) => new
    {
        id = user.Id, username = user.Username, displayName = user.DisplayName,
        roles = service.EffectiveRoles(user), enabled = user.Enabled,
        mustChangePassword = user.MustChangePassword, lastLoginAt = user.LastLoginAt,
        externalIdentities = user.ExternalIdentities, version = user.Version
    };
    private static object ProviderResponse(AdminIdentityProvider provider) => new
    {
        provider.Id, provider.DisplayName, type = provider.Type.ToString().ToLowerInvariant(), provider.Enabled,
        provider.AutoProvision, provider.DefaultRoles, provider.RoleMappings, hasSecret = !string.IsNullOrEmpty(provider.ProtectedSecret),
        settings = provider.Settings.ToDictionary(pair => pair.Key, pair => ParseValue(pair.Value)), provider.Version
    };

    private static async Task<AdminIdentityUser?> CurrentUserAsync(ClaimsPrincipal principal,
        IAdminIdentityStore store, CancellationToken cancellationToken) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? await store.FindUserAsync(id, cancellationToken) : null;
    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (IdentityOperationException exception) { return Results.Json(new { code = exception.Code, message = exception.Message }, statusCode: exception.StatusCode); }
        catch (AntiforgeryValidationException) { return Results.Json(new { code = "antiforgery_failed", message = "The request verification token is invalid." }, statusCode: 400); }
    }
    private static void ValidateProvider(string id, SaveProviderRequest request, IProductRoleCatalog roleCatalog)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(request.DisplayName))
            throw new IdentityOperationException("validation_error", "Provider ID and display name are required.");
        if (request.DefaultRoles.Concat(request.RoleMappings.Select(mapping => mapping.Role)).Any(role => !roleCatalog.Roles.Contains(role, StringComparer.OrdinalIgnoreCase)))
            throw new IdentityOperationException("validation_error", "A provider role is not supported by this product.");
    }
    private static string JsonValue(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    private static object ParseValue(string value) => bool.TryParse(value, out var boolean) ? boolean : int.TryParse(value, out var number) ? number : value;

    private sealed record BootstrapRequest(string Username, string Password);
    private sealed record LoginRequest(string Username, string Password, string? ProviderId);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    private sealed record ExternalStartRequest(string ReturnUrl);
    private sealed record CreateUserRequest(string Username, string? DisplayName, IReadOnlyList<string> Roles);
    private sealed record UpdateUserRequest(string? DisplayName, IReadOnlyList<string> Roles, bool Enabled, string Version);
    private sealed record SaveProviderRequest(string Id, string DisplayName, IdentityProviderType Type, bool Enabled,
        bool AutoProvision, IReadOnlyList<string> DefaultRoles, IReadOnlyList<AdminRoleMapping> RoleMappings,
        Dictionary<string, JsonElement> Settings, string? Secret, string? Version);
}
