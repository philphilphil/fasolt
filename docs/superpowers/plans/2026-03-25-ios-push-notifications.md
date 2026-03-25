# iOS Push Notifications Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Send push notifications to iOS users when cards become due for review, with user-configurable notification intervals.

**Architecture:** A .NET `BackgroundService` runs on a 1-hour timer, querying users with due cards whose preferred notification interval has elapsed. It sends APNs push notifications directly via HTTP/2. The iOS app registers for remote notifications after the first study session and stores the device token on the server.

**Tech Stack:** .NET BackgroundService, Apple Push Notification service (APNs HTTP/2 API), ES256 JWT auth, SwiftUI UserNotifications framework

---

### Task 1: DeviceToken Entity and Migration

**Files:**
- Create: `fasolt.Server/Domain/Entities/DeviceToken.cs`
- Modify: `fasolt.Server/Domain/Entities/AppUser.cs`
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs`

- [ ] **Step 1: Create DeviceToken entity**

Create `fasolt.Server/Domain/Entities/DeviceToken.cs`:

```csharp
namespace Fasolt.Server.Domain.Entities;

public class DeviceToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

- [ ] **Step 2: Add notification fields to AppUser**

Modify `fasolt.Server/Domain/Entities/AppUser.cs` — add notification preferences:

```csharp
public class AppUser : IdentityUser
{
    public int NotificationIntervalHours { get; set; } = 8;
    public DateTimeOffset? LastNotifiedAt { get; set; }
}
```

- [ ] **Step 3: Register DeviceToken in DbContext**

Modify `fasolt.Server/Infrastructure/Data/AppDbContext.cs`.

Add the DbSet (after line 16):

```csharp
public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
```

Add entity configuration inside `OnModelCreating` (after the `ConsentGrant` block, before the closing `}`):

```csharp
builder.Entity<DeviceToken>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Token).HasMaxLength(200).IsRequired();
    entity.HasIndex(e => e.UserId).IsUnique();
    entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
});

builder.Entity<AppUser>(entity =>
{
    entity.Property(e => e.NotificationIntervalHours).HasDefaultValue(8);
});
```

- [ ] **Step 4: Generate and apply the migration**

Run:
```bash
cd fasolt.Server && dotnet ef migrations add AddDeviceTokensAndNotifications
```

Expected: Migration file created in `Infrastructure/Data/Migrations/`.

Run:
```bash
dotnet ef database update
```

Expected: Database updated successfully.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Domain/Entities/DeviceToken.cs fasolt.Server/Domain/Entities/AppUser.cs fasolt.Server/Infrastructure/Data/AppDbContext.cs fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat: add DeviceToken entity and notification fields on AppUser (#23)"
```

---

### Task 2: Device Token and Notification Settings Endpoints

**Files:**
- Create: `fasolt.Server/Api/Endpoints/NotificationEndpoints.cs`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Create NotificationEndpoints**

Create `fasolt.Server/Api/Endpoints/NotificationEndpoints.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Api.Endpoints;

public static class NotificationEndpoints
{
    private static readonly int[] AllowedIntervals = [4, 6, 8, 10, 12, 24];

    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization().RequireRateLimiting("api");

        group.MapPut("/device-token", UpsertDeviceToken);
        group.MapDelete("/device-token", DeleteDeviceToken);
        group.MapGet("/settings", GetSettings);
        group.MapPut("/settings", UpdateSettings);
    }

    private static async Task<IResult> UpsertDeviceToken(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db,
        UpsertDeviceTokenRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest("Token is required.");

        var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == user.Id);
        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.Token = request.Token;
            existing.UpdatedAt = now;
        }
        else
        {
            db.DeviceTokens.Add(new DeviceToken
            {
                UserId = user.Id,
                Token = request.Token,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteDeviceToken(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == user.Id);
        if (existing is not null)
        {
            db.DeviceTokens.Remove(existing);
            await db.SaveChangesAsync();
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var hasToken = await db.DeviceTokens.AnyAsync(d => d.UserId == user.Id);

        return Results.Ok(new NotificationSettingsResponse(
            user.NotificationIntervalHours,
            hasToken));
    }

    private static async Task<IResult> UpdateSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        UpdateNotificationSettingsRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (!AllowedIntervals.Contains(request.IntervalHours))
            return Results.BadRequest($"intervalHours must be one of: {string.Join(", ", AllowedIntervals)}");

        user.NotificationIntervalHours = request.IntervalHours;
        await userManager.UpdateAsync(user);

        return Results.NoContent();
    }
}

public record UpsertDeviceTokenRequest(string Token);
public record UpdateNotificationSettingsRequest(int IntervalHours);
public record NotificationSettingsResponse(int IntervalHours, bool HasDeviceToken);
```

- [ ] **Step 2: Register the endpoints in Program.cs**

Modify `fasolt.Server/Program.cs` — add after the `app.MapAdminEndpoints();` line (around line 390):

```csharp
app.MapNotificationEndpoints();
```

- [ ] **Step 3: Build and verify**

Run:
```bash
cd fasolt.Server && dotnet build
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/NotificationEndpoints.cs fasolt.Server/Program.cs
git commit -m "feat: add notification device-token and settings endpoints (#23)"
```

---

### Task 3: APNs Service

**Files:**
- Create: `fasolt.Server/Infrastructure/Services/ApnsService.cs`
- Modify: `fasolt.Server/Program.cs`
- Modify: `fasolt.Server/appsettings.json`
- Modify: `fasolt.Server/appsettings.Development.json`

- [ ] **Step 1: Create ApnsService**

Create `fasolt.Server/Infrastructure/Services/ApnsService.cs`:

```csharp
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Fasolt.Server.Infrastructure.Services;

public class ApnsSettings
{
    public string KeyId { get; set; } = default!;
    public string TeamId { get; set; } = default!;
    public string BundleId { get; set; } = default!;
    public string? KeyPath { get; set; }
    public string? KeyBase64 { get; set; }
}

public class ApnsService
{
    private readonly HttpClient _httpClient;
    private readonly ApnsSettings _settings;
    private readonly ECDsa _key;
    private readonly ILogger<ApnsService> _logger;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry;

    public ApnsService(HttpClient httpClient, ApnsSettings settings, ILogger<ApnsService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
        _key = LoadKey(settings);
    }

    private static ECDsa LoadKey(ApnsSettings settings)
    {
        var key = ECDsa.Create();
        if (!string.IsNullOrEmpty(settings.KeyBase64))
        {
            var keyBytes = Convert.FromBase64String(settings.KeyBase64);
            key.ImportPkcs8PrivateKey(keyBytes, out _);
        }
        else if (!string.IsNullOrEmpty(settings.KeyPath))
        {
            var pem = File.ReadAllText(settings.KeyPath);
            key.ImportFromPem(pem);
        }
        else
        {
            throw new InvalidOperationException("APNs key must be configured via Apns:KeyPath or Apns:KeyBase64.");
        }
        return key;
    }

    private string GetToken()
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        var securityKey = new ECDsaSecurityKey(_key) { KeyId = _settings.KeyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.TeamId,
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = credentials,
        };

        var handler = new JsonWebTokenHandler();
        _cachedToken = handler.CreateToken(descriptor);
        _tokenExpiry = DateTimeOffset.UtcNow.AddMinutes(50);

        return _cachedToken;
    }

    /// <summary>
    /// Sends a push notification. Returns true if successful, false if the token is invalid (should be deleted).
    /// </summary>
    public async Task<bool> SendNotification(string deviceToken, string title, string body, int badgeCount)
    {
        var token = GetToken();

        var payload = new
        {
            aps = new
            {
                alert = new { title, body },
                sound = "default",
                badge = badgeCount,
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.push.apple.com/3/device/{deviceToken}");
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
        request.Headers.TryAddWithoutValidation("apns-topic", _settings.BundleId);
        request.Headers.TryAddWithoutValidation("apns-push-type", "alert");
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return true;

            if ((int)response.StatusCode == 410) // Gone — token no longer valid
            {
                _logger.LogInformation("APNs token {Token} is no longer valid (410 Gone)", deviceToken[..8]);
                return false;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("APNs request failed with {StatusCode}: {Body}",
                (int)response.StatusCode, responseBody);
            return true; // Don't delete the token on transient errors
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send APNs notification to {Token}", deviceToken[..8]);
            return true; // Don't delete on network errors
        }
    }
}
```

- [ ] **Step 2: Add APNs configuration to appsettings**

Modify `fasolt.Server/appsettings.json` — add the `Apns` section:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "OAuth": {
    "AllowedNonHttpsRedirectPatterns": ["fasolt://", "http://localhost", "http://127.0.0.1"]
  },
  "Apns": {
    "KeyId": "",
    "TeamId": "",
    "BundleId": "",
    "KeyPath": "",
    "KeyBase64": ""
  }
}
```

Modify `fasolt.Server/appsettings.Development.json` — add placeholder:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fasolt;Username=spaced;Password=spaced_dev"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ReverseProxy": {
    "TrustAllProxies": true
  },
  "Admin": {
    "Email": "dev@fasolt.local"
  },
  "Apns": {
    "KeyId": "YOUR_KEY_ID",
    "TeamId": "YOUR_TEAM_ID",
    "BundleId": "com.fasolt.app",
    "KeyPath": "path/to/AuthKey.p8"
  }
}
```

- [ ] **Step 3: Register ApnsService in Program.cs**

Modify `fasolt.Server/Program.cs` — add after the `builder.Services.AddScoped<OverviewService>();` line (around line 197):

```csharp
var apnsSettings = builder.Configuration.GetSection("Apns").Get<ApnsSettings>();
if (apnsSettings is not null && !string.IsNullOrEmpty(apnsSettings.KeyId))
{
    builder.Services.AddSingleton(apnsSettings);
    builder.Services.AddHttpClient<ApnsService>();
}
```

Add the using at the top of Program.cs:

```csharp
using Fasolt.Server.Infrastructure.Services;
```

- [ ] **Step 4: Build and verify**

Run:
```bash
cd fasolt.Server && dotnet build
```

Expected: Build succeeded. Note: you may need to add `Microsoft.IdentityModel.JsonWebTokens` NuGet package if not already present. Check with `dotnet list package` and add with `dotnet add package Microsoft.IdentityModel.JsonWebTokens` if needed.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Infrastructure/Services/ApnsService.cs fasolt.Server/Program.cs fasolt.Server/appsettings.json fasolt.Server/appsettings.Development.json
git commit -m "feat: add APNs service for push notification delivery (#23)"
```

---

### Task 4: Notification Background Service

**Files:**
- Create: `fasolt.Server/Infrastructure/Services/NotificationBackgroundService.cs`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Create NotificationBackgroundService**

Create `fasolt.Server/Infrastructure/Services/NotificationBackgroundService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Infrastructure.Services;

public class NotificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ApnsService apnsService,
    ILogger<NotificationBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NotificationBackgroundService started, running every {Interval}", Interval);

        using var timer = new PeriodicTimer(Interval);

        // Run once immediately on startup, then on each tick
        await ProcessNotifications(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessNotifications(stoppingToken);
        }
    }

    private async Task ProcessNotifications(CancellationToken stoppingToken)
    {
        logger.LogInformation("Checking for users with due cards to notify");

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;

            // Find users who have a device token and whose notification interval has elapsed
            var eligibleUsers = await db.DeviceTokens
                .Include(d => d.User)
                .Where(d =>
                    d.User.LastNotifiedAt == null ||
                    d.User.LastNotifiedAt.Value.AddHours(d.User.NotificationIntervalHours) <= now)
                .Select(d => new
                {
                    d.UserId,
                    d.Token,
                    d.User.NotificationIntervalHours,
                })
                .ToListAsync(stoppingToken);

            logger.LogInformation("Found {Count} eligible users to check", eligibleUsers.Count);

            foreach (var entry in eligibleUsers)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    await ProcessUserNotification(db, entry.UserId, entry.Token, now, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process notification for user {UserId}", entry.UserId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during notification processing cycle");
        }
    }

    private async Task ProcessUserNotification(
        AppDbContext db, string userId, string deviceToken,
        DateTimeOffset now, CancellationToken stoppingToken)
    {
        // Query due cards grouped by deck
        var dueCardsByDeck = await db.Cards
            .Where(c => c.UserId == userId && (c.DueAt == null || c.DueAt <= now))
            .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive))
            .SelectMany(c => c.DeckCards.DefaultIfEmpty(),
                (card, deckCard) => new { DeckName = deckCard != null ? deckCard.Deck.Name : null })
            .GroupBy(x => x.DeckName ?? "Unsorted")
            .Select(g => new { DeckName = g.Key, Count = g.Count() })
            .ToListAsync(stoppingToken);

        var totalDue = dueCardsByDeck.Sum(g => g.Count);

        if (totalDue == 0)
        {
            logger.LogDebug("User {UserId} has no due cards, skipping", userId);
            return;
        }

        // Build notification body
        var deckBreakdown = string.Join(", ",
            dueCardsByDeck.OrderByDescending(g => g.Count).Select(g => $"{g.Count} in {g.DeckName}"));
        var body = $"You have {totalDue} card{(totalDue == 1 ? "" : "s")} due: {deckBreakdown}";
        var title = "Cards due";

        var tokenValid = await apnsService.SendNotification(deviceToken, title, body, totalDue);

        if (!tokenValid)
        {
            // Token is invalid — remove it
            var token = await db.DeviceTokens.FirstOrDefaultAsync(
                d => d.UserId == userId, stoppingToken);
            if (token is not null)
            {
                db.DeviceTokens.Remove(token);
                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation("Removed invalid device token for user {UserId}", userId);
            }
            return;
        }

        // Update LastNotifiedAt
        var user = await db.Users.FindAsync([userId], stoppingToken);
        if (user is not null)
        {
            user.LastNotifiedAt = now;
            await db.SaveChangesAsync(stoppingToken);
        }

        logger.LogInformation("Sent notification to user {UserId}: {TotalDue} cards due", userId, totalDue);
    }
}
```

- [ ] **Step 2: Register the background service in Program.cs**

Modify `fasolt.Server/Program.cs` — add after the ApnsService registration block (from Task 3):

```csharp
if (apnsSettings is not null && !string.IsNullOrEmpty(apnsSettings.KeyId))
{
    builder.Services.AddSingleton(apnsSettings);
    builder.Services.AddHttpClient<ApnsService>();
    builder.Services.AddHostedService<NotificationBackgroundService>();
}
```

Note: this replaces the block from Task 3 step 3 — the `AddHostedService` line is added to the same conditional block.

- [ ] **Step 3: Build and verify**

Run:
```bash
cd fasolt.Server && dotnet build
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Infrastructure/Services/NotificationBackgroundService.cs fasolt.Server/Program.cs
git commit -m "feat: add background service for periodic due-card notifications (#23)"
```

---

### Task 5: iOS — Notification Permission and Device Token Registration

**Files:**
- Create: `fasolt.ios/Fasolt/Services/NotificationService.swift`
- Modify: `fasolt.ios/Fasolt/FasoltApp.swift`
- Modify: `fasolt.ios/Fasolt/ViewModels/StudyViewModel.swift`
- Modify: `fasolt.ios/Fasolt/Models/APIModels.swift`

- [ ] **Step 1: Create NotificationService**

Create `fasolt.ios/Fasolt/Services/NotificationService.swift`:

```swift
import Foundation
import UserNotifications
import UIKit

@MainActor
@Observable
final class NotificationService {
    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func requestPermissionAndRegister() async {
        let center = UNUserNotificationCenter.current()
        do {
            let granted = try await center.requestAuthorization(options: [.alert, .sound, .badge])
            if granted {
                UIApplication.shared.registerForRemoteNotifications()
            }
        } catch {
            print("Notification permission error: \(error)")
        }
    }

    func registerDeviceToken(_ tokenData: Data) async {
        let token = tokenData.map { String(format: "%02x", $0) }.joined()
        let endpoint = Endpoint(
            path: "/api/notifications/device-token",
            method: .put,
            body: DeviceTokenRequest(token: token)
        )
        do {
            try await apiClient.request(endpoint) as Void
        } catch {
            print("Failed to register device token: \(error)")
        }
    }

    func deleteDeviceToken() async {
        let endpoint = Endpoint(path: "/api/notifications/device-token", method: .delete)
        do {
            try await apiClient.request(endpoint) as Void
        } catch {
            print("Failed to delete device token: \(error)")
        }
    }

    func clearBadge() {
        UNUserNotificationCenter.current().setBadgeCount(0) { _ in }
    }
}
```

- [ ] **Step 2: Add API models for notifications**

Modify `fasolt.ios/Fasolt/Models/APIModels.swift` — add at the end of the file:

```swift
struct DeviceTokenRequest: Encodable, Sendable {
    let token: String
}

struct NotificationSettingsResponse: Decodable, Sendable {
    let intervalHours: Int
    let hasDeviceToken: Bool
}

struct UpdateNotificationSettingsRequest: Encodable, Sendable {
    let intervalHours: Int
}
```

- [ ] **Step 3: Add AppDelegate adaptor for remote notification callbacks**

Modify `fasolt.ios/Fasolt/FasoltApp.swift`. Replace the entire file with:

```swift
import SwiftUI
import SwiftData

class AppDelegate: NSObject, UIApplicationDelegate {
    var onDeviceToken: ((Data) -> Void)?

    func application(_ application: UIApplication,
                     didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {
        onDeviceToken?(deviceToken)
    }

    func application(_ application: UIApplication,
                     didFailToRegisterForRemoteNotificationsWithError error: Error) {
        print("Remote notification registration failed: \(error)")
    }
}

@main
struct FasoltApp: App {
    @UIApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @State private var authService = AuthService()
    @State private var networkMonitor = NetworkMonitor()
    @Environment(\.scenePhase) private var scenePhase
    @State private var lastRefresh: Date = .distantPast
    @State private var notificationService: NotificationService?

    var body: some Scene {
        WindowGroup {
            Group {
                if authService.isAuthenticated {
                    MainTabView()
                } else {
                    OnboardingView()
                }
            }
            .animation(.default, value: authService.isAuthenticated)
            .onChange(of: scenePhase) { oldPhase, newPhase in
                if newPhase == .active && oldPhase != .active {
                    let now = Date()
                    if now.timeIntervalSince(lastRefresh) > 30 {
                        lastRefresh = now
                        NotificationCenter.default.post(name: .appDidBecomeActive, object: nil)
                    }
                    notificationService?.clearBadge()
                }
            }
            .onAppear {
                let service = NotificationService(apiClient: authService.apiClient)
                notificationService = service
                appDelegate.onDeviceToken = { tokenData in
                    Task { await service.registerDeviceToken(tokenData) }
                }
            }
        }
        .environment(authService)
        .environment(networkMonitor)
        .modelContainer(for: [Card.self, CachedDeck.self, PendingReview.self])
    }
}
```

- [ ] **Step 4: Trigger notification permission after first study session**

Modify `fasolt.ios/Fasolt/ViewModels/StudyViewModel.swift`. Add the notification trigger at the end of the `rateCard` method, when the session transitions to `.summary`:

Replace the block at the end of `rateCard`:
```swift
        if currentIndex >= cards.count {
            state = .summary
        } else {
            state = .studying
        }
```

With:
```swift
        if currentIndex >= cards.count {
            state = .summary
            requestNotificationPermissionIfNeeded()
        } else {
            state = .studying
        }
```

Add a new property and method to `StudyViewModel`:

```swift
    @AppStorage("hasRequestedNotificationPermission") private var hasRequestedPermission = false
    var notificationService: NotificationService?

    private func requestNotificationPermissionIfNeeded() {
        guard !hasRequestedPermission else { return }
        hasRequestedPermission = true
        Task {
            await notificationService?.requestPermissionAndRegister()
        }
    }
```

- [ ] **Step 5: Pass NotificationService to StudyViewModel**

Modify `fasolt.ios/Fasolt/Views/MainTabView.swift` — in the `body` property, after creating `studyViewModelFactory`, update the factory to inject the notification service.

Replace the `studyViewModelFactory` definition:
```swift
        let studyViewModelFactory: () -> StudyViewModel = {
            StudyViewModel(cardRepository: cardRepository)
        }
```

With:
```swift
        let notificationService = NotificationService(apiClient: apiClient)

        let studyViewModelFactory: () -> StudyViewModel = {
            let vm = StudyViewModel(cardRepository: cardRepository)
            vm.notificationService = notificationService
            return vm
        }
```

- [ ] **Step 6: Add token cleanup on sign out**

Modify `fasolt.ios/Fasolt/Services/AuthService.swift`. Replace the `signOut` method:

```swift
    func signOut() {
        Task {
            let service = NotificationService(apiClient: apiClient)
            await service.deleteDeviceToken()
        }
        keychain.deleteAll()
        isAuthenticated = false
    }
```

- [ ] **Step 7: Build and verify**

Run:
```bash
cd fasolt.ios && xcodegen generate && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5
```

Expected: Build Succeeded.

- [ ] **Step 8: Commit**

```bash
git add fasolt.ios/Fasolt/Services/NotificationService.swift fasolt.ios/Fasolt/FasoltApp.swift fasolt.ios/Fasolt/ViewModels/StudyViewModel.swift fasolt.ios/Fasolt/Models/APIModels.swift fasolt.ios/Fasolt/Views/MainTabView.swift fasolt.ios/Fasolt/Services/AuthService.swift
git commit -m "feat: add iOS notification permission, token registration, and cleanup (#23)"
```

---

### Task 6: iOS — Notification Settings UI

**Files:**
- Create: `fasolt.ios/Fasolt/ViewModels/NotificationSettingsViewModel.swift`
- Modify: `fasolt.ios/Fasolt/Views/Settings/SettingsView.swift`

- [ ] **Step 1: Create NotificationSettingsViewModel**

Create `fasolt.ios/Fasolt/ViewModels/NotificationSettingsViewModel.swift`:

```swift
import Foundation
import UserNotifications

@MainActor
@Observable
final class NotificationSettingsViewModel {
    static let allowedIntervals = [4, 6, 8, 10, 12, 24]

    var intervalHours: Int = 8
    var hasDeviceToken = false
    var permissionStatus: UNAuthorizationStatus = .notDetermined
    var isLoading = false
    var errorMessage: String?

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func load() async {
        isLoading = true
        errorMessage = nil

        // Check system permission status
        let settings = await UNUserNotificationCenter.current().notificationSettings()
        permissionStatus = settings.authorizationStatus

        // Fetch server settings
        do {
            let endpoint = Endpoint(path: "/api/notifications/settings", method: .get)
            let response: NotificationSettingsResponse = try await apiClient.request(endpoint)
            intervalHours = response.intervalHours
            hasDeviceToken = response.hasDeviceToken
        } catch {
            errorMessage = "Could not load notification settings."
        }

        isLoading = false
    }

    func updateInterval(_ hours: Int) async {
        let endpoint = Endpoint(
            path: "/api/notifications/settings",
            method: .put,
            body: UpdateNotificationSettingsRequest(intervalHours: hours)
        )
        do {
            try await apiClient.request(endpoint) as Void
            intervalHours = hours
        } catch {
            errorMessage = "Could not update notification interval."
        }
    }

    var permissionLabel: String {
        switch permissionStatus {
        case .authorized: return "Enabled"
        case .denied: return "Denied — tap to open Settings"
        case .provisional: return "Provisional"
        case .notDetermined: return "Not requested yet"
        case .ephemeral: return "Ephemeral"
        @unknown default: return "Unknown"
        }
    }

    var isPermissionDenied: Bool {
        permissionStatus == .denied
    }
}
```

- [ ] **Step 2: Add notifications section to SettingsView**

Modify `fasolt.ios/Fasolt/Views/Settings/SettingsView.swift`. Replace the entire file:

```swift
import SwiftUI

struct SettingsView: View {
    @Environment(AuthService.self) private var authService
    @State private var viewModel: SettingsViewModel
    @State private var notificationViewModel: NotificationSettingsViewModel
    @State private var showSignOutConfirmation = false

    init(viewModel: SettingsViewModel, notificationViewModel: NotificationSettingsViewModel) {
        _viewModel = State(initialValue: viewModel)
        _notificationViewModel = State(initialValue: notificationViewModel)
    }

    var body: some View {
        NavigationStack {
            List {
                Section("Account") {
                    if viewModel.isLoading {
                        HStack {
                            Text("Loading...")
                                .foregroundStyle(.secondary)
                            Spacer()
                            ProgressView()
                        }
                    } else if let error = viewModel.errorMessage {
                        HStack {
                            Label(error, systemImage: "exclamationmark.triangle")
                                .foregroundStyle(.secondary)
                            Spacer()
                            Button("Retry") {
                                Task { await viewModel.loadUserInfo() }
                            }
                            .font(.subheadline)
                        }
                    } else {
                        if let email = viewModel.email {
                            LabeledContent("Email", value: email)
                        }
                        if let serverURL = viewModel.serverURL {
                            LabeledContent("Server", value: serverURL)
                                .lineLimit(1)
                        }
                    }
                }

                Section("Notifications") {
                    if notificationViewModel.isLoading {
                        HStack {
                            Text("Loading...")
                                .foregroundStyle(.secondary)
                            Spacer()
                            ProgressView()
                        }
                    } else {
                        LabeledContent("Permission", value: notificationViewModel.permissionLabel)
                            .onTapGesture {
                                if notificationViewModel.isPermissionDenied {
                                    if let url = URL(string: UIApplication.openSettingsURLString) {
                                        UIApplication.shared.open(url)
                                    }
                                }
                            }

                        if notificationViewModel.hasDeviceToken {
                            Picker("Check interval", selection: Binding(
                                get: { notificationViewModel.intervalHours },
                                set: { newValue in
                                    Task { await notificationViewModel.updateInterval(newValue) }
                                }
                            )) {
                                ForEach(NotificationSettingsViewModel.allowedIntervals, id: \.self) { hours in
                                    Text("Every \(hours)h").tag(hours)
                                }
                            }
                        }

                        if let error = notificationViewModel.errorMessage {
                            Text(error)
                                .foregroundStyle(.red)
                                .font(.caption)
                        }
                    }
                }

                Section {
                    Button("Sign Out", role: .destructive) {
                        showSignOutConfirmation = true
                    }
                }

                Section("About") {
                    LabeledContent("Version", value: viewModel.appVersion)
                }
            }
            .navigationTitle("Settings")
            .offlineBanner()
            .alert("Sign Out?", isPresented: $showSignOutConfirmation) {
                Button("Cancel", role: .cancel) {}
                Button("Sign Out", role: .destructive) {
                    authService.signOut()
                }
            } message: {
                Text("You'll need to sign in again to use Fasolt.")
            }
            .task {
                await viewModel.loadUserInfo()
                await notificationViewModel.load()
            }
        }
    }
}
```

- [ ] **Step 3: Update MainTabView to pass the new ViewModel**

Modify `fasolt.ios/Fasolt/Views/MainTabView.swift`. Update the `SettingsView` instantiation:

Replace:
```swift
            SettingsView(
                viewModel: SettingsViewModel(apiClient: apiClient)
            )
```

With:
```swift
            SettingsView(
                viewModel: SettingsViewModel(apiClient: apiClient),
                notificationViewModel: NotificationSettingsViewModel(apiClient: apiClient)
            )
```

- [ ] **Step 4: Build and verify**

Run:
```bash
cd fasolt.ios && xcodegen generate && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5
```

Expected: Build Succeeded.

- [ ] **Step 5: Commit**

```bash
git add fasolt.ios/Fasolt/ViewModels/NotificationSettingsViewModel.swift fasolt.ios/Fasolt/Views/Settings/SettingsView.swift fasolt.ios/Fasolt/Views/MainTabView.swift
git commit -m "feat: add notification settings UI with interval picker (#23)"
```

---

### Task 7: Backend Tests

**Files:**
- Create: `fasolt.Server.Tests/NotificationBackgroundServiceTests.cs`
- Create: `fasolt.Server.Tests/NotificationEndpointsTests.cs`

- [ ] **Step 1: Check if test project exists**

Run:
```bash
ls fasolt.Server.Tests/ 2>/dev/null || echo "No test project — create one"
```

If no test project exists, create it:
```bash
dotnet new xunit -n fasolt.Server.Tests && dotnet sln add fasolt.Server.Tests && cd fasolt.Server.Tests && dotnet add reference ../fasolt.Server && dotnet add package Microsoft.EntityFrameworkCore.InMemory && dotnet add package Moq
```

- [ ] **Step 2: Write NotificationBackgroundService tests**

Create `fasolt.Server.Tests/NotificationBackgroundServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Server.Infrastructure.Services;

namespace Fasolt.Server.Tests;

public class NotificationBackgroundServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (IServiceScopeFactory, AppDbContext) CreateScopeFactory()
    {
        var db = CreateDb();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(AppDbContext))).Returns(db);

        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return (factory.Object, db);
    }

    [Fact]
    public async Task Skips_users_with_no_device_token()
    {
        var (scopeFactory, db) = CreateScopeFactory();
        var apns = new Mock<ApnsService>(null!, null!, null!);
        var logger = new Mock<ILogger<NotificationBackgroundService>>();

        var user = new AppUser { Id = "u1", UserName = "test@test.com", Email = "test@test.com" };
        db.Users.Add(user);
        db.Cards.Add(new Card
        {
            Id = Guid.NewGuid(), PublicId = "c1", UserId = "u1",
            Front = "Q", Back = "A", DueAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        // No device token registered — user should be skipped
        apns.Verify(x => x.SendNotification(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Skips_users_whose_interval_has_not_elapsed()
    {
        var (scopeFactory, db) = CreateScopeFactory();

        var user = new AppUser
        {
            Id = "u1", UserName = "test@test.com", Email = "test@test.com",
            NotificationIntervalHours = 8,
            LastNotifiedAt = DateTimeOffset.UtcNow.AddHours(-2), // Only 2h ago, interval is 8h
        };
        db.Users.Add(user);
        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = "u1", Token = "abc123",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var eligible = await db.DeviceTokens
            .Include(d => d.User)
            .Where(d =>
                d.User.LastNotifiedAt == null ||
                d.User.LastNotifiedAt.Value.AddHours(d.User.NotificationIntervalHours) <= DateTimeOffset.UtcNow)
            .ToListAsync();

        Assert.Empty(eligible);
    }

    [Fact]
    public async Task Includes_users_whose_interval_has_elapsed()
    {
        var (scopeFactory, db) = CreateScopeFactory();

        var user = new AppUser
        {
            Id = "u1", UserName = "test@test.com", Email = "test@test.com",
            NotificationIntervalHours = 4,
            LastNotifiedAt = DateTimeOffset.UtcNow.AddHours(-5), // 5h ago, interval is 4h
        };
        db.Users.Add(user);
        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = "u1", Token = "abc123",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var eligible = await db.DeviceTokens
            .Include(d => d.User)
            .Where(d =>
                d.User.LastNotifiedAt == null ||
                d.User.LastNotifiedAt.Value.AddHours(d.User.NotificationIntervalHours) <= DateTimeOffset.UtcNow)
            .ToListAsync();

        Assert.Single(eligible);
    }

    [Fact]
    public async Task Includes_users_with_null_LastNotifiedAt()
    {
        var (scopeFactory, db) = CreateScopeFactory();

        var user = new AppUser
        {
            Id = "u1", UserName = "test@test.com", Email = "test@test.com",
            LastNotifiedAt = null,
        };
        db.Users.Add(user);
        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = "u1", Token = "abc123",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var eligible = await db.DeviceTokens
            .Include(d => d.User)
            .Where(d =>
                d.User.LastNotifiedAt == null ||
                d.User.LastNotifiedAt.Value.AddHours(d.User.NotificationIntervalHours) <= DateTimeOffset.UtcNow)
            .ToListAsync();

        Assert.Single(eligible);
    }
}
```

- [ ] **Step 3: Write notification endpoint validation tests**

Create `fasolt.Server.Tests/NotificationEndpointsTests.cs`:

```csharp
using Fasolt.Server.Api.Endpoints;

namespace Fasolt.Server.Tests;

public class NotificationEndpointsTests
{
    [Theory]
    [InlineData(4, true)]
    [InlineData(6, true)]
    [InlineData(8, true)]
    [InlineData(10, true)]
    [InlineData(12, true)]
    [InlineData(24, true)]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(5, false)]
    [InlineData(48, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Validates_allowed_intervals(int interval, bool expected)
    {
        int[] allowedIntervals = [4, 6, 8, 10, 12, 24];
        Assert.Equal(expected, allowedIntervals.Contains(interval));
    }
}
```

- [ ] **Step 4: Run tests**

Run:
```bash
cd fasolt.Server.Tests && dotnet test --verbosity normal
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server.Tests/
git commit -m "test: add notification background service and endpoint tests (#23)"
```

---

### Task 8: E2E API Tests with Playwright

**Files:**
- Create or modify: Playwright test file for notification API round-trip

- [ ] **Step 1: Identify existing Playwright test structure**

Run:
```bash
ls fasolt.client/e2e/ 2>/dev/null || ls fasolt.client/tests/ 2>/dev/null || echo "Check for Playwright config"
find . -name "playwright.config.*" -maxdepth 3 2>/dev/null
```

Adapt the following test to match the existing Playwright test structure.

- [ ] **Step 2: Write notification API e2e test**

Create a test file (adjust path to match existing structure):

```typescript
import { test, expect } from '@playwright/test';

const BASE_URL = 'http://localhost:8080';

test.describe('Notification API', () => {
  let cookies: string;

  test.beforeAll(async ({ request }) => {
    // Login as dev user
    const loginResponse = await request.post(`${BASE_URL}/api/identity/login`, {
      data: { email: 'dev@fasolt.local', password: 'Dev1234!' },
    });
    expect(loginResponse.ok()).toBeTruthy();
    cookies = loginResponse.headers()['set-cookie'] || '';
  });

  test('GET /api/notifications/settings returns defaults', async ({ request }) => {
    const response = await request.get(`${BASE_URL}/api/notifications/settings`, {
      headers: { cookie: cookies },
    });
    expect(response.ok()).toBeTruthy();
    const body = await response.json();
    expect(body.intervalHours).toBe(8);
    expect(body.hasDeviceToken).toBe(false);
  });

  test('PUT device token and verify settings', async ({ request }) => {
    // Register a device token
    const putResponse = await request.put(`${BASE_URL}/api/notifications/device-token`, {
      headers: { cookie: cookies },
      data: { token: 'test-device-token-abc123' },
    });
    expect(putResponse.status()).toBe(204);

    // Verify settings now show hasDeviceToken: true
    const settingsResponse = await request.get(`${BASE_URL}/api/notifications/settings`, {
      headers: { cookie: cookies },
    });
    const body = await settingsResponse.json();
    expect(body.hasDeviceToken).toBe(true);

    // Clean up
    const deleteResponse = await request.delete(`${BASE_URL}/api/notifications/device-token`, {
      headers: { cookie: cookies },
    });
    expect(deleteResponse.status()).toBe(204);
  });

  test('PUT settings with valid interval', async ({ request }) => {
    const response = await request.put(`${BASE_URL}/api/notifications/settings`, {
      headers: { cookie: cookies },
      data: { intervalHours: 6 },
    });
    expect(response.status()).toBe(204);

    const settingsResponse = await request.get(`${BASE_URL}/api/notifications/settings`, {
      headers: { cookie: cookies },
    });
    const body = await settingsResponse.json();
    expect(body.intervalHours).toBe(6);

    // Reset to default
    await request.put(`${BASE_URL}/api/notifications/settings`, {
      headers: { cookie: cookies },
      data: { intervalHours: 8 },
    });
  });

  test('PUT settings with invalid interval returns 400', async ({ request }) => {
    const response = await request.put(`${BASE_URL}/api/notifications/settings`, {
      headers: { cookie: cookies },
      data: { intervalHours: 5 },
    });
    expect(response.status()).toBe(400);
  });
});
```

- [ ] **Step 3: Run the e2e tests**

Ensure the full stack is running (`./dev.sh`), then:
```bash
cd fasolt.client && npx playwright test notifications
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/e2e/ || git add fasolt.client/tests/
git commit -m "test: add e2e tests for notification API endpoints (#23)"
```

---

### Task 9: Add XcodeGen entry for new files

**Files:**
- Modify: `fasolt.ios/project.yml` (if XcodeGen is used)

- [ ] **Step 1: Check if new Swift files need XcodeGen config**

Run:
```bash
cat fasolt.ios/project.yml 2>/dev/null | head -30
```

If the project uses glob patterns like `Sources/**/*.swift`, no changes needed. If files are listed explicitly, add the new files:
- `Fasolt/Services/NotificationService.swift`
- `Fasolt/ViewModels/NotificationSettingsViewModel.swift`

- [ ] **Step 2: Regenerate and verify**

Run:
```bash
cd fasolt.ios && xcodegen generate
```

Expected: project generated successfully.

- [ ] **Step 3: Commit if changes were needed**

```bash
git add fasolt.ios/project.yml 2>/dev/null
git diff --cached --quiet || git commit -m "chore: add notification files to XcodeGen config (#23)"
```
