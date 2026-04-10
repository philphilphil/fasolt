using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Http;

namespace Fasolt.Server.Infrastructure.Logging;

public sealed class AxiomHttpClient : IHttpClient
{
    private readonly HttpClient _httpClient = new();

    public AxiomHttpClient(string apiToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
    }

    public void Configure(IConfiguration configuration) { }

    public async Task<HttpResponseMessage> PostAsync(string requestUri, Stream contentStream,
        CancellationToken cancellationToken = default)
    {
        using var content = new StreamContent(contentStream);
        content.Headers.Add("Content-Type", "application/json");
        return await _httpClient.PostAsync(requestUri, content, cancellationToken);
    }

    public void Dispose() => _httpClient.Dispose();
}
