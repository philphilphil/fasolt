# Flashcard Creation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement flashcard creation from files/sections, card editing, deletion, custom cards, and card preview (US-3.1 through US-3.5, US-3.8).

**Architecture:** Add `Card` domain entity with soft delete. Create `/api/cards` endpoints and a `ContentExtractor` service for slicing markdown by heading. Build card creation/edit dialogs on the frontend, a cards list view, and wire up the existing "Create cards" buttons in FileDetailView. Update file endpoints to return real card counts.

**Tech Stack:** .NET 10, EF Core + Npgsql, Vue 3, TypeScript, Pinia, shadcn-vue, Tailwind CSS 3

---

## File Map

### Backend — New Files
- `spaced-md.Server/Domain/Entities/Card.cs` — Card entity with soft delete
- `spaced-md.Server/Application/Dtos/CardDtos.cs` — Request/response DTOs for card endpoints
- `spaced-md.Server/Application/Services/ContentExtractor.cs` — Frontmatter stripping and heading section extraction
- `spaced-md.Server/Api/Endpoints/CardEndpoints.cs` — /api/cards endpoints (create, list, get, update, delete, extract)

### Backend — Modified Files
- `spaced-md.Server/Infrastructure/Data/AppDbContext.cs` — Add Card DbSet and model configuration
- `spaced-md.Server/Program.cs` — Register `MapCardEndpoints()`
- `spaced-md.Server/Api/Endpoints/FileEndpoints.cs` — Update List and GetById to return real cardCount

### Frontend — New Files
- `spaced-md.client/src/stores/cards.ts` — Pinia cards store
- `spaced-md.client/src/components/CardCreateDialog.vue` — Dialog for creating cards with preview
- `spaced-md.client/src/components/CardEditDialog.vue` — Dialog for editing cards with preview
- `spaced-md.client/src/views/CardsView.vue` — Card list view with edit/delete/filter

### Frontend — Modified Files
- `spaced-md.client/src/types/index.ts` — Replace old Card/Deck interfaces, add ExtractedContent
- `spaced-md.client/src/views/FileDetailView.vue` — Wire "Create cards" buttons and add "Create card from file"
- `spaced-md.client/src/views/FilesView.vue` — Add "Create card" action per file
- `spaced-md.client/src/router/index.ts` — Add `/cards` route
- `spaced-md.client/src/layouts/AppLayout.vue` — Add "Cards" tab to navigation
- `spaced-md.client/src/components/BottomNav.vue` — Add "Cards" tab to mobile nav

---

## Task 1: Card Entity

**Files:**
- Create: `spaced-md.Server/Domain/Entities/Card.cs`

- [ ] **Step 1: Create Card entity**

```csharp
// spaced-md.Server/Domain/Entities/Card.cs
namespace SpacedMd.Server.Domain.Entities;

public class Card
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public Guid? FileId { get; set; }
    public MarkdownFile? File { get; set; }
    public string? SourceHeading { get; set; }
    public string Front { get; set; } = default!;
    public string Back { get; set; } = default!;
    public string CardType { get; set; } = default!; // "file", "section", "custom"
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

- [ ] **Step 2: Commit**

```bash
git add spaced-md.Server/Domain/Entities/Card.cs
git commit -m "feat: add Card domain entity with soft delete"
```

---

## Task 2: DbContext and Migration

**Files:**
- Modify: `spaced-md.Server/Infrastructure/Data/AppDbContext.cs`

- [ ] **Step 1: Add Card DbSet and configuration**

Add `DbSet<Card>` property and configure in `OnModelCreating`. Read the file first, then add:

After the existing `DbSet<FileHeading>` line:
```csharp
public DbSet<Card> Cards => Set<Card>();
```

Inside `OnModelCreating`, after the `FileHeading` configuration block, add:

```csharp
builder.Entity<Card>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Front).IsRequired();
    entity.Property(e => e.Back).IsRequired();
    entity.Property(e => e.CardType).HasMaxLength(20).IsRequired();
    entity.HasIndex(e => e.UserId);
    entity.HasIndex(e => e.FileId);
    entity.HasQueryFilter(e => e.DeletedAt == null);
    entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne(e => e.File).WithMany().HasForeignKey(e => e.FileId).OnDelete(DeleteBehavior.SetNull);
});
```

Note: `HasQueryFilter(e => e.DeletedAt == null)` automatically filters soft-deleted cards on all queries. Use `IgnoreQueryFilters()` if you ever need to include deleted cards.

- [ ] **Step 2: Create and apply migration**

```bash
dotnet ef migrations add AddCards --project spaced-md.Server
docker compose up -d
dotnet ef database update --project spaced-md.Server
```

- [ ] **Step 3: Commit**

```bash
git add spaced-md.Server/Infrastructure/Data/AppDbContext.cs spaced-md.Server/Infrastructure/Data/Migrations/
git commit -m "feat: add Cards schema with soft delete and migration"
```

---

## Task 3: ContentExtractor Service

**Files:**
- Create: `spaced-md.Server/Application/Services/ContentExtractor.cs`

- [ ] **Step 1: Create ContentExtractor**

```csharp
// spaced-md.Server/Application/Services/ContentExtractor.cs
using System.Text;
using System.Text.RegularExpressions;

namespace SpacedMd.Server.Application.Services;

public static partial class ContentExtractor
{
    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingPattern();

    public static string StripFrontmatter(string markdown)
    {
        if (!markdown.StartsWith("---\n") && !markdown.StartsWith("---\r\n"))
            return markdown;

        var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end == -1) return markdown;

        var afterFrontmatter = markdown.IndexOf('\n', end + 4);
        if (afterFrontmatter == -1) return string.Empty;

        return markdown[(afterFrontmatter + 1)..];
    }

    public static string? ExtractSection(string markdown, string heading)
    {
        var lines = markdown.Split('\n');
        var inCodeFence = false;
        var foundHeading = false;
        var headingLevel = 0;
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.StartsWith("```"))
            {
                inCodeFence = !inCodeFence;
                if (foundHeading) result.AppendLine(line);
                continue;
            }

            if (inCodeFence)
            {
                if (foundHeading) result.AppendLine(line);
                continue;
            }

            var match = HeadingPattern().Match(trimmed);
            if (match.Success)
            {
                var level = match.Groups[1].Value.Length;
                var text = match.Groups[2].Value.Trim();

                if (!foundHeading && text == heading)
                {
                    foundHeading = true;
                    headingLevel = level;
                    result.AppendLine(line);
                    continue;
                }

                if (foundHeading && level <= headingLevel)
                    break;
            }

            if (foundHeading) result.AppendLine(line);
        }

        return foundHeading ? result.ToString().TrimEnd() : null;
    }

    public static string? GetFirstH1(string markdown)
    {
        var lines = markdown.Split('\n');
        var inCodeFence = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.StartsWith("```"))
            {
                inCodeFence = !inCodeFence;
                continue;
            }

            if (inCodeFence) continue;

            var match = HeadingPattern().Match(trimmed);
            if (match.Success && match.Groups[1].Value.Length == 1)
                return match.Groups[2].Value.Trim();
        }

        return null;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add spaced-md.Server/Application/Services/ContentExtractor.cs
git commit -m "feat: add ContentExtractor service for section extraction and frontmatter stripping"
```

---

## Task 4: Card DTOs

**Files:**
- Create: `spaced-md.Server/Application/Dtos/CardDtos.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// spaced-md.Server/Application/Dtos/CardDtos.cs
namespace SpacedMd.Server.Application.Dtos;

public record CreateCardRequest(Guid? FileId, string? SourceHeading, string Front, string Back, string CardType);

public record UpdateCardRequest(string Front, string Back);

public record CardDto(
    Guid Id,
    Guid? FileId,
    string? SourceHeading,
    string Front,
    string Back,
    string CardType,
    DateTimeOffset CreatedAt);

public record ExtractedContentDto(string Front, string Back);
```

- [ ] **Step 2: Commit**

```bash
git add spaced-md.Server/Application/Dtos/CardDtos.cs
git commit -m "feat: add card DTOs"
```

---

## Task 5: Card API Endpoints

**Files:**
- Create: `spaced-md.Server/Api/Endpoints/CardEndpoints.cs`
- Modify: `spaced-md.Server/Program.cs`

- [ ] **Step 1: Create CardEndpoints**

```csharp
// spaced-md.Server/Api/Endpoints/CardEndpoints.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpacedMd.Server.Application.Dtos;
using SpacedMd.Server.Application.Services;
using SpacedMd.Server.Domain.Entities;
using SpacedMd.Server.Infrastructure.Data;

namespace SpacedMd.Server.Api.Endpoints;

public static class CardEndpoints
{
    private static readonly string[] ValidCardTypes = ["file", "section", "custom"];

    public static void MapCardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cards").RequireAuthorization();

        group.MapPost("/", Create);
        group.MapGet("/", List);
        group.MapGet("/extract", Extract);
        group.MapGet("/{id:guid}", GetById);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> Create(
        CreateCardRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Front) || string.IsNullOrWhiteSpace(request.Back))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Front and back are required."]
            });

        if (!ValidCardTypes.Contains(request.CardType))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["cardType"] = ["Card type must be 'file', 'section', or 'custom'."]
            });

        if (request.CardType is "file" or "section")
        {
            if (request.FileId is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["fileId"] = ["File ID is required for file and section cards."]
                });

            var fileExists = await db.MarkdownFiles
                .AnyAsync(f => f.Id == request.FileId && f.UserId == user.Id);
            if (!fileExists) return Results.NotFound();
        }

        var card = new Card
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FileId = request.FileId,
            SourceHeading = request.SourceHeading,
            Front = request.Front,
            Back = request.Back,
            CardType = request.CardType,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Cards.Add(card);
        await db.SaveChangesAsync();

        return Results.Created($"/api/cards/{card.Id}", ToDto(card));
    }

    private static async Task<IResult> List(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db,
        Guid? fileId = null)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var query = db.Cards.Where(c => c.UserId == user.Id);

        if (fileId.HasValue)
            query = query.Where(c => c.FileId == fileId.Value);

        var cards = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CardDto(c.Id, c.FileId, c.SourceHeading, c.Front, c.Back, c.CardType, c.CreatedAt))
            .ToListAsync();

        return Results.Ok(cards);
    }

    private static async Task<IResult> GetById(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

        if (card is null) return Results.NotFound();

        return Results.Ok(ToDto(card));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateCardRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Front) || string.IsNullOrWhiteSpace(request.Back))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Front and back are required."]
            });

        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

        if (card is null) return Results.NotFound();

        card.Front = request.Front;
        card.Back = request.Back;
        await db.SaveChangesAsync();

        return Results.Ok(ToDto(card));
    }

    private static async Task<IResult> Delete(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

        if (card is null) return Results.NotFound();

        card.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> Extract(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db,
        Guid? fileId = null,
        string? heading = null)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (fileId is null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["fileId"] = ["File ID is required."]
            });

        var file = await db.MarkdownFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == user.Id);

        if (file is null) return Results.NotFound();

        string front;
        string back;

        if (string.IsNullOrWhiteSpace(heading))
        {
            // Whole file extraction
            front = ContentExtractor.GetFirstH1(file.Content) ?? file.FileName;
            back = ContentExtractor.StripFrontmatter(file.Content);
        }
        else
        {
            // Section extraction
            front = heading;
            var section = ContentExtractor.ExtractSection(file.Content, heading);
            if (section is null) return Results.NotFound();
            back = section;
        }

        return Results.Ok(new ExtractedContentDto(front, back));
    }

    private static CardDto ToDto(Card c) =>
        new(c.Id, c.FileId, c.SourceHeading, c.Front, c.Back, c.CardType, c.CreatedAt);
}
```

- [ ] **Step 2: Register in Program.cs**

Add after `app.MapFileEndpoints();`:

```csharp
app.MapCardEndpoints();
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build spaced-md.Server
```

- [ ] **Step 4: Commit**

```bash
git add spaced-md.Server/Api/Endpoints/CardEndpoints.cs spaced-md.Server/Program.cs
git commit -m "feat: add /api/cards endpoints (create, list, get, update, delete, extract)"
```

---

## Task 6: Update File Endpoints for Real Card Count

**Files:**
- Modify: `spaced-md.Server/Api/Endpoints/FileEndpoints.cs`

- [ ] **Step 1: Update List query**

In the `List` method, replace the hardcoded `0` cardCount with a real count. Change the `.Select(...)` to:

```csharp
.Select(f => new FileListItemDto(
    f.Id,
    f.FileName,
    f.SizeBytes,
    f.UploadedAt,
    db.Cards.Count(c => c.FileId == f.Id && c.DeletedAt == null),
    f.Headings.OrderBy(h => h.SortOrder)
        .Select(h => new FileHeadingDto(h.Level, h.Text)).ToList()))
```

Note: We use `db.Cards.Count(...)` with explicit `DeletedAt == null` filter instead of relying on the global query filter, because the global filter may not apply within a subquery projection. This ensures correct counts regardless.

- [ ] **Step 2: Update GetById**

In the `GetById` method, replace the hardcoded `0` with:

```csharp
var cardCount = await db.Cards.CountAsync(c => c.FileId == id && c.UserId == user.Id && c.DeletedAt == null);
```

Then use `cardCount` in the `FileDetailDto` constructor instead of `0`.

- [ ] **Step 3: Update ToListItem**

In the `ToListItem` method, the hardcoded `0` stays for now — this method is only used after single file upload when no cards exist yet.

- [ ] **Step 4: Build and verify**

```bash
dotnet build spaced-md.Server
```

- [ ] **Step 5: Commit**

```bash
git add spaced-md.Server/Api/Endpoints/FileEndpoints.cs
git commit -m "feat: return real card counts from file endpoints"
```

---

## Task 7: Frontend Types and Cards Store

**Files:**
- Modify: `spaced-md.client/src/types/index.ts`
- Create: `spaced-md.client/src/stores/cards.ts`

- [ ] **Step 1: Update types**

Read `types/index.ts`. Replace the old `Card` interface (lines 1-12) and `Deck` interface (lines 14-21) with:

```typescript
export interface Card {
  id: string
  fileId: string | null
  sourceHeading: string | null
  front: string
  back: string
  cardType: 'file' | 'section' | 'custom'
  createdAt: string
}

export interface ExtractedContent {
  front: string
  back: string
}
```

Keep all other interfaces (`Stat`, `MarkdownFile`, `FileHeading`, `FileDetail`, `BulkUploadResult`, `Group`, `ReviewRating`) unchanged.

- [ ] **Step 2: Create cards store**

```typescript
// spaced-md.client/src/stores/cards.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Card, ExtractedContent } from '@/types'
import { apiFetch } from '@/api/client'

export const useCardsStore = defineStore('cards', () => {
  const cards = ref<Card[]>([])
  const loading = ref(false)

  async function fetchCards(fileId?: string) {
    loading.value = true
    try {
      const params = fileId ? `?fileId=${fileId}` : ''
      cards.value = await apiFetch<Card[]>(`/cards${params}`)
    } finally {
      loading.value = false
    }
  }

  async function createCard(data: {
    fileId?: string
    sourceHeading?: string
    front: string
    back: string
    cardType: string
  }): Promise<Card> {
    const result = await apiFetch<Card>('/cards', {
      method: 'POST',
      body: JSON.stringify(data),
    })
    await fetchCards()
    return result
  }

  async function updateCard(id: string, data: { front: string; back: string }): Promise<Card> {
    const result = await apiFetch<Card>(`/cards/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    })
    const idx = cards.value.findIndex(c => c.id === id)
    if (idx !== -1) cards.value[idx] = result
    return result
  }

  async function deleteCard(id: string) {
    await apiFetch(`/cards/${id}`, { method: 'DELETE' })
    cards.value = cards.value.filter(c => c.id !== id)
  }

  async function extractContent(fileId: string, heading?: string): Promise<ExtractedContent> {
    const params = heading ? `?fileId=${fileId}&heading=${encodeURIComponent(heading)}` : `?fileId=${fileId}`
    return apiFetch<ExtractedContent>(`/cards/extract${params}`)
  }

  return { cards, loading, fetchCards, createCard, updateCard, deleteCard, extractContent }
})
```

- [ ] **Step 3: Commit**

```bash
git add spaced-md.client/src/types/index.ts spaced-md.client/src/stores/cards.ts
git commit -m "feat: update Card type and add cards store"
```

---

## Task 8: CardCreateDialog Component

**Files:**
- Create: `spaced-md.client/src/components/CardCreateDialog.vue`

- [ ] **Step 1: Create CardCreateDialog**

```vue
<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useCardsStore } from '@/stores/cards'
import { useMarkdown } from '@/composables/useMarkdown'
import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const props = defineProps<{
  open: boolean
  fileId?: string
  sourceHeading?: string
  initialFront?: string
  initialBack?: string
  cardType: 'file' | 'section' | 'custom'
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  created: []
}>()

const cards = useCardsStore()
const { render } = useMarkdown()

const front = ref('')
const back = ref('')
const saving = ref(false)
const error = ref('')
const showPreview = ref(false)

watch(() => props.open, (isOpen) => {
  if (isOpen) {
    front.value = props.initialFront ?? ''
    back.value = props.initialBack ?? ''
    error.value = ''
    showPreview.value = false
  }
})

const isLong = computed(() => back.value.length > 2000)
const renderedFront = computed(() => render(front.value))
const renderedBack = computed(() => render(back.value))

async function save() {
  if (!front.value.trim() || !back.value.trim()) {
    error.value = 'Front and back are required.'
    return
  }
  saving.value = true
  error.value = ''
  try {
    await cards.createCard({
      fileId: props.fileId,
      sourceHeading: props.sourceHeading,
      front: front.value,
      back: back.value,
      cardType: props.cardType,
    })
    emit('update:open', false)
    emit('created')
  } catch {
    error.value = 'Failed to create card.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="max-w-2xl max-h-[85vh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>Create card</DialogTitle>
      </DialogHeader>

      <div class="space-y-4">
        <!-- Front -->
        <div class="space-y-1">
          <label class="text-xs font-medium text-muted-foreground">Front (question)</label>
          <textarea
            v-if="!showPreview"
            v-model="front"
            class="w-full rounded-md border border-border bg-transparent px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            rows="2"
          />
          <div v-else class="prose prose-sm dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="renderedFront" />
        </div>

        <!-- Back -->
        <div class="space-y-1">
          <label class="text-xs font-medium text-muted-foreground">Back (answer)</label>
          <textarea
            v-if="!showPreview"
            v-model="back"
            class="w-full rounded-md border border-border bg-transparent px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            rows="8"
          />
          <div v-else class="prose prose-sm dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="renderedBack" />
        </div>

        <!-- Long content warning -->
        <div v-if="isLong" class="rounded-md border border-yellow-500/30 bg-yellow-500/10 px-3 py-2 text-xs text-yellow-600 dark:text-yellow-400">
          This card is quite long ({{ back.length.toLocaleString() }} chars). Consider creating cards from specific sections instead.
        </div>

        <div v-if="error" class="text-xs text-destructive">{{ error }}</div>
      </div>

      <DialogFooter class="gap-2">
        <Button variant="outline" size="sm" @click="showPreview = !showPreview">
          {{ showPreview ? 'Edit' : 'Preview' }}
        </Button>
        <Button variant="outline" size="sm" @click="emit('update:open', false)">Cancel</Button>
        <Button size="sm" :disabled="saving" @click="save">
          {{ saving ? 'Saving...' : 'Create card' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add spaced-md.client/src/components/CardCreateDialog.vue
git commit -m "feat: add CardCreateDialog with live preview and long content warning"
```

---

## Task 9: CardEditDialog Component

**Files:**
- Create: `spaced-md.client/src/components/CardEditDialog.vue`

- [ ] **Step 1: Create CardEditDialog**

```vue
<script setup lang="ts">
import { ref, watch } from 'vue'
import { useCardsStore } from '@/stores/cards'
import { useMarkdown } from '@/composables/useMarkdown'
import type { Card } from '@/types'
import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const props = defineProps<{
  open: boolean
  card: Card | null
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  updated: []
}>()

const cards = useCardsStore()
const { render } = useMarkdown()

const front = ref('')
const back = ref('')
const saving = ref(false)
const error = ref('')
const showPreview = ref(false)

watch(() => props.open, (isOpen) => {
  if (isOpen && props.card) {
    front.value = props.card.front
    back.value = props.card.back
    error.value = ''
    showPreview.value = false
  }
})

async function save() {
  if (!props.card || !front.value.trim() || !back.value.trim()) {
    error.value = 'Front and back are required.'
    return
  }
  saving.value = true
  error.value = ''
  try {
    await cards.updateCard(props.card.id, { front: front.value, back: back.value })
    emit('update:open', false)
    emit('updated')
  } catch {
    error.value = 'Failed to update card.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="max-w-2xl max-h-[85vh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>Edit card</DialogTitle>
      </DialogHeader>

      <div class="space-y-4">
        <div class="space-y-1">
          <label class="text-xs font-medium text-muted-foreground">Front (question)</label>
          <textarea
            v-if="!showPreview"
            v-model="front"
            class="w-full rounded-md border border-border bg-transparent px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            rows="2"
          />
          <div v-else class="prose prose-sm dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="render(front)" />
        </div>

        <div class="space-y-1">
          <label class="text-xs font-medium text-muted-foreground">Back (answer)</label>
          <textarea
            v-if="!showPreview"
            v-model="back"
            class="w-full rounded-md border border-border bg-transparent px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            rows="8"
          />
          <div v-else class="prose prose-sm dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="render(back)" />
        </div>

        <div v-if="error" class="text-xs text-destructive">{{ error }}</div>
      </div>

      <DialogFooter class="gap-2">
        <Button variant="outline" size="sm" @click="showPreview = !showPreview">
          {{ showPreview ? 'Edit' : 'Preview' }}
        </Button>
        <Button variant="outline" size="sm" @click="emit('update:open', false)">Cancel</Button>
        <Button size="sm" :disabled="saving" @click="save">
          {{ saving ? 'Saving...' : 'Save' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add spaced-md.client/src/components/CardEditDialog.vue
git commit -m "feat: add CardEditDialog with live preview"
```

---

## Task 10: CardsView, Route, and Navigation

**Files:**
- Create: `spaced-md.client/src/views/CardsView.vue`
- Modify: `spaced-md.client/src/router/index.ts`
- Modify: `spaced-md.client/src/layouts/AppLayout.vue`
- Modify: `spaced-md.client/src/components/BottomNav.vue`

- [ ] **Step 1: Create CardsView**

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useCardsStore } from '@/stores/cards'
import { useFilesStore } from '@/stores/files'
import type { Card } from '@/types'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import CardCreateDialog from '@/components/CardCreateDialog.vue'
import CardEditDialog from '@/components/CardEditDialog.vue'

const cards = useCardsStore()
const files = useFilesStore()

const filterFileId = ref<string>('')
const editTarget = ref<Card | null>(null)
const editOpen = ref(false)
const deleteTarget = ref<Card | null>(null)
const deleteError = ref('')
const createOpen = ref(false)

onMounted(async () => {
  await Promise.all([cards.fetchCards(), files.fetchFiles()])
})

async function applyFilter() {
  await cards.fetchCards(filterFileId.value || undefined)
}

function getFileName(fileId: string | null): string {
  if (!fileId) return '—'
  const f = files.files.find(f => f.id === fileId)
  return f?.fileName ?? '(deleted)'
}

function truncate(text: string, max = 60): string {
  return text.length > max ? text.slice(0, max) + '…' : text
}

function formatDate(date: string): string {
  return new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

function openEdit(card: Card) {
  editTarget.value = card
  editOpen.value = true
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleteError.value = ''
  try {
    await cards.deleteCard(deleteTarget.value.id)
    deleteTarget.value = null
  } catch {
    deleteError.value = 'Failed to delete card.'
  }
}
</script>

<template>
  <div class="space-y-4">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <select
          v-model="filterFileId"
          class="h-7 rounded-md border border-border bg-transparent px-2 text-xs"
          @change="applyFilter"
        >
          <option value="">All files</option>
          <option v-for="f in files.files" :key="f.id" :value="f.id">{{ f.fileName }}</option>
        </select>
      </div>
      <Button size="sm" class="h-7 text-xs" @click="createOpen = true">New card</Button>
    </div>

    <!-- Cards table -->
    <Table v-if="cards.cards.length > 0">
      <TableHeader>
        <TableRow class="text-[10px] uppercase tracking-wider text-muted-foreground hover:bg-transparent">
          <TableHead class="h-8">Front</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Source</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Type</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Created</TableHead>
          <TableHead class="h-8 w-20"></TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow v-for="card in cards.cards" :key="card.id" class="text-xs">
          <TableCell class="font-medium text-foreground max-w-[200px] truncate">{{ truncate(card.front) }}</TableCell>
          <TableCell class="hidden text-muted-foreground sm:table-cell font-mono">{{ getFileName(card.fileId) }}</TableCell>
          <TableCell class="hidden sm:table-cell">
            <Badge variant="secondary" class="text-[10px]">{{ card.cardType }}</Badge>
          </TableCell>
          <TableCell class="hidden text-muted-foreground sm:table-cell">{{ formatDate(card.createdAt) }}</TableCell>
          <TableCell class="flex gap-1">
            <Button variant="ghost" size="sm" class="h-6 text-[10px]" @click="openEdit(card)">Edit</Button>
            <Button variant="ghost" size="sm" class="h-6 text-[10px] text-muted-foreground hover:text-destructive" @click="deleteTarget = card">&times;</Button>
          </TableCell>
        </TableRow>
      </TableBody>
    </Table>

    <!-- Empty state -->
    <div v-if="!cards.loading && cards.cards.length === 0" class="py-12 text-center text-sm text-muted-foreground">
      No cards yet. Create one from a file or start from scratch.
    </div>

    <!-- Create dialog (custom cards) -->
    <CardCreateDialog
      v-model:open="createOpen"
      card-type="custom"
      initial-front=""
      initial-back=""
      @created="cards.fetchCards(filterFileId || undefined)"
    />

    <!-- Edit dialog -->
    <CardEditDialog
      v-model:open="editOpen"
      :card="editTarget"
      @updated="cards.fetchCards(filterFileId || undefined)"
    />

    <!-- Delete confirmation -->
    <Dialog :open="!!deleteTarget" @update:open="deleteTarget = null">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete card</DialogTitle>
          <DialogDescription>
            Are you sure you want to delete this card? It will be removed from study.
          </DialogDescription>
        </DialogHeader>
        <div v-if="deleteError" class="text-xs text-destructive">{{ deleteError }}</div>
        <DialogFooter>
          <Button variant="outline" @click="deleteTarget = null">Cancel</Button>
          <Button variant="destructive" @click="confirmDelete">Delete</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
```

- [ ] **Step 2: Add route**

In `spaced-md.client/src/router/index.ts`, add after the `/files/:id` route:

```typescript
{ path: '/cards', name: 'cards', component: () => import('@/views/CardsView.vue') },
```

- [ ] **Step 3: Add "Cards" tab to desktop navigation**

In `spaced-md.client/src/layouts/AppLayout.vue`, update the `tabs` array (around line 12-17) to add Cards between Files and Groups:

```typescript
const tabs = [
  { label: 'Dashboard', value: '/' },
  { label: 'Files', value: '/files' },
  { label: 'Cards', value: '/cards' },
  { label: 'Groups', value: '/groups' },
  { label: 'Settings', value: '/settings' },
]
```

- [ ] **Step 4: Add "Cards" tab to mobile navigation**

In `spaced-md.client/src/components/BottomNav.vue`, update the `tabs` array (around line 6-11) to add Cards between Files and Groups:

```typescript
const tabs = [
  { name: 'Dashboard', path: '/', icon: '▦' },
  { name: 'Files', path: '/files', icon: '◫' },
  { name: 'Cards', path: '/cards', icon: '▤' },
  { name: 'Groups', path: '/groups', icon: '⊞' },
  { name: 'Settings', path: '/settings', icon: '⚙' },
]
```

- [ ] **Step 5: Commit**

```bash
git add spaced-md.client/src/views/CardsView.vue spaced-md.client/src/router/index.ts spaced-md.client/src/layouts/AppLayout.vue spaced-md.client/src/components/BottomNav.vue
git commit -m "feat: add CardsView with edit/delete and Cards tab in navigation"
```

---

## Task 11: Wire Create Cards Buttons in FileDetailView

**Files:**
- Modify: `spaced-md.client/src/views/FileDetailView.vue`

- [ ] **Step 1: Update FileDetailView**

Read the file first. Add card creation functionality:

In the `<script setup>`, add imports and state:

```typescript
import { useCardsStore } from '@/stores/cards'
import CardCreateDialog from '@/components/CardCreateDialog.vue'

const cardsStore = useCardsStore()

const createOpen = ref(false)
const createFront = ref('')
const createBack = ref('')
const createHeading = ref<string | undefined>(undefined)
const createType = ref<'file' | 'section'>('file')
const extracting = ref(false)
```

Add functions for extracting content and opening the dialog:

```typescript
async function createFromFile() {
  if (!file.value) return
  extracting.value = true
  try {
    const content = await cardsStore.extractContent(file.value.id)
    createFront.value = content.front
    createBack.value = content.back
    createHeading.value = undefined
    createType.value = 'file'
    createOpen.value = true
  } finally {
    extracting.value = false
  }
}

async function createFromSection(headingText: string) {
  if (!file.value) return
  extracting.value = true
  try {
    const content = await cardsStore.extractContent(file.value.id, headingText)
    createFront.value = content.front
    createBack.value = content.back
    createHeading.value = headingText
    createType.value = 'section'
    createOpen.value = true
  } finally {
    extracting.value = false
  }
}
```

In the template header (between the filename and Source button), add:

```html
<Button variant="outline" size="sm" class="h-7 text-xs" :disabled="extracting" @click="createFromFile">
  Create card
</Button>
```

Replace the disabled "Create cards" button in the sidebar with a working one:

```html
<Button
  variant="ghost"
  size="sm"
  class="h-5 text-[10px] opacity-0 group-hover:opacity-100 shrink-0"
  @click.stop="createFromSection(heading.text)"
>
  Create cards
</Button>
```

Add the CardCreateDialog at the end of the template (before the closing `</div>`):

```html
<CardCreateDialog
  v-model:open="createOpen"
  :file-id="file?.id"
  :source-heading="createHeading"
  :initial-front="createFront"
  :initial-back="createBack"
  :card-type="createType"
/>
```

- [ ] **Step 2: Commit**

```bash
git add spaced-md.client/src/views/FileDetailView.vue
git commit -m "feat: wire Create cards buttons in FileDetailView"
```

---

## Task 12: Wire Create Card in FilesView

**Files:**
- Modify: `spaced-md.client/src/views/FilesView.vue`

- [ ] **Step 1: Add card creation to FilesView**

Read the file first. Add imports:

```typescript
import { useCardsStore } from '@/stores/cards'
import CardCreateDialog from '@/components/CardCreateDialog.vue'
```

Add state:

```typescript
const cardsStore = useCardsStore()
const createOpen = ref(false)
const createFront = ref('')
const createBack = ref('')
const createFileId = ref<string | undefined>(undefined)
const extracting = ref(false)
```

Add function:

```typescript
async function createCardFromFile(fileId: string) {
  extracting.value = true
  try {
    const content = await cardsStore.extractContent(fileId)
    createFront.value = content.front
    createBack.value = content.back
    createFileId.value = fileId
    createOpen.value = true
  } finally {
    extracting.value = false
  }
}
```

In the expanded headings area, after the "Create cards" buttons for each heading, add a "Create card from file" button. Add this inside the expanded row `<div>`, after the headings loop:

```html
<div class="mt-2 pt-2 border-t border-border/50">
  <Button
    variant="outline"
    size="sm"
    class="h-6 text-[10px]"
    :disabled="extracting"
    @click.stop="createCardFromFile(file.id)"
  >
    Create card from entire file
  </Button>
</div>
```

Add the dialog at the end of the template (before closing `</div>`):

```html
<CardCreateDialog
  v-model:open="createOpen"
  :file-id="createFileId"
  :initial-front="createFront"
  :initial-back="createBack"
  card-type="file"
  @created="files.fetchFiles()"
/>
```

- [ ] **Step 2: Commit**

```bash
git add spaced-md.client/src/views/FilesView.vue
git commit -m "feat: add Create card from file action in FilesView"
```

---

## Task 13: Playwright Smoke Test

**Files:**
- No new files — use Playwright MCP

- [ ] **Step 1: Start the full stack**

```bash
./dev.sh
```

- [ ] **Step 2: Smoke test via Playwright**

Use Playwright MCP to:
1. Log in
2. Upload a test `.md` file
3. Navigate to file detail, click "Create card" on a heading section
4. Verify the dialog opens with pre-filled front/back
5. Save the card
6. Navigate to `/cards`, verify the card appears in the list
7. Edit the card, verify changes save
8. Delete the card, verify it's gone
9. Go back to `/files`, verify card count updated
10. Create a custom card from `/cards` "New card" button
11. Verify it appears with type "custom"

- [ ] **Step 3: Commit any fixes discovered during testing**

---

## Task 14: Move Completed Requirements

**Files:**
- Move: `docs/requirements/03-flashcard-creation.md` → `docs/requirements/done/`

- [ ] **Step 1: Move requirements file**

```bash
mv docs/requirements/03-flashcard-creation.md docs/requirements/done/
git add docs/requirements/03-flashcard-creation.md docs/requirements/done/03-flashcard-creation.md
git commit -m "docs: move 03-flashcard-creation.md to done/"
```
