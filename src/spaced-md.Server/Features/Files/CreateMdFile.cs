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
                .WithTags("files");
        }

        public static IResult Handler(IServer sender, Request request, ApplicationDbContext context)
        {
            var mdfile = new MarkdownFile
            {
                Id = Guid.CreateVersion7(),
                FileName = request.Name,
                Content = request.Content,
                UploadedAt = DateTime.UtcNow,
                ApplicationUserId = string.Empty,
            };

            context.MarkdownFiles.Add(mdfile);
            context.SaveChanges();

            return Results.Ok(
                new Response(mdfile.Id));

        }
    }
}