using Microsoft.EntityFrameworkCore;
using spaced_md.Infrastructure.Database;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddEndpoints(typeof(Program).Assembly);
builder.Services.AddAuthorization();
builder.Services.AddAuthentication()
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "spaced-md";
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(10);
    });

builder.Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddUserManager<UserManager<IdentityUser>>()
    .AddRoleManager<RoleManager<IdentityRole>>()
    .AddSignInManager<SignInManager<IdentityUser>>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CookiesPolicy", builder =>
    {
        builder.WithOrigins("http://localhost:52325")
               .AllowCredentials()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
    options.AddPolicy("CookiesPolicy", builder =>
    {
        builder.WithOrigins("http://localhost:5041")
               .AllowCredentials()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapOpenApi();

app.UseCors(builder =>
{
    builder
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
});


if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options =>
    {
        options
            .WithHttpBearerAuthentication(bearer =>
            {
                bearer.Token = "";
            });
    });

    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        await InitUserSeeder.Initialize(services);
    }
}

app.UseHttpsRedirection();
app.MapEndpoints();
app.MapFallbackToFile("/index.html");

app.Run();

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}