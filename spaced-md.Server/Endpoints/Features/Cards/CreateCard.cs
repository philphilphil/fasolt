using System.Security.Claims;
using spaced_md.Infrastructure.Database;

namespace spaced_md.Server.Endpoints.Features.Cards
{
    public class CreateCard : IEndpoint
    {
        public record CardCreateRequest(string Title, string Content, Guid MarkdownFileId);
        public record CardResponse(Guid Id);

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("card", Handler)
               .WithTags("Cards")
               .RequireAuthorization();
        }

        public static IResult Handler(HttpContext httpContext, CardCreateRequest request, ApplicationDbContext context)
        {
            var card = new Card
            {
                Id = Guid.CreateVersion7(),
                Title = request.Title,
                Content = request.Content,
                CreatedAt = DateTime.UtcNow,
                ApplicationUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new Exception("User not found"),
                MarkdownFileId = request.MarkdownFileId,
            };

            context.Cards.Add(card);
            context.SaveChanges();

            return Results.Ok(new CardResponse(card.Id));
        }
    }
}