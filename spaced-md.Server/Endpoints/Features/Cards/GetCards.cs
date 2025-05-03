using System.Security.Claims;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using spaced_md.Infrastructure.Database;
using SpacedMd.Server.Services;
using static SpacedMd.Server.Services.MarkdownService;

namespace spaced_md.Server
{
    public class GetCards : IEndpoint
    {
        public record CardResponse(Guid Id, string Name, string Content, UsageType UsageType, string MdFileName, Guid MdFileId, DateTime UploadedAt, DateTime? UpdatedAt = null);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("card/{id}", SingleHandler)
                .WithTags("Cards")
                .Produces<CardResponse>()
                .RequireAuthorization();

            app.MapGet("card", AllHandler)
                .WithTags("Cards")
                .Produces<CardResponse[]>()
                .RequireAuthorization();
        }

        public static IResult SingleHandler(HttpContext httpContext, Guid? id, ApplicationDbContext context, IMarkdownService _mdService)
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (id == null)
                return Results.BadRequest("Id is required");
            if (id == Guid.Empty)
                return Results.BadRequest("Id cannot be empty");

            var card = context.Cards
                .Include(x => x.MarkdownFile)
                .FirstOrDefault(x => x.ApplicationUserId == userId && x.Id == id && !x.IsDeleted);
            if (card == null)
                return Results.NotFound("Card not found");

            return Results.Ok(
                new CardResponse(card.Id, card.Title, card.Content, card.UsageType, card.MarkdownFile!.FileName, card.MarkdownFileId, card.CreatedAt, card.UpdatedAt));
        }

        public static IResult AllHandler(HttpContext httpContext, Guid? id, ApplicationDbContext context)
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var usersCards = context.Cards
                .Include(x => x.MarkdownFile)
                .Where(x => x.ApplicationUserId == userId && x.DeletedAt == null)
                .ToList();

            if (usersCards.Count == 0)
                return Results.NotFound("No cards found");

            var cards = usersCards.Select(x => new CardResponse(x.Id, x.Title, x.Content, x.UsageType, x.MarkdownFile!.FileName, x.MarkdownFileId, x.CreatedAt, x.UpdatedAt)).ToArray();
            return Results.Ok(cards);
        }
    }
}