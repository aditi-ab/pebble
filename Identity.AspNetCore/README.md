# Pebble.Identity.AspNetCore

ASP.NET Core identity services and minimal API endpoints matching `@pebble/identity`.

## Registration

```csharp
using Pebble.Identity;

builder.Services.AddPebbleIdentity(options =>
{
    options.BasePath = "/admin";
    options.AdministratorPolicy = "Administrator";
});

builder.Services.AddScoped<IAdminIdentityStore, ApplicationIdentityStore>();
builder.Services.AddSingleton<IProductRoleCatalog, ApplicationRoleCatalog>();
builder.Services.AddScoped<IAdminIdentityAuditSink, ApplicationIdentityAuditSink>();

app.MapPebbleIdentity();
```

The host application owns persistence by implementing `IAdminIdentityStore`. It also supplies its assignable roles through `IProductRoleCatalog`. Registering an audit sink is optional, but recommended. The default sink discards audit events.

## Configurable routes

`BasePath` and every route template can be changed during registration:

```csharp
builder.Services.AddPebbleIdentity(options =>
{
    options.BasePath = "/control-plane";
    options.AntiforgeryHeader = "X-Antiforgery";
    options.Endpoints.Authentication = "/session";
    options.Endpoints.Management = "/access";
    options.Endpoints.Login = "/password";
    options.Endpoints.Users = "/accounts";
    options.Endpoints.User = "/accounts/{id:guid}";
    options.Endpoints.ResetPassword = "/accounts/{id:guid}/reset-password";
});
```

Keep `{providerId}` in both external authentication templates, `{id:guid}` in user templates, and `{id}` in provider templates. Configure the matching paths in `createIdentityApi` when the browser client does not use the defaults.

`MapPebbleIdentity()` maps authentication, external authentication, and management routes. Hosts that provide their own local authentication endpoints can instead call `MapPebbleIdentityExternalAuthentication()` and `MapPebbleIdentityManagement()` separately.

## Security behavior

The package uses ASP.NET Core data protection for provider secrets and external-login state. Local passwords use `IPasswordHasher<AdminIdentityUser>`. Mutations validate antiforgery tokens, management endpoints require the configured administrator policy, and the default cookie is HTTP-only with strict same-site behavior.

Persist data-protection keys outside ephemeral containers. Changing data-protection purposes or losing the key ring invalidates protected provider secrets and active external-login state.

See the `@pebble/identity` README for the complete default HTTP contract and custom client implementations.
