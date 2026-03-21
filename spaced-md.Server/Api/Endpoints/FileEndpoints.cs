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
    }

    private static async Task<IResult> Upload(
        IFormFile file,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var (result, error) = await SaveFile(file, user.Id, db);
        if (error is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = [error]
            });

        await db.SaveChangesAsync();
        return Results.Created($"/api/files/{result!.Id}", ToListItem(result));
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
