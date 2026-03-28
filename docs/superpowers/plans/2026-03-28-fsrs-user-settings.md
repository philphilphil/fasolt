# FSRS User Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users configure FSRS scheduling parameters (desired retention, maximum interval) from web and iOS settings.

**Architecture:** Add nullable columns to `AppUser`, expose via `GET/PUT /api/settings/scheduling`, construct per-request FSRS schedulers in `ReviewService` using user values, and add UI sections to both web and iOS settings pages.

**Tech Stack:** .NET 10, EF Core, Vue 3 + shadcn-vue, Swift/SwiftUI, FSRS.Core

---

### Task 1: Add columns to AppUser and create migration

**Files:**
- Modify: `fasolt.Server/Domain/Entities/AppUser.cs`
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs:89-92`
- Create: EF Core migration (auto-generated)

- [ ] **Step 1: Add properties to AppUser**

In `fasolt.Server/Domain/Entities/AppUser.cs`, add the two nullable properties:

```csharp
using Microsoft.AspNetCore.Identity;

namespace Fasolt.Server.Domain.Entities;

public class AppUser : IdentityUser
{
    public int NotificationIntervalHours { get; set; } = 8;
    public DateTimeOffset? LastNotifiedAt { get; set; }
    public double? DesiredRetention { get; set; }
    public int? MaximumInterval { get; set; }
}
```

- [ ] **Step 2: Configure in AppDbContext**

In `fasolt.Server/Infrastructure/Data/AppDbContext.cs`, inside the `AppUser` entity configuration block, add the new columns:

```csharp
builder.Entity<AppUser>(entity =>
{
    entity.Property(e => e.NotificationIntervalHours).HasDefaultValue(8);
    entity.Property(e => e.DesiredRetention).HasDefaultValue(null);
    entity.Property(e => e.MaximumInterval).HasDefaultValue(null);
});
```

- [ ] **Step 3: Create migration**

Run:
```bash
dotnet ef migrations add AddFsrsUserSettings --project fasolt.Server
```

- [ ] **Step 4: Verify migration applies**

Run:
```bash
dotnet ef database update --project fasolt.Server
```

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Domain/Entities/AppUser.cs fasolt.Server/Infrastructure/Data/AppDbContext.cs fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat: add DesiredRetention and MaximumInterval columns to AppUser (#38)"
```

---

### Task 2: Create scheduling settings service and API endpoint

**Files:**
- Create: `fasolt.Server/Application/Dtos/SchedulingSettingsDtos.cs`
- Create: `fasolt.Server/Application/Services/SchedulingSettingsService.cs`
- Create: `fasolt.Server/Api/Endpoints/SchedulingSettingsEndpoints.cs`
- Modify: `fasolt.Server/Program.cs` (register endpoint)

- [ ] **Step 1: Write the failing test**

Create `fasolt.Tests/SchedulingSettingsServiceTests.cs`:

```csharp
using FluentAssertions;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class SchedulingSettingsServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GetSettings_ReturnsDefaults_WhenNotCustomized()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.GetSettings(UserId);

        result.DesiredRetention.Should().Be(0.9);
        result.MaximumInterval.Should().Be(36500);
    }

    [Fact]
    public async Task UpdateSettings_SavesAndReturnsValues()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.85, 365);

        result.Should().NotBeNull();
        result!.DesiredRetention.Should().Be(0.85);
        result.MaximumInterval.Should().Be(365);
    }

    [Fact]
    public async Task UpdateSettings_PersistsValues()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);
        await svc.UpdateSettings(UserId, 0.85, 365);

        await using var db2 = _db.CreateDbContext();
        var svc2 = new SchedulingSettingsService(db2);
        var result = await svc2.GetSettings(UserId);

        result.DesiredRetention.Should().Be(0.85);
        result.MaximumInterval.Should().Be(365);
    }

    [Fact]
    public async Task UpdateSettings_RejectsRetentionTooLow()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.5, 365);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsRetentionTooHigh()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.99, 365);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsIntervalTooLow()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.9, 0);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsIntervalTooHigh()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.9, 40000);

        result.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~SchedulingSettingsServiceTests" -v n`
Expected: Build error — `SchedulingSettingsService` does not exist.

- [ ] **Step 3: Create DTOs**

Create `fasolt.Server/Application/Dtos/SchedulingSettingsDtos.cs`:

```csharp
namespace Fasolt.Server.Application.Dtos;

public record SchedulingSettingsResponse(double DesiredRetention, int MaximumInterval);
public record UpdateSchedulingSettingsRequest(double DesiredRetention, int MaximumInterval);
```

- [ ] **Step 4: Create service**

Create `fasolt.Server/Application/Services/SchedulingSettingsService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class SchedulingSettingsService(AppDbContext db)
{
    public const double DefaultRetention = 0.9;
    public const int DefaultMaxInterval = 36500;

    public async Task<SchedulingSettingsResponse> GetSettings(string userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        return new SchedulingSettingsResponse(
            user.DesiredRetention ?? DefaultRetention,
            user.MaximumInterval ?? DefaultMaxInterval);
    }

    public async Task<SchedulingSettingsResponse?> UpdateSettings(string userId, double desiredRetention, int maximumInterval)
    {
        if (desiredRetention < 0.70 || desiredRetention > 0.97)
            return null;
        if (maximumInterval < 1 || maximumInterval > 36500)
            return null;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return null;

        user.DesiredRetention = desiredRetention;
        user.MaximumInterval = maximumInterval;
        await db.SaveChangesAsync();

        return new SchedulingSettingsResponse(desiredRetention, maximumInterval);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~SchedulingSettingsServiceTests" -v n`
Expected: All 7 tests PASS.

- [ ] **Step 6: Create API endpoint**

Create `fasolt.Server/Api/Endpoints/SchedulingSettingsEndpoints.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class SchedulingSettingsEndpoints
{
    public static void MapSchedulingSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings/scheduling").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/", GetSettings);
        group.MapPut("/", UpdateSettings);
    }

    private static async Task<IResult> GetSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        SchedulingSettingsService service)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var settings = await service.GetSettings(user.Id);
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        SchedulingSettingsService service,
        UpdateSchedulingSettingsRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await service.UpdateSettings(user.Id, request.DesiredRetention, request.MaximumInterval);
        if (result is null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["desiredRetention"] = ["Must be between 0.70 and 0.97."],
                ["maximumInterval"] = ["Must be between 1 and 36500."],
            });

        return Results.Ok(result);
    }
}
```

- [ ] **Step 7: Register endpoint and service in Program.cs**

In `fasolt.Server/Program.cs`, add the service registration near other `AddScoped` calls:

```csharp
builder.Services.AddScoped<SchedulingSettingsService>();
```

And add the endpoint mapping near other `Map*Endpoints()` calls:

```csharp
app.MapSchedulingSettingsEndpoints();
```

- [ ] **Step 8: Commit**

```bash
git add fasolt.Server/Application/Dtos/SchedulingSettingsDtos.cs fasolt.Server/Application/Services/SchedulingSettingsService.cs fasolt.Server/Api/Endpoints/SchedulingSettingsEndpoints.cs fasolt.Server/Program.cs fasolt.Tests/SchedulingSettingsServiceTests.cs
git commit -m "feat: add scheduling settings service and API endpoint (#38)"
```

---

### Task 3: Use per-user FSRS settings in ReviewService

**Files:**
- Modify: `fasolt.Server/Application/Services/ReviewService.cs:63-95`
- Modify: `fasolt.Tests/ReviewServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `fasolt.Tests/ReviewServiceTests.cs` — a new test that verifies custom retention is used. First, update the `CreateService` method and add the test:

```csharp
[Fact]
public async Task RateCard_UsesCustomRetention_WhenSet()
{
    await using var db = _db.CreateDbContext();

    // Set custom retention on user
    var user = await db.Users.FirstAsync(u => u.Id == UserId);
    user.DesiredRetention = 0.80;
    await db.SaveChangesAsync();

    var svc = CreateService(db);
    var cardId = await CreateCard(db, "Custom Q?", "Custom A.");

    // Rate easy to get into review state with a scheduled interval
    var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "easy"));
    result.Should().NotBeNull();
    result!.DueAt.Should().BeAfter(_time.GetUtcNow());

    // Now rate again with default retention for comparison
    await using var db2 = _db.CreateDbContext();
    var user2 = await db2.Users.FirstAsync(u => u.Id == UserId);
    user2.DesiredRetention = null; // reset to default
    await db2.SaveChangesAsync();

    await using var db3 = _db.CreateDbContext();
    var svc2 = CreateService(db3);
    var cardId2 = await CreateCard(db3, "Default Q?", "Default A.");
    var result2 = await svc2.RateCard(UserId, new RateCardRequest(cardId2, "easy"));

    // Lower retention (0.80) should produce longer intervals than default (0.9)
    var interval1 = result.DueAt!.Value - _time.GetUtcNow();
    var interval2 = result2!.DueAt!.Value - _time.GetUtcNow();
    interval1.Should().BeGreaterThan(interval2);
}
```

Add this `using` at the top of the file:
```csharp
using Microsoft.EntityFrameworkCore;
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~RateCard_UsesCustomRetention" -v n`
Expected: FAIL — `ReviewService` doesn't read user settings yet.

- [ ] **Step 3: Modify ReviewService to construct per-user scheduler**

Update `fasolt.Server/Application/Services/ReviewService.cs`. Change the constructor to accept `AppDbContext` and `TimeProvider` only (drop `IScheduler`), and build a scheduler per rating:

```csharp
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using FSRS.Core.Configurations;
using FSRS.Core.Enums;
using FSRS.Core.Interfaces;
using FSRS.Core.Services;
using FsrsCard = FSRS.Core.Models.Card;

namespace Fasolt.Server.Application.Services;

public class ReviewService(AppDbContext db, TimeProvider timeProvider)
{
    private static readonly Dictionary<string, Rating> ValidRatings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["again"] = Rating.Again,
        ["hard"] = Rating.Hard,
        ["good"] = Rating.Good,
        ["easy"] = Rating.Easy,
    };

    internal static string MapState(State state) => state switch
    {
        State.Learning => "learning",
        State.Review => "review",
        State.Relearning => "relearning",
        _ => "new",
    };

    internal static State ParseState(string state) => state switch
    {
        "learning" => State.Learning,
        "review" => State.Review,
        "relearning" => State.Relearning,
        _ => default,
    };

    private async Task<IScheduler> CreateSchedulerForUser(string userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var options = new SchedulerOptions
        {
            DesiredRetention = user.DesiredRetention ?? 0.9,
            MaximumInterval = user.MaximumInterval ?? 36500,
            EnableFuzzing = true,
        };
        return new SchedulerFactory(options).CreateScheduler();
    }

    public async Task<List<DueCardDto>> GetDueCards(string userId, int limit = 50, string? deckId = null)
    {
        // ... unchanged ...
    }

    public async Task<RateCardResponse?> RateCard(string userId, RateCardRequest request)
    {
        if (!ValidRatings.TryGetValue(request.Rating, out var fsrsRating))
            return null;

        var card = await db.Cards.FirstOrDefaultAsync(c => c.PublicId == request.CardId && c.UserId == userId);
        if (card is null) return null;

        var scheduler = await CreateSchedulerForUser(userId);

        var fsrsCard = card.State == "new"
            ? new FsrsCard { Due = card.DueAt?.UtcDateTime ?? card.CreatedAt.UtcDateTime }
            : new FsrsCard
            {
                State = ParseState(card.State),
                Stability = card.Stability,
                Difficulty = card.Difficulty,
                Step = card.Step,
                Due = card.DueAt?.UtcDateTime ?? card.CreatedAt.UtcDateTime,
                LastReview = card.LastReviewedAt?.UtcDateTime,
            };

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var (updated, _) = scheduler.ReviewCard(fsrsCard, fsrsRating, now, null);

        card.Stability = updated.Stability;
        card.Difficulty = updated.Difficulty;
        card.Step = updated.Step;
        card.State = MapState(updated.State);
        card.DueAt = new DateTimeOffset(updated.Due, TimeSpan.Zero);
        card.LastReviewedAt = timeProvider.GetUtcNow();

        await db.SaveChangesAsync();
        return new RateCardResponse(card.PublicId, card.Stability, card.Difficulty, card.DueAt, card.State);
    }

    public async Task<ReviewStatsDto> GetStats(string userId)
    {
        // ... unchanged ...
    }
}
```

- [ ] **Step 4: Update existing ReviewServiceTests to match new constructor**

In `fasolt.Tests/ReviewServiceTests.cs`, remove the `IScheduler` field and update `CreateService`:

Remove these lines from the constructor:
```csharp
private readonly IScheduler _scheduler;
// and the constructor body that creates it
```

Update `CreateService`:
```csharp
private ReviewService CreateService(Server.Infrastructure.Data.AppDbContext db)
    => new(db, _time);
```

Remove unused `using` statements for `FSRS.Core.Configurations`, `FSRS.Core.Interfaces`, `FSRS.Core.Services`.

The constructor should just be:
```csharp
public ReviewServiceTests() { }
```

- [ ] **Step 5: Run all review tests**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~ReviewServiceTests" -v n`
Expected: All tests PASS, including the new `RateCard_UsesCustomRetention_WhenSet`.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Application/Services/ReviewService.cs fasolt.Tests/ReviewServiceTests.cs
git commit -m "feat: use per-user FSRS settings in ReviewService (#38)"
```

---

### Task 4: Web UI — scheduling settings section

**Files:**
- Modify: `fasolt.client/src/views/SettingsView.vue`

- [ ] **Step 1: Add scheduling settings section to SettingsView.vue**

Add state variables, load function, and save function to the `<script setup>` block:

```typescript
const desiredRetention = ref(0.9)
const maximumInterval = ref(36500)
const schedulingSuccess = ref(false)
const schedulingError = ref('')
const schedulingLoading = ref(true)

async function loadSchedulingSettings() {
  schedulingLoading.value = true
  try {
    const settings = await apiFetch<{ desiredRetention: number; maximumInterval: number }>('/settings/scheduling')
    desiredRetention.value = settings.desiredRetention
    maximumInterval.value = settings.maximumInterval
  } catch {
    schedulingError.value = 'Failed to load scheduling settings.'
  } finally {
    schedulingLoading.value = false
  }
}

async function saveSchedulingSettings() {
  schedulingSuccess.value = false
  schedulingError.value = ''
  try {
    const settings = await apiFetch<{ desiredRetention: number; maximumInterval: number }>('/settings/scheduling', {
      method: 'PUT',
      body: JSON.stringify({
        desiredRetention: desiredRetention.value,
        maximumInterval: maximumInterval.value,
      }),
    })
    desiredRetention.value = settings.desiredRetention
    maximumInterval.value = settings.maximumInterval
    schedulingSuccess.value = true
  } catch (e) {
    if (isApiError(e) && e.errors) {
      schedulingError.value = Object.values(e.errors).flat().join(' ')
    } else {
      schedulingError.value = 'Failed to save scheduling settings.'
    }
  }
}

onMounted(() => {
  newEmail.value = auth.user?.email || ''
  loadSchedulingSettings()
})
```

Remove the existing `onMounted` that only sets `newEmail`.

Add the template section before the email card:

```html
<Card class="border-border/60">
  <CardHeader>
    <CardTitle class="text-sm">Scheduling</CardTitle>
  </CardHeader>
  <CardContent>
    <form class="flex flex-col gap-4" @submit.prevent="saveSchedulingSettings">
      <div v-if="schedulingLoading" class="text-xs text-muted-foreground">Loading...</div>
      <template v-else>
        <div v-if="schedulingSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-xs text-success">Settings saved.</div>
        <div v-if="schedulingError" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ schedulingError }}</div>

        <div class="flex flex-col gap-1.5">
          <label for="desired-retention" class="text-xs font-medium">Desired retention</label>
          <Input id="desired-retention" v-model.number="desiredRetention" type="number" min="0.70" max="0.97" step="0.01" required />
          <p class="text-xs text-muted-foreground">
            How likely you want to remember a card when it comes up for review. Higher values (e.g. 0.95) mean more frequent reviews but stronger recall. Lower values (e.g. 0.85) mean fewer reviews but more forgetting. Changes apply to future reviews only — cards already scheduled keep their current due dates.
          </p>
        </div>

        <div class="flex flex-col gap-1.5">
          <label for="maximum-interval" class="text-xs font-medium">Maximum interval (days)</label>
          <Input id="maximum-interval" v-model.number="maximumInterval" type="number" min="1" max="36500" step="1" required />
          <p class="text-xs text-muted-foreground">
            The longest gap allowed between reviews, in days. For example, 365 means you'll see every card at least once a year. The default (36500 days ≈ 100 years) means there's effectively no cap.
          </p>
        </div>

        <Button type="submit" size="sm" class="self-start text-xs">Save scheduling settings</Button>
      </template>
    </form>
  </CardContent>
</Card>
```

- [ ] **Step 2: Verify the page compiles**

Run: `cd fasolt.client && npx vue-tsc --noEmit`
Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/SettingsView.vue
git commit -m "feat(web): add scheduling settings UI to settings page (#38)"
```

---

### Task 5: iOS — scheduling settings section

**Files:**
- Create: `fasolt.ios/Fasolt/ViewModels/SchedulingSettingsViewModel.swift`
- Modify: `fasolt.ios/Fasolt/Models/APIModels.swift`
- Modify: `fasolt.ios/Fasolt/Views/Settings/SettingsView.swift`
- Modify: `fasolt.ios/Fasolt/Views/MainTabView.swift:48-51`

- [ ] **Step 1: Add API models**

In `fasolt.ios/Fasolt/Models/APIModels.swift`, add at the end before the closing of the file:

```swift
// MARK: - Scheduling Settings

struct SchedulingSettingsResponse: Decodable, Sendable {
    let desiredRetention: Double
    let maximumInterval: Int
}

struct UpdateSchedulingSettingsRequest: Encodable, Sendable {
    let desiredRetention: Double
    let maximumInterval: Int
}
```

- [ ] **Step 2: Create SchedulingSettingsViewModel**

Create `fasolt.ios/Fasolt/ViewModels/SchedulingSettingsViewModel.swift`:

```swift
import Foundation

@MainActor
@Observable
final class SchedulingSettingsViewModel {
    var desiredRetention: Double = 0.9
    var maximumInterval: Int = 36500
    var isLoading = false
    var errorMessage: String?
    var successMessage: String?

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func load() async {
        isLoading = true
        errorMessage = nil

        do {
            let endpoint = Endpoint(path: "/api/settings/scheduling", method: .get)
            let response: SchedulingSettingsResponse = try await apiClient.request(endpoint)
            desiredRetention = response.desiredRetention
            maximumInterval = response.maximumInterval
        } catch {
            errorMessage = "Could not load scheduling settings."
        }

        isLoading = false
    }

    func save() async {
        isLoading = true
        errorMessage = nil
        successMessage = nil

        let endpoint = Endpoint(
            path: "/api/settings/scheduling",
            method: .put,
            body: UpdateSchedulingSettingsRequest(
                desiredRetention: desiredRetention,
                maximumInterval: maximumInterval
            )
        )

        do {
            let response: SchedulingSettingsResponse = try await apiClient.request(endpoint)
            desiredRetention = response.desiredRetention
            maximumInterval = response.maximumInterval
            successMessage = "Settings saved."
        } catch {
            errorMessage = "Could not save scheduling settings."
        }

        isLoading = false
    }
}
```

- [ ] **Step 3: Add scheduling section to SettingsView.swift**

Update `fasolt.ios/Fasolt/Views/Settings/SettingsView.swift`:

Add a new `@State` property at the top:
```swift
@State private var schedulingViewModel: SchedulingSettingsViewModel
```

Update `init` to accept and initialize it:
```swift
init(viewModel: SettingsViewModel, notificationViewModel: NotificationSettingsViewModel, schedulingViewModel: SchedulingSettingsViewModel) {
    _viewModel = State(initialValue: viewModel)
    _notificationViewModel = State(initialValue: notificationViewModel)
    _schedulingViewModel = State(initialValue: schedulingViewModel)
}
```

Add a new section in the `List` body, after the "Notifications" section and before the "Sign Out" section:

```swift
Section("Scheduling") {
    if schedulingViewModel.isLoading {
        HStack {
            Text("Loading...")
                .foregroundStyle(.secondary)
            Spacer()
            ProgressView()
        }
    } else {
        if let success = schedulingViewModel.successMessage {
            Text(success)
                .foregroundStyle(.green)
                .font(.caption)
        }
        if let error = schedulingViewModel.errorMessage {
            Text(error)
                .foregroundStyle(.red)
                .font(.caption)
        }

        VStack(alignment: .leading, spacing: 4) {
            HStack {
                Text("Desired retention")
                Spacer()
                Text(String(format: "%.0f%%", schedulingViewModel.desiredRetention * 100))
                    .foregroundStyle(.secondary)
            }
            Slider(
                value: $schedulingViewModel.desiredRetention,
                in: 0.70...0.97,
                step: 0.01
            )
            Text("How likely you want to remember a card when it comes up for review. Higher values mean more frequent reviews but stronger recall. Lower values mean fewer reviews but more forgetting. Changes apply to future reviews only.")
                .font(.caption)
                .foregroundStyle(.secondary)
        }

        VStack(alignment: .leading, spacing: 4) {
            HStack {
                Text("Maximum interval")
                Spacer()
                Text("\(schedulingViewModel.maximumInterval) days")
                    .foregroundStyle(.secondary)
            }
            TextField("Days", value: $schedulingViewModel.maximumInterval, format: .number)
                .keyboardType(.numberPad)
                .textFieldStyle(.roundedBorder)
            Text("The longest gap between reviews, in days. 365 means every card is seen at least once a year. Default is 36500 (≈ 100 years).")
                .font(.caption)
                .foregroundStyle(.secondary)
        }

        Button("Save") {
            Task { await schedulingViewModel.save() }
        }
    }
}
```

Update the `.task` modifier to also load scheduling settings:
```swift
.task {
    await viewModel.loadUserInfo()
    await notificationViewModel.load()
    await schedulingViewModel.load()
}
```

- [ ] **Step 4: Update MainTabView to pass schedulingViewModel**

In `fasolt.ios/Fasolt/Views/MainTabView.swift`, update the `SettingsView` instantiation:

```swift
SettingsView(
    viewModel: SettingsViewModel(apiClient: authService.apiClient),
    notificationViewModel: NotificationSettingsViewModel(apiClient: authService.apiClient),
    schedulingViewModel: SchedulingSettingsViewModel(apiClient: authService.apiClient)
)
```

- [ ] **Step 5: Build iOS project**

Run: `cd fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5`
Expected: BUILD SUCCEEDED.

- [ ] **Step 6: Commit**

```bash
git add fasolt.ios/Fasolt/ViewModels/SchedulingSettingsViewModel.swift fasolt.ios/Fasolt/Models/APIModels.swift fasolt.ios/Fasolt/Views/Settings/SettingsView.swift fasolt.ios/Fasolt/Views/MainTabView.swift
git commit -m "feat(ios): add scheduling settings to settings view (#38)"
```

---

### Task 6: Playwright end-to-end test

**Files:**
- No new files — uses Playwright MCP tools

- [ ] **Step 1: Start the full stack**

Ensure `./dev.sh` is running or start backend + frontend manually.

- [ ] **Step 2: Test the scheduling settings flow with Playwright**

Using Playwright MCP tools:
1. Navigate to the app and log in with `dev@fasolt.local` / `Dev1234!`
2. Navigate to `/settings`
3. Verify the "Scheduling" card is visible with "Desired retention" and "Maximum interval" inputs
4. Change desired retention to `0.85`
5. Change maximum interval to `365`
6. Click "Save scheduling settings"
7. Verify the success message "Settings saved." appears
8. Reload the page
9. Verify the values persisted (retention shows `0.85`, interval shows `365`)
10. Reset values back to defaults (`0.9` and `36500`) and save

- [ ] **Step 3: Commit any fixes if needed**

---

### Task 7: Run all tests and verify

- [ ] **Step 1: Run all backend tests**

Run: `dotnet test fasolt.Tests -v n`
Expected: All tests PASS.

- [ ] **Step 2: Run frontend type check**

Run: `cd fasolt.client && npx vue-tsc --noEmit`
Expected: No errors.

- [ ] **Step 3: Final commit if any cleanup was needed**
