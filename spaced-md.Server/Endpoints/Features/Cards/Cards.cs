using System.Security.Claims;
using System.Text.Json.Serialization;
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

        public record CardRequest(string Title, string Content, Guid MdFileId, CardUsageType UsageType, string? Heading, string? HeadingLineNr);
        public record CardResponse(Guid Id, string Title, string Content, CardUsageType UsageType, string MdFileName, Guid MdFileId, DateTime UploadedAt, string? heading, DateTime? UpdatedAt = null);

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("cards/{id}", Get)
                .WithTags("Cards")
                .Produces<CardResponse>()
                .RequireAuthorization();

            app.MapGet("cards", GetAll)
                .WithTags("Cards")
                .Produces<CardResponse[]>()
                .RequireAuthorization();

            app.MapDelete("cards/{id}", Delete)
                .WithTags("Cards")
                .Produces(StatusCodes.Status200OK)
                .RequireAuthorization();

            app.MapPost("card", Post)
                .WithTags("Cards")
                .Produces(StatusCodes.Status200OK)
                .RequireAuthorization();
        }

        public static IResult Get(HttpContext httpContext, Guid? id, ApplicationDbContext context, IMarkdownService _mdService)
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (id == null)
                return Results.BadRequest("Id is required");
            if (id == Guid.Empty)
                return Results.BadRequest("Id cannot be empty");

            var card = context.Cards
                .Include(x => x.MarkdownFile)
                .FirstOrDefault(x => x.ApplicationUserId == userId && x.Id == id && x.DeletedAt == null);
            if (card == null)
                return Results.NotFound("Card not found");

            return Results.Ok(
                new CardResponse(card.Id, card.Title, card.Content, card.UsageType, card.MarkdownFile!.FileName, card.MarkdownFileId, card.CreatedAt, card.Heading,  card.UpdatedAt));
        }

        public static IResult GetAll(HttpContext httpContext, Guid? id, ApplicationDbContext context)
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var usersCards = context.Cards
                .Include(x => x.MarkdownFile)
                .Where(x => x.ApplicationUserId == userId && x.DeletedAt == null)
                .ToList();

            if (usersCards.Count == 0)
                return Results.NotFound("No cards found");

            var cards = usersCards.Select(x => new CardResponse(x.Id, x.Title, x.Content, x.UsageType, x.MarkdownFile!.FileName, x.MarkdownFileId, x.CreatedAt, x.Heading, x.UpdatedAt)).ToArray();
            return Results.Ok(cards);
        }

        public static IResult Delete(HttpContext httpContext, Guid? id, ApplicationDbContext context)
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var card = context.Cards
                .FirstOrDefault(x => x.ApplicationUserId == userId && x.Id == id);
            if (card == null)
                return Results.NotFound("Card not found");

            context.Cards.Remove(card);
            context.SaveChanges();

            return Results.Ok();
        }

        public static IResult Post(HttpContext httpContext, CardRequest request, ApplicationDbContext context)
        {
            var card = new Card
            {
                Id = Guid.CreateVersion7(),
                Title = request.Title,
                Content = request.Content,
                CreatedAt = DateTime.UtcNow,
                ApplicationUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new Exception("User not found"),
                MarkdownFileId = request.MdFileId,
                UsageType = request.UsageType,
                Heading = request.Heading,
                HeadingLineNr = request.HeadingLineNr,
            };

            context.Cards.Add(card);
            context.SaveChanges();

            return Results.Ok(card.Id);
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CardUsageType
    {
        EntireFile,
        PartialFile
    }

}