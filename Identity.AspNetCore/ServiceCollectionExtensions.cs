using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Pebble.Identity;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPebbleIdentity(this IServiceCollection services,
        Action<AdminIdentityOptions>? configure = null)
    {
        var options = new AdminIdentityOptions();
        configure?.Invoke(options);
        services.AddSingleton(Options.Create(options));
        services.AddDataProtection();
        services.AddHttpClient(nameof(ExternalIdentityService));
        services.AddAntiforgery(antiforgery => antiforgery.HeaderName = options.AntiforgeryHeader);
        services.TryAddSingleton<IPasswordHasher<AdminIdentityUser>, PasswordHasher<AdminIdentityUser>>();
        services.TryAddScoped<IAdminIdentityPasswordService, AdminIdentityPasswordService>();
        services.TryAddScoped<ExternalIdentityService>();
        services.TryAddScoped<IExternalIdentityService>(provider => provider.GetRequiredService<ExternalIdentityService>());
        services.TryAddScoped<AdminIdentityService>();
        services.TryAddSingleton<IAdminIdentityAuditSink, NullAdminIdentityAuditSink>();
        if (options.RegisterCookieScheme)
            services.AddAuthentication().AddCookie(options.CookieScheme, cookie =>
        {
            cookie.Cookie.Name = options.CookieName;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            cookie.ExpireTimeSpan = options.SessionLifetime;
            cookie.SlidingExpiration = true;
            cookie.Events.OnRedirectToLogin = context => Status(context, StatusCodes.Status401Unauthorized);
            cookie.Events.OnRedirectToAccessDenied = context => Status(context, StatusCodes.Status403Forbidden);
            cookie.Events.OnValidatePrincipal = ValidatePrincipalAsync;
        });
        return services;
    }

    private static Task Status(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    }

    private static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            context.RejectPrincipal();
            return;
        }
        var store = context.HttpContext.RequestServices.GetRequiredService<IAdminIdentityStore>();
        var user = await store.FindUserAsync(id, context.HttpContext.RequestAborted);
        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<AdminIdentityOptions>>().Value;
        if (user is null || !user.Enabled || user.SecurityStamp != context.Principal.FindFirstValue(options.SecurityStampClaim))
            context.RejectPrincipal();
    }
}
