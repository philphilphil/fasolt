using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using SpacedMd.Mcp;

namespace SpacedMd.Mcp.Tools;

[McpServerToolType]
public class FileTools
{
    [McpServerTool, Description("Upload or update a markdown file (upsert by filename). Returns file ID, headings, and any orphaned cards if updating.")]
    public static async Task<string> UploadFile(
        ApiClient api,
        [Description("The filename (must end with .md)")] string fileName,
        [Description("The full markdown content of the file")] string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var result = await api.PostFileAsync<JsonElement>("/api/files", fileName, stream);
        return result.GetRawText();
    }

    [McpServerTool, Description("List all uploaded markdown files with headings and card counts. Supports cursor-based pagination.")]
    public static async Task<string> ListFiles(
        ApiClient api,
        [Description("Max results to return (1-200, default 50)")] int? limit = null,
        [Description("Cursor from previous page's nextCursor field")] string? after = null)
    {
        var queryParams = new List<string>();
        if (limit.HasValue) queryParams.Add($"limit={limit.Value}");
        if (after is not null) queryParams.Add($"after={Uri.EscapeDataString(after)}");

        var qs = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var result = await api.GetAsync<JsonElement>($"/api/files{qs}");
        return result.GetRawText();
    }

    [McpServerTool, Description("Get a file's full content, headings, and card count by its ID.")]
    public static async Task<string> GetFile(
        ApiClient api,
        [Description("The file ID (GUID)")] string fileId)
    {
        var result = await api.GetAsync<JsonElement>($"/api/files/{Uri.EscapeDataString(fileId)}");
        return result.GetRawText();
    }
}
