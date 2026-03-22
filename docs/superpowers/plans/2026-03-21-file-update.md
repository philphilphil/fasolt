# File Update (Re-upload) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow users to re-upload updated versions of existing markdown files, with a preview of card impacts (updated, orphaned, new sections) and one-click confirmation.

**Architecture:** Add a `FileComparer` service that diffs old vs new file content against existing cards. Two new endpoints: preview-update (dry run) and confirm-update (apply). Frontend detects duplicate filenames on upload and shows a preview dialog before confirming. FileDetailView gets an "Update file" button.

**Tech Stack:** .NET 10, EF Core + Npgsql, Vue 3, TypeScript, Pinia, shadcn-vue, Tailwind CSS 3

---

## File Map

### Backend — New Files
- `fasolt.Server/Application/Services/FileComparer.cs` — Compares old vs new file content, identifies card impacts
- `fasolt.Server/Application/Dtos/FileUpdateDtos.cs` — DTOs for preview and update responses

### Backend — Modified Files
- `fasolt.Server/Api/Endpoints/FileEndpoints.cs` — Add preview-update and confirm-update endpoints

### Frontend — New Files
- `fasolt.client/src/components/FileUpdatePreviewDialog.vue` — Dialog showing card impact preview

### Frontend — Modified Files
- `fasolt.client/src/types/index.ts` — Add `FileUpdatePreview` type
- `fasolt.client/src/stores/files.ts` — Add `previewUpdate` and `confirmUpdate` methods
- `fasolt.client/src/views/FilesView.vue` — Change upload flow to detect duplicates via preview-update
- `fasolt.client/src/views/FileDetailView.vue` — Add "Update file" button

---

## Task 1: FileComparer Service

**Files:**
- Create: `fasolt.Server/Application/Services/FileComparer.cs`

- [ ] **Step 1: Create FileComparer**

```csharp
// fasolt.Server/Application/Services/FileComparer.cs
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Application.Services;

public record UpdatedCardInfo(Guid CardId, string Front, string OldBack, string NewBack);
public record OrphanedCardInfo(Guid CardId, string Front, string? SourceHeading);
public record NewSectionInfo(string Heading, bool HasMarkers);

public record FileComparisonResult(
    List<UpdatedCardInfo> UpdatedCards,
    List<OrphanedCardInfo> OrphanedCards,
    List<Guid> UnchangedCardIds,
    List<NewSectionInfo> NewSections);

public static class FileComparer
{
    public static FileComparisonResult Compare(
        string newContent, List<Card> existingCards)
    {
        var updated = new List<UpdatedCardInfo>();
        var orphaned = new List<OrphanedCardInfo>();
        var unchanged = new List<Guid>();

        var newStripped = ContentExtractor.StripFrontmatter(newContent);

        foreach (var card in existingCards)
        {
            if (card.CardType == "file")
            {
                var (markers, cleanedNew) = ContentExtractor.ParseMarkers(newStripped);
                if (card.Back == cleanedNew)
                    unchanged.Add(card.Id);
                else
                    updated.Add(new UpdatedCardInfo(card.Id, card.Front, card.Back, cleanedNew));
            }
            else if (card.CardType == "section" && card.SourceHeading is not null)
            {
                var section = ContentExtractor.ExtractSection(newContent, card.SourceHeading);
                if (section is null)
                {
                    orphaned.Add(new OrphanedCardInfo(card.Id, card.Front, card.SourceHeading));
                }
                else
                {
                    var (_, cleanedSection) = ContentExtractor.ParseMarkers(section);
                    if (card.Back == cleanedSection)
                        unchanged.Add(card.Id);
                    else
                        updated.Add(new UpdatedCardInfo(card.Id, card.Front, card.Back, cleanedSection));
                }
            }
            else
            {
                // Custom cards or cards without source heading — unchanged
                unchanged.Add(card.Id);
            }
        }

        // Find new sections
        var existingHeadings = existingCards
            .Where(c => c.CardType == "section" && c.SourceHeading is not null)
            .Select(c => c.SourceHeading!)
            .ToHashSet();

        var newSections = new List<NewSectionInfo>();
        var allNewHeadings = HeadingExtractor.Extract(newContent);
        foreach (var (_, text, _) in allNewHeadings)
        {
            if (!existingHeadings.Contains(text))
            {
                var section = ContentExtractor.ExtractSection(newContent, text);
                var hasMarkers = false;
                if (section is not null)
                {
                    var (markers, _) = ContentExtractor.ParseMarkers(section);
                    hasMarkers = markers.Count > 0;
                }
                newSections.Add(new NewSectionInfo(text, hasMarkers));
            }
        }

        return new FileComparisonResult(updated, orphaned, unchanged, newSections);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.Server/Application/Services/FileComparer.cs
git commit -m "feat: add FileComparer service for file update diffing"
```

---

## Task 2: File Update DTOs

**Files:**
- Create: `fasolt.Server/Application/Dtos/FileUpdateDtos.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// fasolt.Server/Application/Dtos/FileUpdateDtos.cs
namespace Fasolt.Server.Application.Dtos;

public record FileUpdatePreviewDto(
    Guid FileId,
    string FileName,
    List<UpdatedCardPreviewDto> UpdatedCards,
    List<OrphanedCardPreviewDto> OrphanedCards,
    int UnchangedCount,
    List<NewSectionPreviewDto> NewSections);

public record UpdatedCardPreviewDto(Guid CardId, string Front, string OldBack, string NewBack);
public record OrphanedCardPreviewDto(Guid CardId, string Front, string? SourceHeading);
public record NewSectionPreviewDto(string Heading, bool HasMarkers);

public record FileUpdateResultDto(int UpdatedCount, int DeletedCount, int OrphanedCount);
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.Server/Application/Dtos/FileUpdateDtos.cs
git commit -m "feat: add file update DTOs"
```

---

## Task 3: Preview-Update and Confirm-Update Endpoints

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/FileEndpoints.cs`

- [ ] **Step 1: Add route registrations**

Read `FileEndpoints.cs`. In the `MapFileEndpoints` method, add after the existing `group.MapDelete(...)` line:

```csharp
group.MapPost("/preview-update", PreviewUpdate).DisableAntiforgery();
group.MapPost("/{id:guid}/update", ConfirmUpdate).DisableAntiforgery();
```

- [ ] **Step 2: Add PreviewUpdate endpoint**

Add this method to the `FileEndpoints` class:

```csharp
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
```

- [ ] **Step 3: Add ConfirmUpdate endpoint**

Add this method to the `FileEndpoints` class:

```csharp
private static async Task<IResult> ConfirmUpdate(
    Guid id,
    IFormFile file,
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager,
    AppDbContext db,
    [FromForm] List<Guid>? deleteCardIds = null)
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
        .Include(f => f.Headings)
        .FirstOrDefaultAsync(f => f.Id == id && f.UserId == user.Id);

    if (existing is null) return Results.NotFound();

    using var reader = new StreamReader(file.OpenReadStream());
    var newContent = await reader.ReadToEndAsync();

    // Validate deleteCardIds ownership
    var idsToDelete = deleteCardIds ?? [];
    if (idsToDelete.Count > 0)
    {
        var validCount = await db.Cards
            .CountAsync(c => idsToDelete.Contains(c.Id) && c.UserId == user.Id && c.FileId == id);
        if (validCount != idsToDelete.Count)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["deleteCardIds"] = ["One or more card IDs are invalid."]
            });
    }

    // Get all cards for this file
    var cards = await db.Cards
        .Where(c => c.FileId == existing.Id && c.UserId == user.Id)
        .ToListAsync();

    var comparison = FileComparer.Compare(newContent, cards);

    // Capture old derived front BEFORE overwriting content
    var oldFirstH1 = ContentExtractor.GetFirstH1(existing.Content);

    // 1. Update file content
    existing.Content = newContent;
    existing.SizeBytes = file.Length;
    existing.UploadedAt = DateTimeOffset.UtcNow;

    // 2. Re-extract headings
    db.FileHeadings.RemoveRange(existing.Headings);
    var newHeadings = HeadingExtractor.Extract(newContent);
    existing.Headings = newHeadings.Select(h => new FileHeading
    {
        Id = Guid.NewGuid(),
        Level = h.Level,
        Text = h.Text,
        SortOrder = h.SortOrder,
    }).ToList();

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
                // Only update front if it still matches old derived value
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
            // If section is null, it's orphaned — handled below
        }
    }

    // 4. Soft-delete requested cards
    var deletedCount = 0;
    foreach (var card in cards.Where(c => idsToDelete.Contains(c.Id)))
    {
        card.DeletedAt = DateTimeOffset.UtcNow;
        deletedCount++;
    }

    // 5. Unlink remaining orphaned cards
    var orphanedIds = comparison.OrphanedCards.Select(c => c.CardId).ToHashSet();
    var orphanedCount = 0;
    foreach (var card in cards.Where(c => orphanedIds.Contains(c.Id) && !idsToDelete.Contains(c.Id)))
    {
        card.FileId = null;
        orphanedCount++;
    }

    await db.SaveChangesAsync();

    return Results.Ok(new FileUpdateResultDto(updatedCount, deletedCount, orphanedCount));
}
```

Note: You'll need to add `using Microsoft.AspNetCore.Mvc;` at the top of the file for the `[FromForm]` attribute.

- [ ] **Step 4: Build and verify**

```bash
dotnet build fasolt.Server
```

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Api/Endpoints/FileEndpoints.cs
git commit -m "feat: add preview-update and confirm-update endpoints"
```

---

## Task 4: Frontend Types and Store

**Files:**
- Modify: `fasolt.client/src/types/index.ts`
- Modify: `fasolt.client/src/stores/files.ts`

- [ ] **Step 1: Add FileUpdatePreview type**

Read `types/index.ts`. Add at the end of the file (before any closing content):

```typescript
export interface FileUpdatePreview {
  fileId: string
  fileName: string
  updatedCards: { cardId: string; front: string; oldBack: string; newBack: string }[]
  orphanedCards: { cardId: string; front: string; sourceHeading: string }[]
  unchangedCount: number
  newSections: { heading: string; hasMarkers: boolean }[]
}
```

- [ ] **Step 2: Add store methods**

Read `stores/files.ts`. Add two new methods before the `return` statement. Also add `FileUpdatePreview` to the type import, and `apiUpload` is already imported:

```typescript
async function previewUpdate(file: File): Promise<FileUpdatePreview | null> {
  const formData = new FormData()
  formData.append('file', file)
  try {
    return await apiUpload<FileUpdatePreview>('/files/preview-update', formData)
  } catch (err: unknown) {
    if (typeof err === 'object' && err !== null && 'status' in err && (err as { status: number }).status === 404) {
      return null // No existing file — caller should use normal upload
    }
    throw err
  }
}

async function confirmUpdate(fileId: string, file: File, deleteCardIds: string[]): Promise<void> {
  const formData = new FormData()
  formData.append('file', file)
  for (const id of deleteCardIds) {
    formData.append('deleteCardIds', id)
  }
  await apiUpload(`/files/${fileId}/update`, formData)
  await fetchFiles()
}
```

Add `previewUpdate` and `confirmUpdate` to the return object.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/types/index.ts fasolt.client/src/stores/files.ts
git commit -m "feat: add FileUpdatePreview type and store methods"
```

---

## Task 5: FileUpdatePreviewDialog Component

**Files:**
- Create: `fasolt.client/src/components/FileUpdatePreviewDialog.vue`

- [ ] **Step 1: Create the dialog**

```vue
<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { FileUpdatePreview } from '@/types'
import { useFilesStore } from '@/stores/files'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const props = defineProps<{
  open: boolean
  preview: FileUpdatePreview | null
  file: File | null
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  confirmed: []
}>()

const files = useFilesStore()
const saving = ref(false)
const error = ref('')

// Track which orphaned cards to delete (default: keep all)
const deleteSet = ref<Set<string>>(new Set())

// Reset state when preview changes
watch(() => props.preview, () => {
  deleteSet.value = new Set()
  error.value = ''
})

function toggleDelete(cardId: string) {
  if (deleteSet.value.has(cardId)) {
    deleteSet.value.delete(cardId)
  } else {
    deleteSet.value.add(cardId)
  }
  // Trigger reactivity
  deleteSet.value = new Set(deleteSet.value)
}

const hasChanges = computed(() => {
  if (!props.preview) return false
  return props.preview.updatedCards.length > 0
    || props.preview.orphanedCards.length > 0
    || props.preview.newSections.length > 0
})

async function confirm() {
  if (!props.preview || !props.file) return
  saving.value = true
  error.value = ''
  try {
    await files.confirmUpdate(
      props.preview.fileId,
      props.file,
      Array.from(deleteSet.value),
    )
    emit('update:open', false)
    emit('confirmed')
  } catch {
    error.value = 'Failed to update file.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="max-w-lg max-h-[85vh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>Updating {{ preview?.fileName }}</DialogTitle>
      </DialogHeader>

      <div v-if="preview" class="space-y-4 text-sm">
        <!-- No changes -->
        <div v-if="!hasChanges" class="text-muted-foreground">
          No card changes detected. File content will be replaced.
        </div>

        <!-- Updated cards -->
        <div v-if="preview.updatedCards.length > 0">
          <div class="text-xs font-medium text-muted-foreground mb-2">
            Cards updated ({{ preview.updatedCards.length }})
          </div>
          <div class="space-y-1">
            <div v-for="card in preview.updatedCards" :key="card.cardId" class="flex items-center gap-2 rounded border border-border px-3 py-2 text-xs">
              <Badge variant="secondary" class="text-[10px] shrink-0">changed</Badge>
              <span class="truncate">{{ card.front }}</span>
            </div>
          </div>
        </div>

        <!-- Orphaned cards -->
        <div v-if="preview.orphanedCards.length > 0">
          <div class="text-xs font-medium text-muted-foreground mb-2">
            Section removed ({{ preview.orphanedCards.length }})
          </div>
          <div class="space-y-1">
            <div v-for="card in preview.orphanedCards" :key="card.cardId" class="flex items-center justify-between rounded border border-border px-3 py-2 text-xs">
              <div class="flex items-center gap-2 min-w-0">
                <Badge variant="secondary" class="text-[10px] shrink-0">orphan</Badge>
                <span class="truncate">{{ card.front }}</span>
              </div>
              <Button
                :variant="deleteSet.has(card.cardId) ? 'destructive' : 'outline'"
                size="sm"
                class="h-6 text-[10px] shrink-0"
                @click="toggleDelete(card.cardId)"
              >
                {{ deleteSet.has(card.cardId) ? 'Delete' : 'Keep' }}
              </Button>
            </div>
          </div>
        </div>

        <!-- Unchanged -->
        <div v-if="preview.unchangedCount > 0" class="text-xs text-muted-foreground">
          {{ preview.unchangedCount }} card{{ preview.unchangedCount === 1 ? '' : 's' }} unchanged
        </div>

        <!-- New sections -->
        <div v-if="preview.newSections.length > 0">
          <div class="text-xs font-medium text-muted-foreground mb-2">
            New sections ({{ preview.newSections.length }})
          </div>
          <div class="space-y-1">
            <div v-for="section in preview.newSections" :key="section.heading" class="flex items-center gap-2 rounded border border-border px-3 py-2 text-xs text-muted-foreground">
              <Badge variant="secondary" class="text-[10px]">new</Badge>
              <span>{{ section.heading }}</span>
              <span v-if="section.hasMarkers" class="text-[10px]">(has questions)</span>
            </div>
          </div>
          <div class="text-[10px] text-muted-foreground mt-1">
            Create cards from new sections after updating.
          </div>
        </div>

        <div v-if="error" class="text-xs text-destructive">{{ error }}</div>
      </div>

      <DialogFooter>
        <Button variant="outline" size="sm" @click="emit('update:open', false)">Cancel</Button>
        <Button size="sm" :disabled="saving" @click="confirm">
          {{ saving ? 'Updating...' : 'Confirm update' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/components/FileUpdatePreviewDialog.vue
git commit -m "feat: add FileUpdatePreviewDialog component"
```

---

## Task 6: Update Upload Flow in FilesView

**Files:**
- Modify: `fasolt.client/src/views/FilesView.vue`

- [ ] **Step 1: Update FilesView**

Read the file first. Make these changes:

**Add imports** (in `<script setup>`):

```typescript
import type { FileUpdatePreview } from '@/types'
import FileUpdatePreviewDialog from '@/components/FileUpdatePreviewDialog.vue'
```

**Add state** (after existing state declarations):

```typescript
const updatePreview = ref<FileUpdatePreview | null>(null)
const updateFile = ref<File | null>(null)
const updateOpen = ref(false)
```

**Modify `handleFiles` function** — change the single-file upload path to try preview-update first:

Replace the single-file branch (the `if (valid.length === 1)` block) with:

```typescript
if (valid.length === 1) {
  uploadProgress.value = `Checking ${valid[0].name}...`
  const preview = await files.previewUpdate(valid[0])
  if (preview) {
    // Existing file — show update preview
    updatePreview.value = preview
    updateFile.value = valid[0]
    uploadProgress.value = ''
    updateOpen.value = true
  } else {
    // New file — normal upload
    uploadProgress.value = `Uploading ${valid[0].name}...`
    await files.uploadFile(valid[0])
    uploadProgress.value = `Uploaded ${valid[0].name}`
  }
}
```

**Add the dialog** in the template, after the existing `CardCreateDialog`:

```html
<FileUpdatePreviewDialog
  v-model:open="updateOpen"
  :preview="updatePreview"
  :file="updateFile"
  @confirmed="files.fetchFiles()"
/>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/views/FilesView.vue
git commit -m "feat: detect duplicate uploads and show update preview in FilesView"
```

---

## Task 7: Add "Update file" Button to FileDetailView

**Files:**
- Modify: `fasolt.client/src/views/FileDetailView.vue`

- [ ] **Step 1: Update FileDetailView**

Read the file first. Make these changes:

**Add imports**:

```typescript
import type { FileUpdatePreview } from '@/types'
import FileUpdatePreviewDialog from '@/components/FileUpdatePreviewDialog.vue'
```

**Add state** (after existing state):

```typescript
const updatePreview = ref<FileUpdatePreview | null>(null)
const updateFile = ref<File | null>(null)
const updateOpen = ref(false)
const updateInput = ref<HTMLInputElement>()
```

**Add function**:

```typescript
async function handleUpdateFile(e: Event) {
  const input = e.target as HTMLInputElement
  const selected = input.files?.[0]
  input.value = ''
  if (!selected || !file.value) return

  extracting.value = true
  extractError.value = ''
  try {
    const preview = await files.previewUpdate(selected)
    if (preview) {
      updatePreview.value = preview
      updateFile.value = selected
      updateOpen.value = true
    } else {
      extractError.value = 'No existing file found to update.'
    }
  } catch {
    extractError.value = 'Failed to preview update.'
  } finally {
    extracting.value = false
  }
}

async function onUpdateConfirmed() {
  // Reload the file content after update
  if (file.value) {
    try {
      file.value = await files.getFileContent(file.value.id)
    } catch {
      router.replace('/files')
    }
  }
}
```

**Add hidden file input and button** in the template header, in the button group (after "Create card" button, before "Source" button):

```html
<input ref="updateInput" type="file" accept=".md" hidden @change="handleUpdateFile" />
<Button variant="outline" size="sm" class="h-7 text-xs" @click="updateInput?.click()">
  Update file
</Button>
```

**Add the dialog** at the end of the template (next to the existing `CardCreateDialog`):

```html
<FileUpdatePreviewDialog
  v-model:open="updateOpen"
  :preview="updatePreview"
  :file="updateFile"
  @confirmed="onUpdateConfirmed"
/>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/views/FileDetailView.vue
git commit -m "feat: add Update file button to FileDetailView"
```

---

## Task 8: Playwright Smoke Test

**Files:**
- No new files — use Playwright MCP

- [ ] **Step 1: Start the full stack**

```bash
./dev.sh
```

- [ ] **Step 2: Smoke test via Playwright**

Use Playwright MCP to:
1. Log in
2. Upload a test `.md` file with two sections
3. Create cards from both sections
4. Modify the test file (change one section, remove another, add a new one)
5. Upload the modified file — verify the update preview dialog appears
6. Check that it shows: 1 updated card, 1 orphaned card, 1 new section
7. Toggle the orphaned card to "Delete"
8. Confirm the update
9. Verify the updated card has new content
10. Verify the deleted card is gone from the cards list
11. Verify new section appears in the file detail heading tree
12. Test the "Update file" button from FileDetailView

- [ ] **Step 3: Commit any fixes discovered during testing**

---

## Task 9: Move Requirements to Done

**Files:**
- Move: `docs/requirements/03c_md_file_update.md` → `docs/requirements/done/`

- [ ] **Step 1: Move requirements file**

```bash
mv docs/requirements/03c_md_file_update.md docs/requirements/done/
git add docs/requirements/03c_md_file_update.md docs/requirements/done/03c_md_file_update.md
git commit -m "docs: move 03c_md_file_update.md to done/"
```
