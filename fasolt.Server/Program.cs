using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Api.Endpoints;
using Fasolt.Server.Api.Middleware;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using FSRS.Core.Extensions;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using dotenv.net;

DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 5));

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var bugsinkDsn = builder.Configuration["BUGSINK_DSN"];
if (!string.IsNullOrEmpty(bugsinkDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = bugsinkDsn;
        o.Environment = builder.Environment.EnvironmentName;
        o.SendDefaultPii = false;
    });
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration["DATABASE_URL"]);
    options.UseOpenIddict();
});

builder.Services.AddRazorPages();

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
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

var gitHubClientId = builder.Configuration["GITHUB_CLIENT_ID"];
var gitHubClientSecret = builder.Configuration["GITHUB_CLIENT_SECRET"];

if (!string.IsNullOrEmpty(gitHubClientId) && !string.IsNullOrEmpty(gitHubClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGitHub(options =>
        {
            options.ClientId = gitHubClientId;
            options.ClientSecret = gitHubClientSecret;
            options.CallbackPath = "/signin-github";
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });
}

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

        options.AllowCustomFlow(Fasolt.Server.Application.Auth.AppleAuthService.GrantType);

        options.RequireProofKeyForCodeExchange();

        var encryptionCertPath = builder.Configuration["OPENIDDICT_ENCRYPTION_CERT_PATH"];
        var signingCertPath = builder.Configuration["OPENIDDICT_SIGNING_CERT_PATH"];

        if (encryptionCertPath is not null && signingCertPath is not null)
        {
            options.AddEncryptionCertificate(System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(encryptionCertPath, null))
                   .AddSigningCertificate(System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(signingCertPath, null));
        }
        else if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        else
        {
            throw new InvalidOperationException(
                "OPENIDDICT_ENCRYPTION_CERT_PATH and OPENIDDICT_SIGNING_CERT_PATH " +
                "must be configured in non-development environments.");
        }

        // Disable transport security: in dev there's no TLS, in prod Cloudflare/Traefik terminates TLS
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

// Sign in with Apple — JWKS cache must be singleton so the in-memory
// cache survives across requests. Uses a named HttpClient so the
// typed-client's transient lifetime doesn't leak.
builder.Services.AddHttpClient(Fasolt.Server.Application.Auth.AppleJwksCache.HttpClientName);
builder.Services.AddSingleton<Fasolt.Server.Application.Auth.AppleJwksCache>();
builder.Services.AddScoped<Fasolt.Server.Application.Auth.AppleAuthService>();

// Email verification OTP service
builder.Services.AddSingleton(TimeProvider.System);
var otpPepper = builder.Configuration["OTP_PEPPER"]
    ?? throw new InvalidOperationException("OTP_PEPPER is not configured. Generate with 'openssl rand -hex 32' and set in .env or environment.");
// Outside of Development, refuse to start with a weak pepper. 32 chars is
// the minimum we consider acceptable (matches 'openssl rand -hex 16' /
// 128 bits). Catches misconfigurations where prod silently gets a test
// value — since the pepper is the only thing between a DB dump and an
// offline brute force of 6-digit codes.
if (!builder.Environment.IsDevelopment() && otpPepper.Length < 32)
    throw new InvalidOperationException(
        "OTP_PEPPER must be at least 32 characters in non-Development environments. Generate with 'openssl rand -hex 32'.");
builder.Services.AddScoped<Fasolt.Server.Application.Auth.IEmailVerificationCodeService>(sp =>
    new Fasolt.Server.Application.Auth.EmailVerificationCodeService(
        sp.GetRequiredService<Fasolt.Server.Infrastructure.Data.AppDbContext>(),
        otpPepper,
        sp.GetRequiredService<TimeProvider>()));

// Password reset OTP service — same pepper, same TimeProvider, different
// entity table. Shares the OTP state machine via OtpCodeStore<T>.
builder.Services.AddScoped<Fasolt.Server.Application.Auth.IPasswordResetCodeService>(sp =>
    new Fasolt.Server.Application.Auth.PasswordResetCodeService(
        sp.GetRequiredService<Fasolt.Server.Infrastructure.Data.AppDbContext>(),
        otpPepper,
        sp.GetRequiredService<TimeProvider>()));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
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
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            title = "Email not verified",
            status = 403,
            detail = "You must verify your email address before accessing this resource."
        });
    };
});

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(24);
});

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("fasolt");

var plunkApiKey = builder.Configuration["PLUNK_API_KEY"];
if (!builder.Environment.IsDevelopment())
{
    // Fail fast: DevEmailSender logs codes and links to the application log.
    // In production that's a credential-in-logs finding — OTPs, confirmation
    // links, and password reset tokens would all end up in Bugsink/Sentry and
    // any aggregator with log-read access. Never allow the dev sender in prod.
    if (string.IsNullOrEmpty(plunkApiKey))
        throw new InvalidOperationException(
            "PLUNK_API_KEY is required in non-development environments. " +
            "The dev email sender must never run in production because it logs verification codes.");

    builder.Services.AddHttpClient<PlunkEmailSender>((sp, client) =>
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", plunkApiKey);
    });
    builder.Services.AddTransient<IEmailSender<AppUser>>(sp => sp.GetRequiredService<PlunkEmailSender>());
    builder.Services.AddTransient<IOtpEmailSender>(sp => sp.GetRequiredService<PlunkEmailSender>());
}
else
{
    builder.Services.AddSingleton<Fasolt.Server.Infrastructure.Services.TestEmailSink>();
    builder.Services.AddTransient<DevEmailSender>();
    builder.Services.AddTransient<IEmailSender<AppUser>>(sp => sp.GetRequiredService<DevEmailSender>());
    builder.Services.AddTransient<IOtpEmailSender>(sp => sp.GetRequiredService<DevEmailSender>());
}

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes(
            IdentityConstants.ApplicationScheme,
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
        .Build();
    options.AddPolicy("AdminCookieOnly", policy =>
        policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
              .RequireRole("Admin"));
    options.AddPolicy("EmailVerified", policy =>
        policy.RequireAuthenticatedUser()
              .AddAuthenticationSchemes(
                  IdentityConstants.ApplicationScheme,
                  OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
              .RequireClaim("email_confirmed", "true"));
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

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                // In development, use a high limit so E2E test suites don't exhaust
                // the quota. Production keeps the real 10/min guard.
                PermitLimit = builder.Environment.IsDevelopment() ? 100 : 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    options.AddPolicy("auth-strict", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));

    options.AddPolicy("api", context =>
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

builder.Services.AddAntiforgery();
builder.Services.AddScoped<CardService>();
builder.Services.AddScoped<DeckService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<SourceService>();
builder.Services.AddScoped<OverviewService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<DeviceTokenService>();
builder.Services.AddScoped<SchedulingSettingsService>();
builder.Services.AddScoped<DeckSnapshotService>();
builder.Services.AddScoped<AccountDataService>();

var apnsKeyId = builder.Configuration["APNS_KEY_ID"];
var apnsKeyBase64 = builder.Configuration["APNS_KEY_BASE64"];
var apnsKeyPath = builder.Configuration["APNS_KEY_PATH"];
var apnsKeyReady = !string.IsNullOrEmpty(apnsKeyId) &&
    (!string.IsNullOrEmpty(apnsKeyBase64) ||
     (!string.IsNullOrEmpty(apnsKeyPath) && File.Exists(apnsKeyPath)));
if (apnsKeyReady)
{
    var apnsSettings = new ApnsSettings
    {
        KeyId = apnsKeyId!,
        TeamId = builder.Configuration["APPLE_TEAM_ID"] ?? "",
        BundleId = builder.Configuration["APPLE_BUNDLE_ID"] ?? "com.fasolt.app",
        KeyBase64 = apnsKeyBase64,
        KeyPath = apnsKeyPath,
        UseSandbox = !bool.TryParse(builder.Configuration["APNS_USE_SANDBOX"], out var sandbox) || sandbox,
    };
    builder.Services.AddSingleton(apnsSettings);
    builder.Services.AddHttpClient<ApnsService>();
    builder.Services.AddSingleton<NotificationBackgroundService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<NotificationBackgroundService>());
}

builder.Services.AddFSRS(options =>
{
    options.DesiredRetention = 0.9;
    options.MaximumInterval = 36500;
    options.EnableFuzzing = true;
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithToolsFromAssembly();

builder.Services.AddOpenApi();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Seed Admin role
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    // Seed first-party iOS OAuth client
    var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    const string iosClientId = "fasolt-ios";
    const string appleGrantType = Fasolt.Server.Application.Auth.AppleAuthService.GrantType;
    var existing = await appManager.FindByClientIdAsync(iosClientId);

    if (existing is null)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = iosClientId,
            DisplayName = "Fasolt iOS",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ApplicationType = OpenIddictConstants.ApplicationTypes.Native,
            ConsentType = OpenIddictConstants.ConsentTypes.Systematic,
        };
        descriptor.RedirectUris.Add(new Uri("fasolt://oauth/callback"));
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.GrantType + appleGrantType);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess);
        await appManager.CreateAsync(descriptor);
    }
    else
    {
        // Idempotently ensure the Apple grant type permission is present on existing dev/prod clients.
        var permissions = await appManager.GetPermissionsAsync(existing);
        var appleGrantPermission = OpenIddictConstants.Permissions.Prefixes.GrantType + appleGrantType;
        if (!permissions.Contains(appleGrantPermission))
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await appManager.PopulateAsync(descriptor, existing);
            descriptor.Permissions.Add(appleGrantPermission);
            await appManager.UpdateAsync(existing, descriptor);
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(error => error.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\":\"An unexpected error occurred.\"}");
    }));
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment())
{
    await DevSeedData.SeedAsync(app.Services);
}

// Promote configured admin email
var adminEmail = app.Configuration["ADMIN_EMAIL"];
if (!string.IsNullOrEmpty(adminEmail))
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser is not null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
    {
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}

if (!app.Environment.IsDevelopment())
{
    var corsOrigins = app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    if (corsOrigins is null || corsOrigins.Length == 0)
    {
        app.Logger.LogWarning("Cors:AllowedOrigins is not configured. CORS will block all cross-origin requests in production.");
    }
}

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost,
};

var trustAllProxies = builder.Configuration.GetValue("ReverseProxy:TrustAllProxies",
    defaultValue: app.Environment.IsDevelopment());

if (trustAllProxies)
{
    forwardedHeadersOptions.KnownProxies.Clear();
    forwardedHeadersOptions.KnownIPNetworks.Clear();

    if (!app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning("ReverseProxy:TrustAllProxies is enabled in a non-development environment. " +
            "Consider configuring ReverseProxy:KnownNetworks for production.");
    }
}
else
{
    var knownNetworks = builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>();
    if (knownNetworks is not null)
    {
        foreach (var cidr in knownNetworks)
        {
            var parts = cidr.Split('/');
            if (parts.Length == 2 && System.Net.IPAddress.TryParse(parts[0], out var address) && int.TryParse(parts[1], out var prefixLength))
            {
                forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(address, prefixLength));
            }
        }
    }
}

app.UseForwardedHeaders(forwardedHeadersOptions);

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
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'";
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
        dict["registration_endpoint"] = $"{baseUrl}/oauth/clients/register";
        dict["logo_uri"] = $"{baseUrl}/logo.png";

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
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/oauth"),
    branch => branch.UseMiddleware<Fasolt.Server.Api.Middleware.ContentSecurityPolicyMiddleware>());
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
app.MapRazorPages();
app.MapAdminEndpoints();
app.MapNotificationEndpoints();
app.MapSchedulingSettingsEndpoints();
app.MapSnapshotEndpoints();
app.MapDemoDeckEndpoints();
// Identity's MapIdentityApi removed — all auth endpoints are in AccountEndpoints

// Apple App Site Association — links the iOS app and the website so credentials
// saved in iCloud Keychain by the native register form autofill in the OAuth
// web view (and vice versa). Apple's CDN fetches this file periodically; it
// must be served as JSON with no extension and no auth.
app.MapGet("/.well-known/apple-app-site-association", [Microsoft.AspNetCore.Authorization.AllowAnonymous] (IConfiguration configuration) =>
{
    var teamId = configuration["APPLE_TEAM_ID"];
    var bundleId = configuration["APPLE_BUNDLE_ID"];
    if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(bundleId))
        return Results.NotFound();

    var payload = new
    {
        webcredentials = new
        {
            apps = new[] { $"{teamId}.{bundleId}" }
        }
    };
    return Results.Json(payload, contentType: "application/json");
});

app.MapMcp("/mcp").RequireAuthorization("EmailVerified").RequireRateLimiting("api");

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/test/last-email", (string email, Fasolt.Server.Infrastructure.Services.TestEmailSink sink) =>
    {
        var captured = sink.GetLast(email);
        return captured is null ? Results.NotFound() : Results.Ok(captured);
    }).AllowAnonymous();
}

// SPA fallback — serve index.html for client-side routes
app.MapFallbackToFile("index.html");

app.Run();

// Expose Program as partial for WebApplicationFactory in integration tests
public partial class Program { }
