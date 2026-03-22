using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Api.Auth;
using Fasolt.Server.Api.Endpoints;
using Fasolt.Server.Api.Middleware;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Server.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

builder.Services.AddAuthentication()
    .AddScheme<BearerTokenOptions, BearerTokenHandler>(
        BearerTokenDefaults.AuthenticationScheme, _ => { });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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

builder.Services.AddTransient<IEmailSender<AppUser>, DevEmailSender>();

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
        Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme,
        BearerTokenDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment())
{
    await DevSeedData.SeedAsync(app.Services);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ErrorResponseMiddleware>();

app.MapHealthEndpoints();
app.MapAccountEndpoints();
app.MapCardEndpoints();
app.MapReviewEndpoints();
app.MapDeckEndpoints();
app.MapSearchEndpoints();
app.MapSourceEndpoints();
app.MapApiTokenEndpoints();
app.MapGroup("/api/identity").MapIdentityApi<AppUser>();

app.Run();

// Expose Program as partial for WebApplicationFactory in integration tests
public partial class Program { }
