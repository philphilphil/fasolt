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
    .AddCookie(IdentityConstants.ApplicationScheme)
    .AddBearerToken(IdentityConstants.BearerScheme);

builder.Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddUserManager<UserManager<IdentityUser>>()
    .AddRoleManager<RoleManager<IdentityRole>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddApiEndpoints();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueDev", policy =>
        policy.WithOrigins("http://localhost:5041")
              .AllowAnyHeader()
              .AllowAnyMethod()
    );
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
    app.MapScalarApiReference();

    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        await InitUserSeeder.Initialize(services);
    }
}

app.UseHttpsRedirection();
app.MapEndpoints();
app.MapFallbackToFile("/index.html");
app.MapIdentityApi<IdentityUser>()
    .WithTags("Identity");

app.Run();


