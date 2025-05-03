using System.Security.Claims;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using spaced_md.Infrastructure.Database;
using SpacedMd.Server.Services;
using static SpacedMd.Server.Services.MarkdownService;

namespace spaced_md.Server
{
    public class GetMdFiles : IEndpoint
    {
        public record MdFileResponse(Guid Id, string FileName, string Content, DateTime UploadedAt, List<MdHeading>? headings = null, DateTime? UpdatedAt = null, DateTime? DeletedAt = null);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("mdfiles/{id}", SingleHandler)
                .WithTags("MdFiles")
                .Produces<MdFileResponse>()
                .RequireAuthorization();

            app.MapGet("mdfiles", AllHandler)
                .WithTags("MdFiles")
                .Produces<MdFileResponse[]>()
                .RequireAuthorization();
        }

        public static IResult SingleHandler(HttpContext httpContext, Guid? id, ApplicationDbContext context, IMarkdownService _mdService)
        {
            // TODO: repalce with fluent validation
            if (id == null)
                return Results.BadRequest("Id is required");

            if (httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) == null)
                return Results.Unauthorized();

            if (id == Guid.Empty)
                return Results.BadRequest("Id cannot be empty");

            if (context.MarkdownFiles == null)
                return Results.NotFound("No files found");

            var mdFile = context.MarkdownFiles
                .FirstOrDefault(x => x.ApplicationUserId == httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) && x.Id == id);
            if (mdFile == null)
                return Results.NotFound("File not found");

            var headings = _mdService.GetHeadings(mdFile.Content);

            return Results.Ok(
                new MdFileResponse(mdFile.Id, mdFile.FileName, mdFile.Content, mdFile.CreatedAt, headings, mdFile.UpdatedAt, mdFile.DeletedAt));
        }

        public static IResult AllHandler(HttpContext httpContext, Guid? id, ApplicationDbContext context)
        {
            var usersMdFiles = context.MarkdownFiles
                .Where(x => x.ApplicationUserId == httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier))
                .ToList();

            if (usersMdFiles.Count == 0)
                return Results.NotFound("No files found");

            return Results.Ok(usersMdFiles.Select(x => new MdFileResponse(
                x.Id,
                x.FileName,
                x.Content.Length > 200 ? x.Content.Substring(0, 200) + "..." : x.Content,
                x.CreatedAt)));
        }
    }
}