using System.Security.Claims;
using Microsoft.AspNetCore.Hosting.Server;
using spaced_md.Infrastructure.Database;

namespace spaced_md.Server
{
    public class CreateCard : IEndpoint
    {

    public record Request(string Name, string Content);
    public record Response(Guid Id);

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("file", Handler)
                .WithTags("MdFiles")
                .RequireAuthorization();
        }

        public static IResult Handler(HttpContext httpContext, Request request, ApplicationDbContext context)
        {
            var mdfile = new MarkdownFile
            {
                Id = Guid.CreateVersion7(),
                FileName = request.Name,
                Content = request.Content,
                UploadedAt = DateTime.UtcNow,
                ApplicationUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new Exception("User not found"),
            };

            context.MarkdownFiles.Add(mdfile);
            context.SaveChanges();

            return Results.Ok(
                new Response(mdfile.Id));

        }
    }
}