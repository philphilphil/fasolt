using System.Security.Claims;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using spaced_md.Infrastructure.Database;

namespace spaced_md.Server
{
    public class GetMdFiles : IEndpoint
    {

    public record MdFileRequest(Guid id);

    public record MdFileResponse(Guid Id, string FileName, string Content, DateTime UploadedAt);

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("mdfile", Handler)
                .WithTags("MdFiles");
                // .RequireAuthorization();
        }

        public static IResult Handler(HttpContext httpContext, [FromBody] MdFileRequest request, ApplicationDbContext context)
        {
            var usersMdFiles = context.MarkdownFiles
                .Where(x => x.ApplicationUserId == httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier))
                .ToList();

            if (usersMdFiles.Count == 0)
                return Results.NotFound("No files found");

            if (request.id == default)  
                return Results.Ok(usersMdFiles.Select(x => new MdFileResponse(x.Id, x.FileName, x.Content, x.UploadedAt)));

            var mdFile = usersMdFiles.FirstOrDefault(x => x.Id == request.id);
            if (mdFile == null)
                return Results.NotFound("File not found");

            return Results.Ok(
                new MdFileResponse(mdFile.Id, mdFile.FileName, mdFile.Content, mdFile.UploadedAt));
        }
    }
}