using Fasolt.Server.Api.McpTools;
using Fasolt.Server.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpResources;

internal static class McpResourceHandlers
{
    public static IMcpServerBuilder AddFasoltResources(this IMcpServerBuilder builder)
    {
        builder.WithListResourcesHandler(async (ctx, ct) =>
        {
            var services = ctx.Services
                ?? throw new InvalidOperationException("No request services available.");
            var http = services.GetRequiredService<IHttpContextAccessor>();
            var resourceService = services.GetRequiredService<McpResourceService>();
            var logger = services.GetService<ILoggerFactory>()?.CreateLogger("McpResources");

            var userId = ResolveUserId(http);
            logger?.LogInformation("MCP resources/list called by user {UserId}", userId);

            try
            {
                var entries = await resourceService.ListUserResourcesAsync(userId);

                return new ListResourcesResult
                {
                    Resources = entries.Select(e => new Resource
                    {
                        Uri = e.Uri,
                        Name = e.Name,
                        Description = e.Description,
                        MimeType = e.MimeType,
                    }).ToList(),
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not McpException)
            {
                logger?.LogError(ex, "MCP resources/list threw for user {UserId}", userId);
                throw;
            }
        });

        builder.WithListResourceTemplatesHandler((ctx, ct) =>
            ValueTask.FromResult(new ListResourceTemplatesResult
            {
                ResourceTemplates =
                [
                    new ResourceTemplate
                    {
                        UriTemplate = ResourceUris.DeckTemplate,
                        Name = "Deck",
                        Description = "A specific deck's cards rendered as markdown",
                        MimeType = "text/markdown",
                    },
                ],
            }));

        builder.WithReadResourceHandler(async (ctx, ct) =>
        {
            var services = ctx.Services
                ?? throw new InvalidOperationException("No request services available.");
            var http = services.GetRequiredService<IHttpContextAccessor>();
            var resourceService = services.GetRequiredService<McpResourceService>();
            var logger = services.GetService<ILoggerFactory>()?.CreateLogger("McpResources");

            var userId = ResolveUserId(http);
            var uri = ctx.Params?.Uri
                ?? throw new McpProtocolException("Missing resource URI.", McpErrorCode.InvalidParams);

            logger?.LogInformation("MCP resources/read {Uri} called by user {UserId}", uri, userId);

            try
            {
                string text;

                if (uri == ResourceUris.DueToday)
                {
                    text = await resourceService.RenderDueTodayAsync(userId);
                }
                else if (uri == ResourceUris.Recent)
                {
                    text = await resourceService.RenderRecentAsync(userId);
                }
                else if (ResourceUris.TryParseDeck(uri, out var deckId))
                {
                    var rendered = await resourceService.RenderDeckAsync(userId, deckId);
                    if (rendered is null)
                        throw new McpProtocolException($"Deck not found: {deckId}", McpErrorCode.InvalidParams);
                    text = rendered;
                }
                else
                {
                    throw new McpProtocolException($"Unknown resource URI: {uri}", McpErrorCode.InvalidParams);
                }

                return new ReadResourceResult
                {
                    Contents =
                    [
                        new TextResourceContents
                        {
                            Uri = uri,
                            MimeType = "text/markdown",
                            Text = text,
                        },
                    ],
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not McpException)
            {
                logger?.LogError(ex, "MCP resources/read {Uri} threw for user {UserId}", uri, userId);
                throw;
            }
        });

        return builder;
    }

    private static string ResolveUserId(IHttpContextAccessor http)
    {
        try
        {
            return McpUserResolver.GetUserId(http);
        }
        catch (UnauthorizedAccessException)
        {
            throw new McpProtocolException("Not authenticated.", McpErrorCode.InvalidRequest);
        }
    }
}
