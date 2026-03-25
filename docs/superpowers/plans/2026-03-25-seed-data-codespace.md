# Seed Data & Codespace Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Populate the dev database with rich seed data (decks, cards with SVG/SRS states/source metadata, two users) and add Codespace configuration.

**Architecture:** Extend `DevSeedData.SeedAsync()` to create entities via `AppDbContext` after user creation. Add `.devcontainer/devcontainer.json` for GitHub Codespaces with .NET 10, Node, and Docker-in-Docker.

**Tech Stack:** .NET 10, EF Core, GitHub Codespaces devcontainer

**Spec:** `docs/superpowers/specs/2026-03-25-seed-data-codespace-design.md`

---

### File Map

- Modify: `fasolt.Server/Infrastructure/Data/DevSeedData.cs` — expand seed data with decks, cards, second user
- Create: `.devcontainer/devcontainer.json` — Codespace configuration

---

### Task 1: Devcontainer Configuration

**Files:**
- Create: `.devcontainer/devcontainer.json`

- [ ] **Step 1: Create the devcontainer.json**

```json
{
  "name": "fasolt",
  "image": "mcr.microsoft.com/devcontainers/dotnet:10.0",
  "features": {
    "ghcr.io/devcontainers/features/node:1": {},
    "ghcr.io/devcontainers/features/docker-in-docker:2": {}
  },
  "postCreateCommand": "cd fasolt.client && npm install",
  "customizations": {
    "vscode": {
      "extensions": [
        "ms-dotnettools.csdevkit",
        "Vue.volar",
        "bradlc.vscode-tailwindcss"
      ]
    }
  },
  "forwardPorts": [5173, 8080, 5432]
}
```

- [ ] **Step 2: Verify valid JSON**

Run: `python3 -c "import json; json.load(open('.devcontainer/devcontainer.json')); print('Valid JSON')"`
Expected: `Valid JSON`

- [ ] **Step 3: Commit**

```bash
git add .devcontainer/devcontainer.json
git commit -m "feat: add devcontainer configuration for GitHub Codespaces (#25)"
```

---

### Task 2: Expand Seed Data

**Files:**
- Modify: `fasolt.Server/Infrastructure/Data/DevSeedData.cs`

- [ ] **Step 1: Add using statements and rewrite DevSeedData**

Replace the entire contents of `DevSeedData.cs` with the following. This keeps the existing admin user creation logic and adds rich seed data after it:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;

namespace Fasolt.Server.Infrastructure.Data;

public static class DevSeedData
{
    public const string DevEmail = "dev@fasolt.local";
    public const string DevPassword = "Dev1234!";
    public const string RegularEmail = "user@fasolt.local";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await userManager.FindByEmailAsync(DevEmail);
        if (existing is not null)
        {
            // Ensure dev user has Admin role even if already created
            if (!await userManager.IsInRoleAsync(existing, "Admin"))
                await userManager.AddToRoleAsync(existing, "Admin");
            return;
        }

        // === Admin User ===
        var adminUser = new AppUser
        {
            UserName = DevEmail,
            Email = DevEmail,
            DisplayName = "Dev User",
            EmailConfirmed = true,
        };
        await userManager.CreateAsync(adminUser, DevPassword);
        await userManager.AddToRoleAsync(adminUser, "Admin");

        // === Regular User ===
        var regularUser = new AppUser
        {
            UserName = RegularEmail,
            Email = RegularEmail,
            DisplayName = "Regular User",
            EmailConfirmed = true,
        };
        await userManager.CreateAsync(regularUser, DevPassword);

        // === Admin Decks ===
        var now = DateTimeOffset.UtcNow;

        var capitalsDeck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Name = "European Capitals",
            Description = "Capitals of European countries",
            CreatedAt = now,
            IsActive = true,
        };

        var programmingDeck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Name = "Programming Concepts",
            Description = "Core computer science concepts",
            CreatedAt = now,
            IsActive = true,
        };

        var archivedDeck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Name = "Archived Deck",
            Description = "An inactive deck for testing",
            CreatedAt = now,
            IsActive = false,
        };

        db.Decks.AddRange(capitalsDeck, programmingDeck, archivedDeck);

        // === Admin Cards — European Capitals ===
        var france = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is the capital of France?",
            Back = "Paris",
            State = "review",
            Stability = 15.5,
            Difficulty = 4.2,
            Step = null,
            DueAt = now.AddDays(5),
            LastReviewedAt = now.AddDays(-2),
            CreatedAt = now.AddDays(-10),
        };

        var germany = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is the capital of Germany?",
            Back = "Berlin",
            State = "learning",
            Stability = 2.1,
            Difficulty = 5.0,
            Step = 1,
            DueAt = now.AddMinutes(10),
            LastReviewedAt = now,
            CreatedAt = now.AddDays(-3),
        };

        var spain = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is the capital of Spain?",
            Back = "Madrid",
            State = "new",
            CreatedAt = now.AddDays(-1),
        };

        var italy = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is the capital of Italy?",
            Back = "Rome",
            State = "new",
            CreatedAt = now,
        };

        db.Cards.AddRange(france, germany, spain, italy);
        db.DeckCards.AddRange(
            new DeckCard { DeckId = capitalsDeck.Id, CardId = france.Id },
            new DeckCard { DeckId = capitalsDeck.Id, CardId = germany.Id },
            new DeckCard { DeckId = capitalsDeck.Id, CardId = spain.Id },
            new DeckCard { DeckId = capitalsDeck.Id, CardId = italy.Id }
        );

        // === Admin Cards — Programming Concepts ===
        var linkedList = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is a linked list?",
            Back = "A linear data structure where each element (node) contains a value and a pointer to the next node. Unlike arrays, elements are not stored contiguously in memory.",
            State = "new",
            CreatedAt = now.AddDays(-2),
        };

        var bigO = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is Big O notation?",
            Back = "A mathematical notation that describes the upper bound of an algorithm's time or space complexity as the input size grows. Common complexities: O(1), O(log n), O(n), O(n log n), O(n²).",
            State = "review",
            Stability = 22.0,
            Difficulty = 5.5,
            Step = null,
            DueAt = now.AddDays(7),
            LastReviewedAt = now.AddDays(-1),
            CreatedAt = now.AddDays(-14),
        };

        const string treeFrontSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 150" width="200" height="150">
              <line x1="100" y1="30" x2="50" y2="90" stroke="currentColor" stroke-width="2"/>
              <line x1="100" y1="30" x2="150" y2="90" stroke="currentColor" stroke-width="2"/>
              <circle cx="100" cy="30" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="100" y="35" text-anchor="middle" font-size="14" fill="currentColor">8</text>
              <circle cx="50" cy="90" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="50" y="95" text-anchor="middle" font-size="14" fill="currentColor">3</text>
              <circle cx="150" cy="90" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="150" y="95" text-anchor="middle" font-size="14" fill="currentColor">10</text>
            </svg>
            """;

        const string treeBackSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 150" width="200" height="150">
              <line x1="100" y1="30" x2="50" y2="90" stroke="currentColor" stroke-width="2"/>
              <line x1="100" y1="30" x2="150" y2="90" stroke="currentColor" stroke-width="2"/>
              <circle cx="100" cy="30" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="100" y="35" text-anchor="middle" font-size="14" fill="currentColor">root</text>
              <circle cx="50" cy="90" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="50" y="95" text-anchor="middle" font-size="14" fill="currentColor">L</text>
              <circle cx="150" cy="90" r="18" fill="none" stroke="currentColor" stroke-width="2"/>
              <text x="150" y="95" text-anchor="middle" font-size="14" fill="currentColor">R</text>
              <text x="100" y="140" text-anchor="middle" font-size="11" fill="currentColor">Left &lt; Root &lt; Right</text>
            </svg>
            """;

        var binaryTree = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "Explain a binary tree",
            Back = "A tree data structure where each node has at most two children, referred to as left and right. Binary search trees maintain the property: left < parent < right.",
            FrontSvg = treeFrontSvg,
            BackSvg = treeBackSvg,
            State = "new",
            CreatedAt = now.AddDays(-2),
        };

        db.Cards.AddRange(linkedList, bigO, binaryTree);
        db.DeckCards.AddRange(
            new DeckCard { DeckId = programmingDeck.Id, CardId = linkedList.Id },
            new DeckCard { DeckId = programmingDeck.Id, CardId = bigO.Id },
            new DeckCard { DeckId = programmingDeck.Id, CardId = binaryTree.Id }
        );

        // === Admin Cards — Archived Deck ===
        var recursion = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is recursion?",
            Back = "A technique where a function calls itself to solve a problem by breaking it into smaller subproblems. Requires a base case to terminate.",
            State = "new",
            CreatedAt = now.AddDays(-5),
        };

        db.Cards.Add(recursion);
        db.DeckCards.Add(new DeckCard { DeckId = archivedDeck.Id, CardId = recursion.Id });

        // === Admin Cards — Orphaned (no deck) ===
        var moonLanding = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What year was the moon landing?",
            Back = "1969 — Apollo 11, with astronauts Neil Armstrong and Buzz Aldrin.",
            State = "new",
            CreatedAt = now.AddDays(-7),
        };

        var speedOfLight = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is the speed of light?",
            Back = "Approximately 300,000 km/s (299,792,458 m/s) in a vacuum.",
            SourceFile = "physics-notes.md",
            SourceHeading = "Constants",
            State = "new",
            CreatedAt = now.AddDays(-4),
        };

        // === Admin Cards — Source metadata (no deck) ===
        var photosynthesis = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = adminUser.Id,
            Front = "What is photosynthesis?",
            Back = "The process by which plants convert sunlight, water, and carbon dioxide into glucose and oxygen. Occurs primarily in chloroplasts.",
            SourceFile = "biology-notes.md",
            SourceHeading = "Plant Processes",
            State = "new",
            CreatedAt = now.AddDays(-6),
        };

        db.Cards.AddRange(moonLanding, speedOfLight, photosynthesis);

        // === Regular User Data ===
        var mathDeck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = regularUser.Id,
            Name = "Math Basics",
            Description = "Fundamental math concepts",
            CreatedAt = now,
            IsActive = true,
        };

        db.Decks.Add(mathDeck);

        var addition = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = regularUser.Id,
            Front = "What is 2+2?",
            Back = "4",
            State = "new",
            CreatedAt = now,
        };

        var squareRoot = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = regularUser.Id,
            Front = "What is the square root of 144?",
            Back = "12",
            State = "new",
            CreatedAt = now,
        };

        var pi = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = regularUser.Id,
            Front = "What is pi to 2 decimal places?",
            Back = "3.14",
            State = "review",
            Stability = 18.0,
            Difficulty = 4.5,
            Step = null,
            DueAt = now.AddDays(4),
            LastReviewedAt = now.AddDays(-3),
            CreatedAt = now.AddDays(-8),
        };

        db.Cards.AddRange(addition, squareRoot, pi);
        db.DeckCards.AddRange(
            new DeckCard { DeckId = mathDeck.Id, CardId = addition.Id },
            new DeckCard { DeckId = mathDeck.Id, CardId = squareRoot.Id },
            new DeckCard { DeckId = mathDeck.Id, CardId = pi.Id }
        );

        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 2: Verify backend builds**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Infrastructure/Data/DevSeedData.cs
git commit -m "feat: expand dev seed data with decks, cards, SVG, and second user (#25)"
```

---

### Task 3: Test Seed Data

- [ ] **Step 1: Drop the database and restart**

```bash
docker compose down -v && docker compose up -d
```

Wait for Postgres to be ready, then start the backend:

```bash
dotnet run --project fasolt.Server
```

Expected: Backend starts, seed data runs without errors in the console.

- [ ] **Step 2: Verify via API**

Log in and check data:

```bash
# Login
curl -s -c /tmp/cookies.txt -X POST 'http://localhost:8080/api/identity/login?useCookies=true' \
  -H 'Content-Type: application/json' \
  -d '{"email":"dev@fasolt.local","password":"Dev1234!"}'

# Check decks (expect 3: European Capitals, Programming Concepts, Archived Deck)
curl -s -b /tmp/cookies.txt http://localhost:8080/api/decks | python3 -m json.tool

# Check cards (expect 11 admin cards)
curl -s -b /tmp/cookies.txt 'http://localhost:8080/api/cards?limit=20' | python3 -m json.tool

# Check sources (expect physics-notes.md and biology-notes.md)
curl -s -b /tmp/cookies.txt http://localhost:8080/api/sources | python3 -m json.tool

# Check overview
curl -s -b /tmp/cookies.txt http://localhost:8080/api/review/overview | python3 -m json.tool
```

- [ ] **Step 3: Verify regular user isolation**

```bash
# Login as regular user
curl -s -c /tmp/cookies2.txt -X POST 'http://localhost:8080/api/identity/login?useCookies=true' \
  -H 'Content-Type: application/json' \
  -d '{"email":"user@fasolt.local","password":"Dev1234!"}'

# Check decks (expect 1: Math Basics)
curl -s -b /tmp/cookies2.txt http://localhost:8080/api/decks | python3 -m json.tool

# Check cards (expect 3)
curl -s -b /tmp/cookies2.txt 'http://localhost:8080/api/cards?limit=20' | python3 -m json.tool
```

- [ ] **Step 4: Verify in browser with Playwright**

Using Playwright MCP:
1. Navigate to `http://localhost:5173/login`, log in as `dev@fasolt.local`
2. Go to `/decks` — verify 3 decks visible (one should show as inactive)
3. Go to `/cards` — verify multiple cards with different states
4. Click on the "Explain a binary tree" card — verify SVG renders on both front and back
5. Go to `/sources` — verify "physics-notes.md" and "biology-notes.md" appear
6. Log out, log in as `user@fasolt.local`, go to `/decks` — verify only "Math Basics" is visible

- [ ] **Step 5: Close issue**

```bash
gh issue close 25
```
