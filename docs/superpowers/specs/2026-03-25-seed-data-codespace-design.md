# Seed Data & Codespace Support

**Date:** 2026-03-25
**Issue:** #25

## Summary

Expand dev seed data to populate decks, cards (including SVG, varied SRS states, source metadata), and a second regular user. Add `.devcontainer` configuration for GitHub Codespaces.

## Seed Data

### Approach

Extend `DevSeedData.SeedAsync()` in `fasolt.Server/Infrastructure/Data/DevSeedData.cs`. Keep existing admin user creation code as-is. After user creation, seed data for both users. Skip entirely if admin user already exists (current behavior preserved).

### Implementation Notes

- Resolve `AppDbContext` from DI (`scope.ServiceProvider.GetRequiredService<AppDbContext>()`) to persist Cards, Decks, and DeckCards
- Every Card and Deck must set `Id = Guid.NewGuid()`, `PublicId = NanoIdGenerator.New()`, and `CreatedAt = DateTimeOffset.UtcNow`
- Follow existing entity creation patterns from `CardService.cs` and `DeckService.cs`

### Admin User (`dev@fasolt.local`)

**Decks:**

| Deck | IsActive | Purpose |
|------|----------|---------|
| European Capitals | true | Plain text cards, multiple SRS states |
| Programming Concepts | true | Includes SVG card |
| Archived Deck | false | Tests inactive deck feature |

**Cards (10-12 total):**

European Capitals deck (4 cards):
- "What is the capital of France?" / "Paris" — state: review, with stability/difficulty/dueAt set
- "What is the capital of Germany?" / "Berlin" — state: learning, with step=1
- "What is the capital of Spain?" / "Madrid" — state: new
- "What is the capital of Italy?" / "Rome" — state: new

Programming Concepts deck (3 cards):
- "What is a linked list?" / "A linear data structure..." — state: new
- "What is Big O notation?" / "A mathematical notation..." — state: review, with SRS data
- "Explain a binary tree" / "A tree data structure..." — state: new, with front SVG (simple tree diagram) and back SVG (annotated tree)

Archived Deck (1 card):
- "What is recursion?" / "A function that calls itself..." — state: new

Orphaned cards (no deck, 2 cards):
- "What year was the moon landing?" / "1969" — state: new
- "What is the speed of light?" / "~300,000 km/s" — state: new, with `SourceFile: "physics-notes.md"`, `SourceHeading: "Constants"`

Cards with source metadata (1 additional):
- "What is photosynthesis?" / "The process by which plants convert sunlight..." — state: new, `SourceFile: "biology-notes.md"`, `SourceHeading: "Plant Processes"`, in no deck

### Regular User (`user@fasolt.local` / `Dev1234!`)

- DisplayName: "Regular User"
- No admin role
- 1 deck: "Math Basics" (active)
- 3 cards in deck:
  - "What is 2+2?" / "4" — state: new
  - "What is the square root of 144?" / "12" — state: new
  - "What is pi to 2 decimal places?" / "3.14" — state: review, with SRS data

### SRS Field Values for Non-New Cards

Cards in "review" state get realistic FSRS values:
- `Stability`: 10.0-25.0
- `Difficulty`: 4.0-6.0
- `Step`: null (review cards have no step)
- `DueAt`: DateTimeOffset.UtcNow.AddDays(3-7)
- `LastReviewedAt`: DateTimeOffset.UtcNow.AddDays(-1 to -3)
- `State`: "review"

Cards in "learning" state:
- `Stability`: 1.0-3.0
- `Difficulty`: 5.0
- `Step`: 1
- `DueAt`: DateTimeOffset.UtcNow.AddMinutes(10)
- `LastReviewedAt`: DateTimeOffset.UtcNow
- `State`: "learning"

### SVG Content

The binary tree SVG should be a simple, valid SVG (~10-20 lines) showing a tree with a root node and two children. Keep it minimal — just enough to verify SVG rendering works.

## Codespace Configuration

### `.devcontainer/devcontainer.json`

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

### Docker Compose Integration

The existing `docker-compose.yml` handles Postgres. The `./dev.sh` script starts everything. No separate devcontainer compose file needed — `postCreateCommand` or `postStartCommand` can run `docker compose up -d` if needed, but since `./dev.sh` already handles this, we keep it simple.

## Files Changed

- Modify: `fasolt.Server/Infrastructure/Data/DevSeedData.cs` — expand seed data
- Create: `.devcontainer/devcontainer.json` — Codespace configuration

## Testing

- Drop database and restart backend to verify seed data creates correctly
- Verify in UI: login as admin, check decks (3 including inactive), cards (various states, SVG card), sources view
- Login as regular user, verify isolated data
- Test Codespace by checking that `devcontainer.json` is valid JSON and references valid image/features
