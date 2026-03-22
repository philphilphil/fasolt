using System.Net.Http.Json;
using System.Text.Json;

namespace Fasolt.Mcp;

public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<T> GetAsync<T>(string path)
    {
        var response = await http.GetAsync(path);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    public async Task<T> PostAsync<T>(string path, object body)
    {
        var response = await http.PostAsJsonAsync(path, body, JsonOptions);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        try
        {
            var error = JsonSerializer.Deserialize<ApiError>(body, JsonOptions);
            throw new McpApiException(
                (int)response.StatusCode,
                error?.Message ?? $"API returned {(int)response.StatusCode}");
        }
        catch (JsonException)
        {
            throw new McpApiException((int)response.StatusCode, $"API returned {(int)response.StatusCode}: {body}");
        }
    }
}

public record ApiError(string Error, string Message);

public class McpApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
