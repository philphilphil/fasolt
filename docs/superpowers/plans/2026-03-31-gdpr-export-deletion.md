# GDPR Account Deletion & Data Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add data export and account deletion endpoints with Settings UI, fulfilling GDPR Articles 17 and 20.

**Architecture:** Single `AccountDataService` handles both export (build JSON) and deletion (clean up OpenIddict + delete user). Two new endpoints added to existing `AccountEndpoints`. New "Your Data" card section in the Settings page with export button and deletion dialog.

**Tech Stack:** .NET 10 (Minimal API, EF Core, ASP.NET Core Identity), Vue 3 + TypeScript, shadcn-vue Dialog components.

**Spec:** `docs/superpowers/specs/2026-03-31-gdpr-export-deletion-design.md`

---

## File Structure

**Create:**
- `fasolt.Server/Application/Services/AccountDataService.cs` — export + deletion logic
- `fasolt.Server/Application/Dtos/AccountDataDtos.cs` — export JSON DTOs, delete request DTO
- `fasolt.client/src/components/DeleteAccountDialog.vue` — confirmation dialog
- `fasolt.Tests/AccountDataServiceTests.cs` — service tests

**Modify:**
- `fasolt.Server/Application/Dtos/AccountDtos.cs` — add `DeleteAccountRequest` record
- `fasolt.Server/Api/Endpoints/AccountEndpoints.cs` — add export + delete endpoints
- `fasolt.client/src/views/SettingsView.vue` — add "Your Data" section
- `fasolt.client/src/stores/auth.ts` — add `deleteAccount` and `exportData` methods

---

### Task 1: AccountDataService — Export

**Files:**
- Create: `fasolt.Server/Application/Dtos/AccountDataDtos.cs`
- Create: `fasolt.Server/Application/Services/AccountDataService.cs`
- Test: `fasolt.Tests/AccountDataServiceTests.cs`

- [ ] **Step 1: Create export DTOs**

Create `fasolt.Server/Application/Dtos/AccountDataDtos.cs`:

```csharp
namespace Fasolt.Server.Application.Dtos;

public record AccountExport(
    DateTimeOffset ExportedAt,
    AccountExportProfile Account,
    List<AccountExportDeck> Decks,
    List<AccountExportCard> Cards,
    List<string> Sources,
    List<AccountExportSnapshot> Snapshots,
    List<AccountExportConsentGrant> ConsentGrants,
    AccountExportDeviceToken? DeviceToken
);

public record AccountExportProfile(
    string Email,
    bool EmailConfirmed,
    string? ExternalProvider,
    double? DesiredRetention,
    int? MaximumInterval,
    int NotificationIntervalHours
);

public record AccountExportDeck(
    string Name,
    string? Description,
    bool IsSuspended,
    DateTimeOffset CreatedAt,
    List<string> Cards
);

public record AccountExportCard(
    string PublicId,
    string Front,
    string Back,
    string? FrontSvg,
    string? BackSvg,
    string? SourceFile,
    string? SourceHeading,
    string State,
    double? Stability,
    double? Difficulty,
    int? Step,
    DateTimeOffset? DueAt,
    DateTimeOffset? LastReviewedAt,
    bool IsSuspended,
    DateTimeOffset CreatedAt
);

public record AccountExportSnapshot(
    string? DeckName,
    int Version,
    int CardCount,
    string Data,
    DateTimeOffset CreatedAt
);

public record AccountExportConsentGrant(string ClientId, DateTimeOffset GrantedAt);

public record AccountExportDeviceToken(string Token, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record DeleteAccountRequest(string? Password, string? ConfirmEmail);
```

- [ ] **Step 2: Write the failing test for ExportUserData**

Create `fasolt.Tests/AccountDataServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class AccountDataServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task ExportUserData_ReturnsAllUserData()
    {
        // Seed test data
        await using (var db = _db.CreateDbContext())
        {
            var deck = new Deck
            {
                Id = Guid.NewGuid(),
                PublicId = "deck00000001",
                UserId = UserId,
                Name = "Test Deck",
                Description = "A test deck",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Decks.Add(deck);

            var card = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = "card00000001",
                UserId = UserId,
                Front = "What is X?",
                Back = "X is Y.",
                SourceFile = "notes.md",
                SourceHeading = "Intro",
                State = "new",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Cards.Add(card);
            db.DeckCards.Add(new DeckCard { DeckId = deck.Id, CardId = card.Id });
            await db.SaveChangesAsync();
        }

        await using var ctx = _db.CreateDbContext();
        var svc = new AccountDataService(ctx);
        var user = await ctx.Users.FirstAsync(u => u.Id == UserId);

        var export = await svc.ExportUserData(user);

        export.Account.Email.Should().Be("test@fasolt.test");
        export.Cards.Should().HaveCount(1);
        export.Cards[0].Front.Should().Be("What is X?");
        export.Cards[0].PublicId.Should().Be("card00000001");
        export.Decks.Should().HaveCount(1);
        export.Decks[0].Name.Should().Be("Test Deck");
        export.Decks[0].Cards.Should().Contain("card00000001");
        export.Sources.Should().Contain("notes.md");
        export.ExportedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test fasolt.Tests --filter "AccountDataServiceTests.ExportUserData_ReturnsAllUserData" -v n`
Expected: FAIL — `AccountDataService` does not exist yet.

- [ ] **Step 4: Implement AccountDataService.ExportUserData**

Create `fasolt.Server/Application/Services/AccountDataService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class AccountDataService(AppDbContext db)
{
    public async Task<AccountExport> ExportUserData(AppUser user)
    {
        var userId = user.Id;

        var cards = await db.Cards
            .Where(c => c.UserId == userId)
            .Include(c => c.DeckCards)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var decks = await db.Decks
            .Where(d => d.UserId == userId)
            .Include(d => d.Cards)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        var snapshots = await db.DeckSnapshots
            .Where(s => s.UserId == userId)
            .Include(s => s.Deck)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        var consentGrants = await db.ConsentGrants
            .Where(c => c.UserId == userId)
            .ToListAsync();

        var deviceToken = await db.DeviceTokens
            .FirstOrDefaultAsync(d => d.UserId == userId);

        var cardPublicIdMap = cards.ToDictionary(c => c.Id, c => c.PublicId);

        return new AccountExport(
            ExportedAt: DateTimeOffset.UtcNow,
            Account: new AccountExportProfile(
                Email: user.Email!,
                EmailConfirmed: user.EmailConfirmed,
                ExternalProvider: user.ExternalProvider,
                DesiredRetention: user.DesiredRetention,
                MaximumInterval: user.MaximumInterval,
                NotificationIntervalHours: user.NotificationIntervalHours
            ),
            Decks: decks.Select(d => new AccountExportDeck(
                Name: d.Name,
                Description: d.Description,
                IsSuspended: d.IsSuspended,
                CreatedAt: d.CreatedAt,
                Cards: d.Cards.Select(dc => cardPublicIdMap.GetValueOrDefault(dc.CardId, "")).Where(id => id != "").ToList()
            )).ToList(),
            Cards: cards.Select(c => new AccountExportCard(
                PublicId: c.PublicId,
                Front: c.Front,
                Back: c.Back,
                FrontSvg: c.FrontSvg,
                BackSvg: c.BackSvg,
                SourceFile: c.SourceFile,
                SourceHeading: c.SourceHeading,
                State: c.State,
                Stability: c.Stability,
                Difficulty: c.Difficulty,
                Step: c.Step,
                DueAt: c.DueAt,
                LastReviewedAt: c.LastReviewedAt,
                IsSuspended: c.IsSuspended,
                CreatedAt: c.CreatedAt
            )).ToList(),
            Sources: cards.Where(c => c.SourceFile != null).Select(c => c.SourceFile!).Distinct().OrderBy(s => s).ToList(),
            Snapshots: snapshots.Select(s => new AccountExportSnapshot(
                DeckName: s.Deck?.Name,
                Version: s.Version,
                CardCount: s.CardCount,
                Data: s.Data,
                CreatedAt: s.CreatedAt
            )).ToList(),
            ConsentGrants: consentGrants.Select(c => new AccountExportConsentGrant(c.ClientId, c.GrantedAt)).ToList(),
            DeviceToken: deviceToken is null ? null : new AccountExportDeviceToken(deviceToken.Token, deviceToken.CreatedAt, deviceToken.UpdatedAt)
        );
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test fasolt.Tests --filter "AccountDataServiceTests.ExportUserData_ReturnsAllUserData" -v n`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Application/Dtos/AccountDataDtos.cs fasolt.Server/Application/Services/AccountDataService.cs fasolt.Tests/AccountDataServiceTests.cs
git commit -m "feat: add AccountDataService with export logic and tests (#73)"
```

---

### Task 2: AccountDataService — Delete

**Files:**
- Modify: `fasolt.Server/Application/Services/AccountDataService.cs`
- Test: `fasolt.Tests/AccountDataServiceTests.cs`

- [ ] **Step 1: Write the failing test for DeleteUserData**

Add to `fasolt.Tests/AccountDataServiceTests.cs`:

```csharp
[Fact]
public async Task DeleteUserData_RemovesAllUserEntities()
{
    // Seed test data
    await using (var db = _db.CreateDbContext())
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = "deck00000002",
            UserId = UserId,
            Name = "Doomed Deck",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Decks.Add(deck);

        var card = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = "card00000002",
            UserId = UserId,
            Front = "Gone?",
            Back = "Gone.",
            State = "new",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Cards.Add(card);
        db.DeckCards.Add(new DeckCard { DeckId = deck.Id, CardId = card.Id });

        db.DeckSnapshots.Add(new DeckSnapshot
        {
            Id = Guid.NewGuid(),
            PublicId = "snap00000001",
            DeckId = deck.Id,
            UserId = UserId,
            Version = 1,
            CardCount = 1,
            Data = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        db.ConsentGrants.Add(new ConsentGrant
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            ClientId = "test-client",
            GrantedAt = DateTimeOffset.UtcNow,
        });

        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = UserId,
            Token = "test-token",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    await using var ctx = _db.CreateDbContext();
    var svc = new AccountDataService(ctx);

    await svc.DeleteUserData(UserId);

    await using var verify = _db.CreateDbContext();
    (await verify.Users.AnyAsync(u => u.Id == UserId)).Should().BeFalse();
    (await verify.Cards.AnyAsync(c => c.UserId == UserId)).Should().BeFalse();
    (await verify.Decks.AnyAsync(d => d.UserId == UserId)).Should().BeFalse();
    (await verify.DeckSnapshots.AnyAsync(s => s.UserId == UserId)).Should().BeFalse();
    (await verify.ConsentGrants.AnyAsync(c => c.UserId == UserId)).Should().BeFalse();
    (await verify.DeviceTokens.AnyAsync(d => d.UserId == UserId)).Should().BeFalse();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test fasolt.Tests --filter "AccountDataServiceTests.DeleteUserData_RemovesAllUserEntities" -v n`
Expected: FAIL — `DeleteUserData` method does not exist.

- [ ] **Step 3: Implement DeleteUserData**

Add to `fasolt.Server/Application/Services/AccountDataService.cs`, inside the `AccountDataService` class:

```csharp
public async Task DeleteUserData(string userId)
{
    // Clean up OpenIddict tokens and authorizations (not cascade-deleted)
    await db.Database.ExecuteSqlInterpolatedAsync(
        $"""DELETE FROM "OpenIddictTokens" WHERE "Subject" = {userId}""");
    await db.Database.ExecuteSqlInterpolatedAsync(
        $"""DELETE FROM "OpenIddictAuthorizations" WHERE "Subject" = {userId}""");

    // Delete the user — cascade handles cards, decks, snapshots, consent grants, device tokens
    var user = await db.Users.FindAsync(userId);
    if (user is not null)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test fasolt.Tests --filter "AccountDataServiceTests.DeleteUserData_RemovesAllUserEntities" -v n`
Expected: PASS

Note: The OpenIddict SQL deletes will be no-ops in tests (tables exist via `EnsureCreated` but have no rows). The cascade behavior is the critical path being tested.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Application/Services/AccountDataService.cs fasolt.Tests/AccountDataServiceTests.cs
git commit -m "feat: add account deletion to AccountDataService (#73)"
```

---

### Task 3: API Endpoints

**Files:**
- Modify: `fasolt.Server/Application/Dtos/AccountDtos.cs`
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`

- [ ] **Step 1: Add DeleteAccountRequest DTO**

Add to the end of `fasolt.Server/Application/Dtos/AccountDtos.cs`:

```csharp
public record DeleteAccountRequest(string? Password, string? ConfirmEmail);
```

- [ ] **Step 2: Register the export and delete endpoints**

In `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`, add two lines inside `MapAccountEndpoints` after the existing GitHub callback line:

```csharp
group.MapPost("/export", ExportData).RequireAuthorization("EmailVerified");
group.MapDelete("/", DeleteAccount).RequireAuthorization("EmailVerified");
```

- [ ] **Step 3: Implement the ExportData handler**

Add to `AccountEndpoints.cs` as a private static method:

```csharp
private static async Task<IResult> ExportData(
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager,
    AccountDataService accountDataService)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();

    var export = await accountDataService.ExportUserData(user);
    var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(export,
        new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, WriteIndented = true });

    var date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
    return Results.File(json, "application/json", $"fasolt-export-{date}.json");
}
```

- [ ] **Step 4: Implement the DeleteAccount handler**

Add to `AccountEndpoints.cs` as a private static method:

```csharp
private static async Task<IResult> DeleteAccount(
    DeleteAccountRequest request,
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    AccountDataService accountDataService)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();

    if (user.ExternalProvider is not null)
    {
        // GitHub accounts: confirm by email
        if (string.IsNullOrEmpty(request.ConfirmEmail) ||
            !string.Equals(request.ConfirmEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["confirmEmail"] = ["Email does not match your account."]
            });
    }
    else
    {
        // Local accounts: confirm by password
        if (string.IsNullOrEmpty(request.Password) || !await userManager.CheckPasswordAsync(user, request.Password))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = ["Password is incorrect."]
            });
    }

    await accountDataService.DeleteUserData(user.Id);
    await signInManager.SignOutAsync();
    return Results.Ok();
}
```

- [ ] **Step 5: Add the required using statement**

Add to the top of `AccountEndpoints.cs`:

```csharp
using Fasolt.Server.Application.Services;
```

- [ ] **Step 6: Register AccountDataService in DI**

In `fasolt.Server/Program.cs`, find where other services are registered (search for `builder.Services.AddScoped`) and add:

```csharp
builder.Services.AddScoped<AccountDataService>();
```

- [ ] **Step 7: Run all tests to verify nothing is broken**

Run: `dotnet test fasolt.Tests -v n`
Expected: All tests PASS.

- [ ] **Step 8: Commit**

```bash
git add fasolt.Server/Application/Dtos/AccountDtos.cs fasolt.Server/Api/Endpoints/AccountEndpoints.cs fasolt.Server/Program.cs
git commit -m "feat: add export and delete account API endpoints (#73)"
```

---

### Task 4: Frontend — Auth Store Methods

**Files:**
- Modify: `fasolt.client/src/stores/auth.ts`

- [ ] **Step 1: Add exportData method to auth store**

Add inside the `useAuthStore` setup function, after the `resendVerification` function:

```typescript
async function exportData() {
  const response = await fetch('/api/account/export', {
    credentials: 'include',
  })
  if (!response.ok) throw new Error('Export failed')
  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = response.headers.get('content-disposition')?.match(/filename="?(.+?)"?$/)?.[1] ?? 'fasolt-export.json'
  a.click()
  URL.revokeObjectURL(url)
}
```

Note: We use raw `fetch` instead of `apiFetch` because `apiFetch` parses JSON — we need the raw blob for file download.

- [ ] **Step 2: Add deleteAccount method to auth store**

Add after `exportData`:

```typescript
async function deleteAccount(password?: string, confirmEmail?: string) {
  await apiFetch('/account', {
    method: 'DELETE',
    body: JSON.stringify({ password, confirmEmail }),
  })
  user.value = null
}
```

- [ ] **Step 3: Export the new methods from the store's return**

Update the return statement to include:

```typescript
return {
  // ... existing exports ...
  exportData,
  deleteAccount,
}
```

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/stores/auth.ts
git commit -m "feat: add exportData and deleteAccount to auth store (#73)"
```

---

### Task 5: Frontend — Delete Account Dialog

**Files:**
- Create: `fasolt.client/src/components/DeleteAccountDialog.vue`

- [ ] **Step 1: Create the dialog component**

Create `fasolt.client/src/components/DeleteAccountDialog.vue`:

```vue
<script setup lang="ts">
import { ref, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'
import { isApiError } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const auth = useAuthStore()
const router = useRouter()
const password = ref('')
const confirmEmail = ref('')
const error = ref('')
const deleting = ref(false)

const isExternal = computed(() => auth.isExternalAccount)

async function confirmDelete() {
  error.value = ''
  deleting.value = true
  try {
    if (isExternal.value) {
      await auth.deleteAccount(undefined, confirmEmail.value)
    } else {
      await auth.deleteAccount(password.value)
    }
    emit('update:open', false)
    router.push('/')
  } catch (e) {
    if (isApiError(e) && e.errors) {
      error.value = Object.values(e.errors).flat().join(' ')
    } else {
      error.value = 'Failed to delete account.'
    }
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent>
      <DialogHeader>
        <DialogTitle>Delete account</DialogTitle>
        <DialogDescription>
          This action is permanent and cannot be undone. All your cards, decks, and study progress will be deleted.
        </DialogDescription>
      </DialogHeader>
      <div class="flex flex-col gap-3">
        <div v-if="error" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ error }}</div>
        <div v-if="isExternal" class="flex flex-col gap-1.5">
          <label for="confirm-email" class="text-xs font-medium">Type your email to confirm</label>
          <Input id="confirm-email" v-model="confirmEmail" type="email" placeholder="your@email.com" />
        </div>
        <div v-else class="flex flex-col gap-1.5">
          <label for="delete-password" class="text-xs font-medium">Enter your password to confirm</label>
          <Input id="delete-password" v-model="password" type="password" autocomplete="off" />
        </div>
      </div>
      <DialogFooter>
        <Button variant="outline" @click="emit('update:open', false)">Cancel</Button>
        <Button variant="destructive" :disabled="deleting" @click="confirmDelete">
          {{ deleting ? 'Deleting...' : 'Delete my account' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/components/DeleteAccountDialog.vue
git commit -m "feat: add DeleteAccountDialog component (#73)"
```

---

### Task 6: Frontend — Settings "Your Data" Section

**Files:**
- Modify: `fasolt.client/src/views/SettingsView.vue`

- [ ] **Step 1: Add imports and state for the new section**

In `SettingsView.vue`, add to the `<script setup>` imports:

```typescript
import DeleteAccountDialog from '@/components/DeleteAccountDialog.vue'
```

Add to the script section (after the existing refs):

```typescript
const exporting = ref(false)
const exportError = ref('')
const deleteDialogOpen = ref(false)

async function exportData() {
  exporting.value = true
  exportError.value = ''
  try {
    await auth.exportData()
  } catch {
    exportError.value = 'Failed to export data. Please try again.'
  } finally {
    exporting.value = false
  }
}
```

- [ ] **Step 2: Add the "Your Data" card to the template**

Add before the closing `</div>` of the root template element (after the email/password grid):

```html
<Card class="border-border/60">
  <CardHeader>
    <CardTitle class="text-sm">Your data</CardTitle>
  </CardHeader>
  <CardContent class="flex flex-col gap-3">
    <p class="text-xs text-muted-foreground">
      Download a copy of all your data (cards, decks, study progress, snapshots) as a JSON file, or permanently delete your account.
    </p>
    <div v-if="exportError" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ exportError }}</div>
    <div class="flex gap-2">
      <Button size="sm" class="text-xs" :disabled="exporting" @click="exportData">
        {{ exporting ? 'Exporting...' : 'Export data' }}
      </Button>
      <Button size="sm" variant="destructive" class="text-xs" @click="deleteDialogOpen = true">
        Delete account
      </Button>
    </div>
  </CardContent>
</Card>

<DeleteAccountDialog v-model:open="deleteDialogOpen" />
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/SettingsView.vue
git commit -m "feat: add 'Your Data' section to Settings page (#73)"
```

---

### Task 7: End-to-End Testing with Playwright

**Files:** None (browser testing)

- [ ] **Step 1: Start the full stack**

Ensure the dev stack is running: `./dev.sh` (or verify backend + frontend are up).

- [ ] **Step 2: Test the export flow**

Using Playwright MCP:
1. Navigate to the app, log in as the dev seed user (`dev@fasolt.local` / `Dev1234!`)
2. Navigate to `/settings`
3. Scroll to the "Your data" section
4. Click "Export data"
5. Verify the download is triggered (check network response is `200` with JSON content-type)

- [ ] **Step 3: Test the delete account flow — wrong password**

1. Click "Delete account"
2. Verify the dialog opens with password input and warning text
3. Enter a wrong password
4. Click "Delete my account"
5. Verify an error message appears ("Password is incorrect")

- [ ] **Step 4: Test the delete account flow — cancel**

1. Click "Delete account" again
2. Click "Cancel"
3. Verify the dialog closes and account is intact

- [ ] **Step 5: Test the delete account flow — success**

1. Register a throwaway account first (or use the delete flow on a test account)
2. Navigate to `/settings`
3. Click "Delete account"
4. Enter the correct password
5. Click "Delete my account"
6. Verify redirect to the landing page
7. Verify the user is logged out (navigating to `/settings` redirects to login)

- [ ] **Step 6: Commit any test artifacts or fixes**

```bash
git add -A
git commit -m "test: verify GDPR export and deletion flows (#73)"
```
