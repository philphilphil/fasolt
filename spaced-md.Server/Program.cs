using Microsoft.EntityFrameworkCore;
using spaced_md.Infrastructure.Database;
using Microsoft.AspNetCore.OpenApi;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Identity;
using spaced_md.Infrastructure.Auth;

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

builder.Services.AddIdentityCore<User>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddApiEndpoints();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapEndpoints();
app.MapFallbackToFile("/index.html");
app.MapIdentityApi<User>()
    .WithTags("Identity");

app.Run();


