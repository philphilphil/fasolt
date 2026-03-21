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

        var files = request.Form.Files;

        if (files.Count > MaxBulkFiles)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["files"] = [$"Maximum {MaxBulkFiles} files per upload."]
            });

        var results = new List<BulkUploadResultDto>();
        foreach (var file in files)
        {
            var (saved, error) = await SaveFile(file, user.Id, db);
            if (error is not null)
            {
                results.Add(new BulkUploadResultDto(file.FileName, false, null, error));
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
                0, // cardCount — wired in Epic 3
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

        return Results.Ok(new FileDetailDto(
            file.Id,
            file.FileName,
            file.SizeBytes,
            file.UploadedAt,
            0, // cardCount — wired in Epic 3
            file.Content,
            file.Headings.OrderBy(h => h.SortOrder)
                .Select(h => new FileHeadingDto(h.Level, h.Text)).ToList()));
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
        if (!formFile.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return (null, "Only .md files are accepted.");

        if (formFile.Length > MaxFileSize)
            return (null, "File exceeds 1MB limit.");

        var exists = await db.MarkdownFiles
            .AnyAsync(f => f.UserId == userId && f.FileName == formFile.FileName);
        if (exists)
            return (null, $"A file named '{formFile.FileName}' already exists.");

        using var reader = new StreamReader(formFile.OpenReadStream());
        var content = await reader.ReadToEndAsync();

        var entity = new MarkdownFile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = formFile.FileName,
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
