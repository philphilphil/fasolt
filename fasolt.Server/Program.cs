using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Api.Endpoints;
using Fasolt.Server.Api.Middleware;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Infrastructure.Services;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.UseOpenIddict();
});

builder.Services
    .AddIdentityApiEndpoints<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;

        options.User.RequireUniqueEmail = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<AppDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/oauth/authorize")
               .SetTokenEndpointUris("/oauth/token");

        options.AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow();

        options.RequireProofKeyForCodeExchange();

        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .DisableTransportSecurityRequirement();

        options.SetAccessTokenLifetime(TimeSpan.FromHours(1))
               .SetRefreshTokenLifetime(TimeSpan.FromDays(14));

        // Accept any resource parameter from MCP clients (single-tenant, we are the resource server)
        options.DisableResourceValidation();
        options.IgnoreResourcePermissions();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(1);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
});

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});

if (builder.Environment.IsDevelopment())
    builder.Services.AddTransient<IEmailSender<AppUser>, DevEmailSender>();

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes(
            IdentityConstants.ApplicationScheme,
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
        .Build();
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowCredentials()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        // In production, configure via environment variable
        else
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            policy.WithOrigins(origins)
                  .AllowCredentials()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

builder.Services.AddScoped<CardService>();
builder.Services.AddScoped<DeckService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<SourceService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithToolsFromAssembly();

builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment())
{
    await DevSeedData.SeedAsync(app.Services);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
// Inject registration_endpoint into OpenIddict's auto-generated discovery document
app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/.well-known/openid-configuration")
        || context.Request.Path.Equals("/.well-known/oauth-authorization-server"))
    {
        var originalBody = context.Response.Body;
        using var memStream = new MemoryStream();
        context.Response.Body = memStream;

        await next();

        memStream.Position = 0;
        var json = await System.Text.Json.JsonDocument.ParseAsync(memStream);
        var dict = new Dictionary<string, object?>();
        foreach (var prop in json.RootElement.EnumerateObject())
            dict[prop.Name] = prop.Value;

        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        dict["registration_endpoint"] = $"{baseUrl}/oauth/register";

        context.Response.Body = originalBody;
        context.Response.ContentLength = null;
        await System.Text.Json.JsonSerializer.SerializeAsync(originalBody, dict);
        return;
    }
    await next();
});
// RFC 9728: MCP endpoint 401 must include resource_metadata in WWW-Authenticate
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp"))
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode == 401)
            {
                var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
                context.Response.Headers.Remove("WWW-Authenticate");
                context.Response.Headers.Append("WWW-Authenticate",
                    $"Bearer resource_metadata=\"{baseUrl}/.well-known/oauth-protected-resource\"");
            }
            return Task.CompletedTask;
        });
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<ErrorResponseMiddleware>();

app.MapHealthEndpoints();
app.MapAccountEndpoints();
app.MapCardEndpoints();
app.MapReviewEndpoints();
app.MapDeckEndpoints();
app.MapSearchEndpoints();
app.MapSourceEndpoints();
app.MapOAuthEndpoints();
app.MapGroup("/api/identity").MapIdentityApi<AppUser>().RequireRateLimiting("auth");

app.MapMcp("/mcp").RequireAuthorization();

// SPA fallback — serve index.html for client-side routes
app.MapFallbackToFile("index.html");

app.Run();

// Expose Program as partial for WebApplicationFactory in integration tests
public partial class Program { }
