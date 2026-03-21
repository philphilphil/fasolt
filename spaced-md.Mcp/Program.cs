using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using SpacedMd.Mcp;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (required for stdio transport)
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

var baseUrl = Environment.GetEnvironmentVariable("SPACED_MD_URL") ?? "https://spaced-md.io";
var token = Environment.GetEnvironmentVariable("SPACED_MD_TOKEN");

if (string.IsNullOrEmpty(token))
{
    Console.Error.WriteLine("SPACED_MD_TOKEN environment variable is required");
    Environment.Exit(1);
}

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
