# File Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement markdown file upload, listing, preview, deletion, heading browsing, and bulk upload (US-2.1 through US-2.6).

**Architecture:** Add `MarkdownFile` and `FileHeading` domain entities with EF Core. Create `/api/files` endpoints following the existing `AccountEndpoints` pattern. Replace mock data in the Vue files store with API calls. Add `FileDetailView` for markdown preview with heading navigation. Use `markdown-it` for rendering.

**Tech Stack:** .NET 10, EF Core + Npgsql, Vue 3, TypeScript, Pinia, markdown-it, shadcn-vue, Tailwind CSS 3

---

## File Map

### Backend — New Files
- `spaced-md.Server/Domain/Entities/MarkdownFile.cs` — MarkdownFile entity with navigation to headings
- `spaced-md.Server/Domain/Entities/FileHeading.cs` — FileHeading entity
- `spaced-md.Server/Application/Dtos/FileDtos.cs` — Request/response DTOs for file endpoints
- `spaced-md.Server/Application/Services/HeadingExtractor.cs` — Extracts headings from markdown content
- `spaced-md.Server/Api/Endpoints/FileEndpoints.cs` — /api/files endpoints (upload, list, detail, delete, bulk)

### Backend — Modified Files
- `spaced-md.Server/Infrastructure/Data/AppDbContext.cs` — Add DbSets and configure entity relationships
- `spaced-md.Server/Program.cs` — Register `MapFileEndpoints()`

### Frontend — New Files
- `spaced-md.client/src/composables/useMarkdown.ts` — Shared markdown-it instance with image placeholder override
- `spaced-md.client/src/views/FileDetailView.vue` — Markdown preview with heading tree and source toggle

### Frontend — Modified Files
- `spaced-md.client/src/api/client.ts` — Add `apiUpload()` helper for FormData uploads
- `spaced-md.client/src/types/index.ts` — Update `MarkdownFile` type to match API response, add `FileDetail`
- `spaced-md.client/src/stores/files.ts` — Replace mock data with API-backed methods
- `spaced-md.client/src/views/FilesView.vue` — Wire upload zone, sorting, delete dialog, bulk upload UI
- `spaced-md.client/src/router/index.ts` — Add `/files/:id` route

---

## Task 1: Domain Entities

**Files:**
- Create: `spaced-md.Server/Domain/Entities/MarkdownFile.cs`
- Create: `spaced-md.Server/Domain/Entities/FileHeading.cs`

- [ ] **Step 1: Create MarkdownFile entity**

```csharp
// spaced-md.Server/Domain/Entities/MarkdownFile.cs
namespace SpacedMd.Server.Domain.Entities;

public class MarkdownFile
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string Content { get; set; } = default!;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public List<FileHeading> Headings { get; set; } = [];
}
```

- [ ] **Step 2: Create FileHeading entity**

```csharp
// spaced-md.Server/Domain/Entities/FileHeading.cs
namespace SpacedMd.Server.Domain.Entities;

public class FileHeading
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public MarkdownFile File { get; set; } = default!;
    public int Level { get; set; }
    public string Text { get; set; } = default!;
    public int SortOrder { get; set; }
}
```

- [ ] **Step 3: Commit**

```bash
git add spaced-md.Server/Domain/Entities/MarkdownFile.cs spaced-md.Server/Domain/Entities/FileHeading.cs
git commit -m "feat: add MarkdownFile and FileHeading domain entities"
```

---

## Task 2: DbContext and Migration

**Files:**
- Modify: `spaced-md.Server/Infrastructure/Data/AppDbContext.cs`

- [ ] **Step 1: Add DbSets and configure relationships**

Add to `AppDbContext`:

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SpacedMd.Server.Domain.Entities;

namespace SpacedMd.Server.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MarkdownFile> MarkdownFiles => Set<MarkdownFile>();
    public DbSet<FileHeading> FileHeadings => Set<FileHeading>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MarkdownFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.Content).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.FileName }).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FileHeading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.File).WithMany(f => f.Headings).HasForeignKey(e => e.FileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.FileId);
        });
    }
}
```

- [ ] **Step 2: Create migration**

Run from repo root:

```bash
dotnet ef migrations add AddMarkdownFiles --project spaced-md.Server
```

Verify the generated migration creates `MarkdownFiles` and `FileHeadings` tables with the correct constraints.

- [ ] **Step 3: Apply migration**

Start Postgres if not running, then:

```bash
docker compose up -d
dotnet ef database update --project spaced-md.Server
```

- [ ] **Step 4: Commit**

```bash
git add spaced-md.Server/Infrastructure/Data/AppDbContext.cs spaced-md.Server/Infrastructure/Data/Migrations/
git commit -m "feat: add MarkdownFiles and FileHeadings schema with migration"
```

---

## Task 3: HeadingExtractor Service

**Files:**
- Create: `spaced-md.Server/Application/Services/HeadingExtractor.cs`

- [ ] **Step 1: Create HeadingExtractor**

```csharp
// spaced-md.Server/Application/Services/HeadingExtractor.cs
using System.Text.RegularExpressions;

namespace SpacedMd.Server.Application.Services;

public static partial class HeadingExtractor
{
    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingPattern();

    public static List<(int Level, string Text, int SortOrder)> Extract(string markdown)
    {
        var headings = new List<(int Level, string Text, int SortOrder)>();
        var inCodeFence = false;
        var sortOrder = 0;

        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.StartsWith("```"))
            {
                inCodeFence = !inCodeFence;
                continue;
            }

            if (inCodeFence) continue;

            var match = HeadingPattern().Match(trimmed);
            if (match.Success)
            {
                headings.Add((match.Groups[1].Value.Length, match.Groups[2].Value.Trim(), sortOrder++));
            }
        }

        return headings;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add spaced-md.Server/Application/Services/HeadingExtractor.cs
git commit -m "feat: add HeadingExtractor service for markdown heading parsing"
```

---

## Task 4: File DTOs

**Files:**
- Create: `spaced-md.Server/Application/Dtos/FileDtos.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// spaced-md.Server/Application/Dtos/FileDtos.cs
namespace SpacedMd.Server.Application.Dtos;

public record FileHeadingDto(int Level, string Text);

public record FileListItemDto(
    Guid Id,
    string FileName,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    int CardCount,
    List<FileHeadingDto> Headings);

public record FileDetailDto(
    Guid Id,
    string FileName,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    int CardCount,
    string Content,
    List<FileHeadingDto> Headings);

public record BulkUploadResultDto(string FileName, bool Success, Guid? Id, string? Error);
```

- [ ] **Step 2: Commit**

```bash
git add spaced-md.Server/Application/Dtos/FileDtos.cs
git commit -m "feat: add file management DTOs"
```

---

## Task 5: File API Endpoints

**Files:**
- Create: `spaced-md.Server/Api/Endpoints/FileEndpoints.cs`
- Modify: `spaced-md.Server/Program.cs`

- [ ] **Step 1: Create FileEndpoints**

```csharp
// spaced-md.Server/Api/Endpoints/FileEndpoints.cs
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
            .Include(f => f.Headings.OrderBy(h => h.SortOrder))
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
```

- [ ] **Step 2: Register endpoints in Program.cs**

Add after `app.MapAccountEndpoints();`:

```csharp
app.MapFileEndpoints();
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build spaced-md.Server
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add spaced-md.Server/Api/Endpoints/FileEndpoints.cs spaced-md.Server/Program.cs
git commit -m "feat: add /api/files endpoints (upload, list, detail, delete, bulk)"
```

---

## Task 6: Frontend — API Upload Helper and Types

**Files:**
- Modify: `spaced-md.client/src/api/client.ts`
- Modify: `spaced-md.client/src/types/index.ts`

- [ ] **Step 1: Add apiUpload helper**

Add to the bottom of `spaced-md.client/src/api/client.ts`:

```typescript
export async function apiUpload<T>(path: string, formData: FormData): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    credentials: 'include',
    body: formData,
  })

  if (!response.ok) {
    let errors: Record<string, string[]> = {}
    try {
      const body = await response.json()
      if (body.errors) {
        errors = body.errors
      }
    } catch {
      // No JSON body
    }
    throw { status: response.status, errors } as ApiError
  }

  const text = await response.text()
  if (!text) return undefined as T

  return JSON.parse(text)
}
```

- [ ] **Step 2: Update types**

Replace the `MarkdownFile` and `FileHeading` interfaces in `spaced-md.client/src/types/index.ts`:

```typescript
export interface MarkdownFile {
  id: string
  fileName: string
  sizeBytes: number
  uploadedAt: string
  cardCount: number
  headings: FileHeading[]
}

export interface FileHeading {
  level: number
  text: string
}

export interface FileDetail extends MarkdownFile {
  content: string
}

export interface BulkUploadResult {
  fileName: string
  success: boolean
  id: string | null
  error: string | null
}
```

- [ ] **Step 3: Commit**

```bash
git add spaced-md.client/src/api/client.ts spaced-md.client/src/types/index.ts
git commit -m "feat: add apiUpload helper and update file types for API"
```

---

## Task 7: Frontend — Files Store

**Files:**
- Modify: `spaced-md.client/src/stores/files.ts`

- [ ] **Step 1: Replace mock store with API-backed store**

```typescript
// spaced-md.client/src/stores/files.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { MarkdownFile, FileDetail, BulkUploadResult } from '@/types'
import { apiFetch, apiUpload } from '@/api/client'

export const useFilesStore = defineStore('files', () => {
  const files = ref<MarkdownFile[]>([])
  const loading = ref(false)

  async function fetchFiles() {
    loading.value = true
    try {
      files.value = await apiFetch<MarkdownFile[]>('/files')
    } finally {
      loading.value = false
    }
  }

  async function uploadFile(file: File): Promise<MarkdownFile> {
    const formData = new FormData()
    formData.append('file', file)
    const result = await apiUpload<MarkdownFile>('/files', formData)
    await fetchFiles()
    return result
  }

  async function uploadFiles(fileList: File[]): Promise<BulkUploadResult[]> {
    const formData = new FormData()
    for (const file of fileList) {
      formData.append('files', file)
    }
    const results = await apiUpload<BulkUploadResult[]>('/files/bulk', formData)
    await fetchFiles()
    return results
  }

  async function deleteFile(id: string) {
    await apiFetch(`/files/${id}`, { method: 'DELETE' })
    files.value = files.value.filter(f => f.id !== id)
  }

  async function getFileContent(id: string): Promise<FileDetail> {
    return apiFetch<FileDetail>(`/files/${id}`)
  }

  return { files, loading, fetchFiles, uploadFile, uploadFiles, deleteFile, getFileContent }
})
```

- [ ] **Step 2: Commit**

```bash
git add spaced-md.client/src/stores/files.ts
git commit -m "feat: replace mock files store with API-backed store"
```

---

## Task 8: Frontend — useMarkdown Composable

**Files:**
- Create: `spaced-md.client/src/composables/useMarkdown.ts`

- [ ] **Step 1: Install markdown-it**

```bash
cd spaced-md.client && npm install markdown-it && npm install -D @types/markdown-it
```

- [ ] **Step 2: Create composable**

```typescript
// spaced-md.client/src/composables/useMarkdown.ts
import MarkdownIt from 'markdown-it'

const md = new MarkdownIt({
  html: false,
  linkify: true,
  typographer: false,
})

// Override image rendering to show alt-text placeholder
md.renderer.rules.image = (tokens, idx) => {
  const alt = tokens[idx].content || tokens[idx].children?.reduce((s, t) => s + t.content, '') || 'image'
  return `<span class="inline-flex items-center gap-1 rounded bg-muted px-2 py-1 text-xs text-muted-foreground">[${alt}]</span>`
}

export function useMarkdown() {
  function render(content: string): string {
    return md.render(content)
  }

  function stripFrontmatter(content: string): string {
    if (!content.startsWith('---\n') && !content.startsWith('---\r\n')) return content
    const end = content.indexOf('\n---', 3)
    if (end === -1) return content
    // Skip past the closing --- and the newline after it
    const afterFrontmatter = content.indexOf('\n', end + 4)
    if (afterFrontmatter === -1) return ''
    return content.slice(afterFrontmatter + 1)
  }

  return { render, stripFrontmatter }
}
```

- [ ] **Step 3: Commit**

```bash
git add spaced-md.client/src/composables/useMarkdown.ts spaced-md.client/package.json spaced-md.client/package-lock.json
git commit -m "feat: add useMarkdown composable with markdown-it"
```

---

## Task 9: Frontend — FilesView.vue (Upload, Sort, Delete)

**Files:**
- Modify: `spaced-md.client/src/views/FilesView.vue`

- [ ] **Step 1: Rewrite FilesView with upload zone, sorting, and delete**

```vue
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useFilesStore } from '@/stores/files'
import { isApiError } from '@/api/client'
import type { BulkUploadResult } from '@/types'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const router = useRouter()
const files = useFilesStore()
const expandedId = ref<string | null>(null)
const sortBy = ref<'name' | 'date' | 'cards'>('date')
const dragging = ref(false)
const uploading = ref(false)
const uploadProgress = ref('')
const uploadError = ref('')
const uploadResults = ref<BulkUploadResult[] | null>(null)
const deleteTarget = ref<{ id: string; name: string } | null>(null)
const fileInput = ref<HTMLInputElement>()

onMounted(() => files.fetchFiles())

const sortedFiles = computed(() => {
  const sorted = [...files.files]
  switch (sortBy.value) {
    case 'name': sorted.sort((a, b) => a.fileName.localeCompare(b.fileName)); break
    case 'date': sorted.sort((a, b) => new Date(b.uploadedAt).getTime() - new Date(a.uploadedAt).getTime()); break
    case 'cards': sorted.sort((a, b) => b.cardCount - a.cardCount); break
  }
  return sorted
})

function toggle(id: string) {
  expandedId.value = expandedId.value === id ? null : id
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  return `${(bytes / 1024).toFixed(1)} KB`
}

function formatDate(date: string): string {
  return new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

function validateFiles(fileList: File[]): File[] {
  const valid: File[] = []
  const errors: string[] = []
  for (const f of fileList) {
    if (!f.name.endsWith('.md')) {
      errors.push(`${f.name}: not a .md file`)
    } else if (f.size > 1_048_576) {
      errors.push(`${f.name}: exceeds 1MB`)
    } else {
      valid.push(f)
    }
  }
  if (errors.length) uploadError.value = errors.join('. ')
  return valid
}

async function handleFiles(fileList: File[]) {
  uploadError.value = ''
  uploadResults.value = null
  const valid = validateFiles(fileList)
  if (!valid.length) return

  uploading.value = true
  try {
    if (valid.length === 1) {
      uploadProgress.value = `Uploading ${valid[0].name}...`
      await files.uploadFile(valid[0])
      uploadProgress.value = `Uploaded ${valid[0].name}`
    } else {
      uploadProgress.value = `Uploading ${valid.length} files...`
      const results = await files.uploadFiles(valid)
      uploadResults.value = results
      const succeeded = results.filter(r => r.success).length
      uploadProgress.value = `${succeeded}/${results.length} files uploaded`
      const failures = results.filter(r => !r.success)
      if (failures.length) {
        uploadError.value = failures.map(f => `${f.fileName}: ${f.error}`).join('. ')
      }
    }
  } catch (err) {
    if (isApiError(err)) {
      uploadError.value = Object.values(err.errors).flat().join('. ')
    } else {
      uploadError.value = 'Upload failed.'
    }
    uploadProgress.value = ''
  } finally {
    uploading.value = false
  }
}

function onDrop(e: DragEvent) {
  dragging.value = false
  const droppedFiles = Array.from(e.dataTransfer?.files ?? [])
  if (droppedFiles.length) handleFiles(droppedFiles)
}

function onFileSelect(e: Event) {
  const input = e.target as HTMLInputElement
  const selected = Array.from(input.files ?? [])
  if (selected.length) handleFiles(selected)
  input.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  await files.deleteFile(deleteTarget.value.id)
  deleteTarget.value = null
}
</script>

<template>
  <div class="space-y-4">
    <!-- Upload zone -->
    <div
      class="flex flex-col items-center justify-center gap-2 rounded-lg border-2 border-dashed p-8 text-center text-sm transition-colors"
      :class="dragging ? 'border-primary bg-primary/5 text-primary' : 'border-border text-muted-foreground'"
      @dragover.prevent="dragging = true"
      @dragleave.prevent="dragging = false"
      @drop.prevent="onDrop"
      @click="fileInput?.click()"
    >
      <input ref="fileInput" type="file" accept=".md" multiple hidden @change="onFileSelect" />
      <template v-if="uploading">
        <span>{{ uploadProgress }}</span>
      </template>
      <template v-else>
        <span>Drop .md files here or click to upload</span>
      </template>
    </div>

    <!-- Upload feedback -->
    <div v-if="uploadProgress && !uploading" class="text-xs text-green-600 dark:text-green-400">
      {{ uploadProgress }}
    </div>
    <div v-if="uploadError" class="text-xs text-destructive">
      {{ uploadError }}
    </div>

    <!-- Sort controls -->
    <div v-if="files.files.length > 0" class="flex gap-1">
      <Button
        v-for="s in [{ key: 'date', label: 'Date' }, { key: 'name', label: 'Name' }, { key: 'cards', label: 'Cards' }]"
        :key="s.key"
        variant="ghost"
        size="sm"
        class="h-7 text-[10px]"
        :class="sortBy === s.key ? 'bg-accent' : ''"
        @click="sortBy = s.key as typeof sortBy"
      >
        {{ s.label }}
      </Button>
    </div>

    <!-- File table -->
    <Table v-if="files.files.length > 0">
      <TableHeader>
        <TableRow class="text-[10px] uppercase tracking-wider text-muted-foreground hover:bg-transparent">
          <TableHead class="h-8">File</TableHead>
          <TableHead class="h-8">Cards</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Uploaded</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Size</TableHead>
          <TableHead class="h-8 w-12"></TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <template v-for="file in sortedFiles" :key="file.id">
          <TableRow class="cursor-pointer text-xs" @click="toggle(file.id)">
            <TableCell class="font-mono font-medium text-foreground">
              <router-link
                :to="`/files/${file.id}`"
                class="hover:underline"
                @click.stop
              >
                {{ file.fileName }}
              </router-link>
            </TableCell>
            <TableCell class="font-mono text-muted-foreground">{{ file.cardCount }}</TableCell>
            <TableCell class="hidden text-muted-foreground sm:table-cell">{{ formatDate(file.uploadedAt) }}</TableCell>
            <TableCell class="hidden text-muted-foreground sm:table-cell">{{ formatSize(file.sizeBytes) }}</TableCell>
            <TableCell>
              <Button
                variant="ghost"
                size="sm"
                class="h-6 w-6 p-0 text-muted-foreground hover:text-destructive"
                @click.stop="deleteTarget = { id: file.id, name: file.fileName }"
              >
                &times;
              </Button>
            </TableCell>
          </TableRow>
          <TableRow v-if="expandedId === file.id" class="hover:bg-transparent">
            <TableCell :colspan="5" class="p-0">
              <div class="space-y-1 border-t border-border px-4 py-3">
                <div
                  v-for="heading in file.headings"
                  :key="heading.text"
                  class="flex items-center justify-between text-xs"
                >
                  <span class="text-muted-foreground" :style="{ paddingLeft: `${(heading.level - 1) * 12}px` }">
                    {{ '#'.repeat(heading.level) }} {{ heading.text }}
                  </span>
                  <Button variant="ghost" size="sm" class="h-6 text-[10px] opacity-50 cursor-not-allowed" disabled>
                    Create cards
                  </Button>
                </div>
              </div>
            </TableCell>
          </TableRow>
        </template>
      </TableBody>
    </Table>

    <!-- Empty state -->
    <div v-if="!files.loading && files.files.length === 0" class="py-12 text-center text-sm text-muted-foreground">
      No files uploaded yet. Drop a .md file above to get started.
    </div>

    <!-- Delete confirmation dialog -->
    <Dialog :open="!!deleteTarget" @update:open="deleteTarget = null">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete file</DialogTitle>
          <DialogDescription>
            Are you sure you want to delete {{ deleteTarget?.name }}? This cannot be undone.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" @click="deleteTarget = null">Cancel</Button>
          <Button variant="destructive" @click="confirmDelete">Delete</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add spaced-md.client/src/views/FilesView.vue
git commit -m "feat: wire FilesView with upload, sort, delete, and bulk upload"
```

---

## Task 10: Frontend — FileDetailView and Route

**Files:**
- Create: `spaced-md.client/src/views/FileDetailView.vue`
- Modify: `spaced-md.client/src/router/index.ts`

- [ ] **Step 1: Create FileDetailView**

```vue
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useFilesStore } from '@/stores/files'
import { useMarkdown } from '@/composables/useMarkdown'
import type { FileDetail } from '@/types'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'

const route = useRoute()
const router = useRouter()
const files = useFilesStore()
const { render, stripFrontmatter } = useMarkdown()

const file = ref<FileDetail | null>(null)
const loading = ref(true)
const showSource = ref(false)

onMounted(async () => {
  try {
    file.value = await files.getFileContent(route.params.id as string)
  } catch {
    router.replace('/files')
  } finally {
    loading.value = false
  }
})

const strippedContent = computed(() => {
  if (!file.value) return ''
  return stripFrontmatter(file.value.content)
})

const renderedHtml = computed(() => render(strippedContent.value))

function scrollToHeading(text: string) {
  // markdown-it generates heading IDs from text — find the heading element by text content
  const headings = document.querySelectorAll('.markdown-body h1, .markdown-body h2, .markdown-body h3, .markdown-body h4, .markdown-body h5, .markdown-body h6')
  for (const el of headings) {
    if (el.textContent?.trim() === text) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' })
      return
    }
  }
}
</script>

<template>
  <div v-if="loading" class="py-12 text-center text-sm text-muted-foreground">Loading...</div>

  <div v-else-if="file" class="space-y-4">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <Button variant="ghost" size="sm" class="h-7 text-xs" @click="router.push('/files')">
          &larr; Files
        </Button>
        <span class="font-mono text-sm font-medium">{{ file.fileName }}</span>
      </div>
      <Button variant="outline" size="sm" class="h-7 text-xs" @click="showSource = !showSource">
        {{ showSource ? 'Preview' : 'Source' }}
      </Button>
    </div>

    <div class="flex gap-4">
      <!-- Heading tree sidebar -->
      <aside v-if="file.headings.length > 0" class="hidden w-56 shrink-0 md:block">
        <div class="sticky top-4 space-y-1">
          <div class="text-[10px] uppercase tracking-wider text-muted-foreground mb-2">Sections</div>
          <div
            v-for="heading in file.headings"
            :key="heading.text"
            class="group flex items-center justify-between text-xs"
          >
            <button
              class="text-left text-muted-foreground hover:text-foreground transition-colors truncate"
              :style="{ paddingLeft: `${(heading.level - 1) * 12}px` }"
              @click="scrollToHeading(heading.text)"
            >
              {{ heading.text }}
            </button>
            <Button
              variant="ghost"
              size="sm"
              class="h-5 text-[10px] opacity-0 group-hover:opacity-50 cursor-not-allowed shrink-0"
              disabled
            >
              Create cards
            </Button>
          </div>
        </div>
      </aside>

      <!-- Content -->
      <div class="min-w-0 flex-1">
        <div v-if="showSource" class="whitespace-pre-wrap rounded-lg border border-border bg-muted/50 p-4 font-mono text-xs">{{ strippedContent }}</div>
        <div v-else class="markdown-body prose prose-sm dark:prose-invert max-w-none" v-html="renderedHtml" />
      </div>
    </div>
  </div>
</template>
```

- [ ] **Step 2: Add route**

In `spaced-md.client/src/router/index.ts`, add after the `/files` route:

```typescript
{ path: '/files/:id', name: 'file-detail', component: () => import('@/views/FileDetailView.vue') },
```

- [ ] **Step 3: Commit**

```bash
git add spaced-md.client/src/views/FileDetailView.vue spaced-md.client/src/router/index.ts
git commit -m "feat: add FileDetailView with markdown preview and heading navigation"
```

---

## Task 11: Playwright Smoke Test

**Files:**
- No new files — use Playwright MCP

- [ ] **Step 1: Start the full stack**

```bash
./dev.sh
```

- [ ] **Step 2: Smoke test via Playwright**

Use Playwright MCP to:
1. Navigate to the app, log in
2. Go to `/files`
3. Verify empty state shows
4. Upload a test `.md` file via the file input
5. Verify the file appears in the list with correct name
6. Click the filename to navigate to detail view
7. Verify markdown preview renders
8. Toggle source view
9. Go back to files list
10. Delete the file and verify it's gone

- [ ] **Step 3: Commit any fixes discovered during testing**
