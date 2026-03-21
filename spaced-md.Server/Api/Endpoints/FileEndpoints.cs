using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpacedMd.Server.Application.Dtos;
using SpacedMd.Server.Application.Services;
using SpacedMd.Server.Domain.Entities;
using SpacedMd.Server.Infrastructure.Data;

namespace SpacedMd.Server.Api.Endpoints;

public static class FileEndpoints
{
    private const long MaxFileSize = 1_048_576; // 1MB
    private const int MaxBulkFiles = 20;

    public static void MapFileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/files").RequireAuthorization();

        group.MapPost("/", Upload).DisableAntiforgery();
        group.MapPost("/bulk", BulkUpload).DisableAntiforgery();
        group.MapGet("/", List);
        group.MapGet("/{id:guid}", GetById);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/preview-update", PreviewUpdate).DisableAntiforgery();
        group.MapPost("/{id:guid}/update", ConfirmUpdate).DisableAntiforgery();
    }

    private static async Task<IResult> Upload(
        IFormFile file,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        // Basic validation (same as SaveFile)
        var fileName = Path.GetFileName(file.FileName);
        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Only .md files are accepted."]
            });

        if (file.Length > MaxFileSize)
            return Results.UnprocessableEntity(new { error = "file_too_large", message = "File exceeds 1MB limit." });

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();

        // Check for existing file (upsert)
        var existing = await db.MarkdownFiles
            .Include(f => f.Headings)
            .FirstOrDefaultAsync(f => f.UserId == user.Id && f.FileName == fileName);

        if (existing is not null)
        {
            // Upsert: update existing file
            var existingCards = await db.Cards
                .Where(c => c.FileId == existing.Id && c.UserId == user.Id)
                .ToListAsync();

            var comparison = FileComparer.Compare(content, existingCards);
            var orphanedCards = comparison.OrphanedCards
                .Select(c => new OrphanedCardPreviewDto(c.CardId, c.Front, c.SourceHeading))
                .ToList();

            existing.Content = content;
            existing.SizeBytes = file.Length;
            existing.UploadedAt = DateTimeOffset.UtcNow;

            // Re-extract headings
            db.FileHeadings.RemoveRange(existing.Headings);
            var newHeadings = HeadingExtractor.Extract(content);
            foreach (var (level, text, sortOrder) in newHeadings)
            {
                db.FileHeadings.Add(new FileHeading
                {
                    Id = Guid.NewGuid(),
                    FileId = existing.Id,
                    Level = level,
                    Text = text,
                    SortOrder = sortOrder
                });
            }

            await db.SaveChangesAsync();

            var headings = await db.FileHeadings
                .Where(h => h.FileId == existing.Id)
                .OrderBy(h => h.SortOrder)
                .Select(h => new FileHeadingDto(h.Level, h.Text))
                .ToListAsync();

            return Results.Ok(new FileUpsertResponseDto(
                existing.Id, existing.FileName, existing.SizeBytes,
                existing.UploadedAt, true, headings, orphanedCards));
        }

        // New file: delegate to SaveFile (used by both Upload and BulkUpload)
        var (result, error) = await SaveFile(file, user.Id, db);
        if (error is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = [error]
            });

        await db.SaveChangesAsync();

        var responseHeadings = result!.Headings.OrderBy(h => h.SortOrder)
            .Select(h => new FileHeadingDto(h.Level, h.Text)).ToList();

        return Results.Created($"/api/files/{result.Id}", new FileUpsertResponseDto(
            result.Id, result.FileName, result.SizeBytes,
            result.UploadedAt, false, responseHeadings, []));
    }

    private static async Task<IResult> BulkUpload(
        HttpRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        IFormFileCollection files;
        try
        {
            files = request.Form.Files;
        }
        catch
        {
            return Results.BadRequest();
        }

        if (files.Count > MaxBulkFiles)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["files"] = [$"Maximum {MaxBulkFiles} files per upload."]
            });

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<BulkUploadResultDto>();
        foreach (var file in files)
        {
            var name = Path.GetFileName(file.FileName);
            if (!seenNames.Add(name))
            {
                results.Add(new BulkUploadResultDto(name, false, null, "Duplicate file in batch."));
                continue;
            }

            var (saved, error) = await SaveFile(file, user.Id, db);
            if (error is not null)
            {
                results.Add(new BulkUploadResultDto(name, false, null, error));
            }
            else
            {
                results.Add(new BulkUploadResultDto(saved!.FileName, true, saved.Id, null));
            }
        }

        await db.SaveChangesAsync();
        return Results.Ok(results);
    }

    private static async Task<IResult> List(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var files = await db.MarkdownFiles
            .Where(f => f.UserId == user.Id)
            .OrderByDescending(f => f.UploadedAt)
            .Select(f => new FileListItemDto(
                f.Id,
                f.FileName,
                f.SizeBytes,
                f.UploadedAt,
                db.Cards.Count(c => c.FileId == f.Id && c.DeletedAt == null),
                f.Headings.OrderBy(h => h.SortOrder)
                    .Select(h => new FileHeadingDto(h.Level, h.Text)).ToList()))
            .ToListAsync();

        return Results.Ok(files);
    }

    private static async Task<IResult> GetById(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var file = await db.MarkdownFiles
            .Include(f => f.Headings.OrderBy(h => h.SortOrder))
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == user.Id);

        if (file is null) return Results.NotFound();

        var cardCount = await db.Cards.CountAsync(c => c.FileId == id && c.UserId == user.Id && c.DeletedAt == null);

        return Results.Ok(new FileDetailDto(
            file.Id,
            file.FileName,
            file.SizeBytes,
            file.UploadedAt,
            cardCount,
            file.Content,
            file.Headings.Select(h => new FileHeadingDto(h.Level, h.Text)).ToList()));
    }

    private static async Task<IResult> Delete(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var file = await db.MarkdownFiles
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == user.Id);

        if (file is null) return Results.NotFound();

        db.MarkdownFiles.Remove(file);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> PreviewUpdate(
        IFormFile file,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var fileName = Path.GetFileName(file.FileName);

        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Only .md files are accepted."]
            });

        if (file.Length > MaxFileSize)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["File exceeds 1MB limit."]
            });

        var existing = await db.MarkdownFiles
            .FirstOrDefaultAsync(f => f.UserId == user.Id && f.FileName == fileName);

        if (existing is null) return Results.NotFound();

        using var reader = new StreamReader(file.OpenReadStream());
        var newContent = await reader.ReadToEndAsync();

        var cards = await db.Cards
            .Where(c => c.FileId == existing.Id && c.UserId == user.Id)
            .ToListAsync();

        var result = FileComparer.Compare(newContent, cards);

        return Results.Ok(new FileUpdatePreviewDto(
            existing.Id,
            existing.FileName,
            result.UpdatedCards.Select(c => new UpdatedCardPreviewDto(c.CardId, c.Front, c.OldBack, c.NewBack)).ToList(),
            result.OrphanedCards.Select(c => new OrphanedCardPreviewDto(c.CardId, c.Front, c.SourceHeading)).ToList(),
            result.UnchangedCardIds.Count,
            result.NewSections.Select(s => new NewSectionPreviewDto(s.Heading, s.HasMarkers)).ToList()));
    }

    private static async Task<IResult> ConfirmUpdate(
        Guid id,
        IFormFile file,
        HttpRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);

        // Parse deleteCardIds from form fields manually (FromForm doesn't bind well with IFormFile in minimal APIs)
        var deleteCardIds = request.Form["deleteCardIds"]
            .SelectMany(v => v?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [])
            .Select(v => Guid.TryParse(v, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();
        if (user is null) return Results.Unauthorized();

        var fileName = Path.GetFileName(file.FileName);

        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Only .md files are accepted."]
            });

        if (file.Length > MaxFileSize)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["File exceeds 1MB limit."]
            });

        var existing = await db.MarkdownFiles
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == user.Id);

        if (existing is null) return Results.NotFound();

        using var reader = new StreamReader(file.OpenReadStream());
        var newContent = await reader.ReadToEndAsync();

        // Validate deleteCardIds ownership
        if (deleteCardIds.Count > 0)
        {
            var validCount = await db.Cards
                .CountAsync(c => deleteCardIds.Contains(c.Id) && c.UserId == user.Id && c.FileId == id);
            if (validCount != deleteCardIds.Count)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["deleteCardIds"] = ["One or more card IDs are invalid."]
                });
        }

        // Load cards bypassing global query filter to avoid concurrency issues when setting DeletedAt
        var cards = await db.Cards
            .IgnoreQueryFilters()
            .Where(c => c.FileId == existing.Id && c.UserId == user.Id && c.DeletedAt == null)
            .ToListAsync();

        var comparison = FileComparer.Compare(newContent, cards);

        // Capture old derived front BEFORE overwriting content
        var oldFirstH1 = ContentExtractor.GetFirstH1(existing.Content);

        // 1. Update file content
        existing.Content = newContent;
        existing.SizeBytes = file.Length;
        existing.UploadedAt = DateTimeOffset.UtcNow;

        // 2. Re-extract headings
        var oldHeadings = await db.FileHeadings.Where(h => h.FileId == existing.Id).ToListAsync();
        db.FileHeadings.RemoveRange(oldHeadings);
        foreach (var h in HeadingExtractor.Extract(newContent))
        {
            db.FileHeadings.Add(new FileHeading
            {
                Id = Guid.NewGuid(),
                FileId = existing.Id,
                Level = h.Level,
                Text = h.Text,
                SortOrder = h.SortOrder,
            });
        }

        // 3. Update card backs
        var updatedCount = 0;
        var newStripped = ContentExtractor.StripFrontmatter(newContent);
        var newFirstH1 = ContentExtractor.GetFirstH1(newContent);

        foreach (var card in cards)
        {
            if (card.CardType == "file")
            {
                var (_, cleanedNew) = ContentExtractor.ParseMarkers(newStripped);
                if (card.Back != cleanedNew)
                {
                    card.Back = cleanedNew;
                    var oldDerived = oldFirstH1 ?? existing.FileName;
                    if (card.Front == oldDerived)
                        card.Front = newFirstH1 ?? existing.FileName;
                    updatedCount++;
                }
            }
            else if (card.CardType == "section" && card.SourceHeading is not null)
            {
                var section = ContentExtractor.ExtractSection(newContent, card.SourceHeading);
                if (section is not null)
                {
                    var (_, cleanedSection) = ContentExtractor.ParseMarkers(section);
                    if (card.Back != cleanedSection)
                    {
                        card.Back = cleanedSection;
                        updatedCount++;
                    }
                }
            }
        }

        // 4. Soft-delete requested cards
        var deletedCount = 0;
        foreach (var card in cards.Where(c => deleteCardIds.Contains(c.Id)))
        {
            card.DeletedAt = DateTimeOffset.UtcNow;
            deletedCount++;
        }

        // 5. Unlink remaining orphaned cards
        var orphanedIds = comparison.OrphanedCards.Select(c => c.CardId).ToHashSet();
        var orphanedCount = 0;
        foreach (var card in cards.Where(c => orphanedIds.Contains(c.Id) && !deleteCardIds.Contains(c.Id)))
        {
            card.FileId = null;
            orphanedCount++;
        }

        await db.SaveChangesAsync();

        return Results.Ok(new FileUpdateResultDto(updatedCount, deletedCount, orphanedCount));
    }

    private static async Task<(MarkdownFile? File, string? Error)> SaveFile(
        IFormFile formFile, string userId, AppDbContext db)
    {
        var fileName = Path.GetFileName(formFile.FileName);

        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return (null, "Only .md files are accepted.");

        if (formFile.Length > MaxFileSize)
            return (null, "File exceeds 1MB limit.");

        var exists = await db.MarkdownFiles
            .AnyAsync(f => f.UserId == userId && f.FileName == fileName);
        if (exists)
            return (null, $"A file named '{fileName}' already exists.");

        using var reader = new StreamReader(formFile.OpenReadStream());
        var content = await reader.ReadToEndAsync();

        var entity = new MarkdownFile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = fileName,
            Content = content,
            SizeBytes = formFile.Length,
            UploadedAt = DateTimeOffset.UtcNow,
        };

        var headings = HeadingExtractor.Extract(content);
        entity.Headings = headings.Select(h => new FileHeading
        {
            Id = Guid.NewGuid(),
            Level = h.Level,
            Text = h.Text,
            SortOrder = h.SortOrder,
        }).ToList();

        db.MarkdownFiles.Add(entity);
        return (entity, null);
    }

    private static FileListItemDto ToListItem(MarkdownFile f) =>
        new(f.Id, f.FileName, f.SizeBytes, f.UploadedAt, 0,
            f.Headings.OrderBy(h => h.SortOrder)
                .Select(h => new FileHeadingDto(h.Level, h.Text)).ToList());
}
