# Reset Card Study Progress

**Date:** 2026-03-25
**Issue:** #21

## Summary

Add a button on the card detail page to reset a single card's SRS study progress, returning it to "new" state while preserving card content.

## Backend

### New Endpoint

`POST /api/cards/{id}/reset`

- Auth: requires authorization (same as other card endpoints)
- Rate limiting: uses existing `api` rate limit group
- Looks up card by `publicId` + `userId`
- Resets SRS fields:
  - `Stability` → `null`
  - `Difficulty` → `null`
  - `Step` → `null`
  - `DueAt` → `null`
  - `State` → `"new"`
  - `LastReviewedAt` → `null`
- Returns: updated `CardDto` (200 OK), or 404 if not found

### Endpoint Handler

Follows the established pattern: inject `ClaimsPrincipal`, `UserManager<AppUser>`, and `CardService`. Guard with `Results.Unauthorized()` when user is null before delegating to `CardService` (same as `GetById`, `Update`, `Delete`).

### CardService Method

```csharp
public async Task<CardDto?> ResetProgress(string userId, string publicId)
```

Must `.Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)` before calling `ToDto()` — same as `GetCard` and `UpdateCard` — so the returned DTO includes deck info.

No new DTOs required — returns existing `CardDto`.

### Endpoint Registration

Add to `CardEndpoints.MapCardEndpoints()`:

```csharp
group.MapPost("/{id}/reset", ResetProgress);
```

## Frontend

### Card Detail Page (CardDetailView.vue)

- Add "Reset Progress" button in the header button group, next to Edit and Delete
- Hidden during edit mode (`v-if="!editing"`), same as the Edit button
- Styled as `variant="outline"` with destructive text color (matches Delete button pattern)
- Confirmation dialog before executing:
  - Title: "Reset study progress"
  - Description: "This will clear all SRS data (stability, difficulty, scheduling) and return the card to 'new' state."
  - Buttons: Cancel / Reset
- On confirm: calls API, updates local card ref with response
- Shows success feedback ("Progress reset") and error feedback on failure

### Cards Store (cards.ts)

New method:

```typescript
async resetProgress(id: string): Promise<Card> {
  const res = await fetch(`/api/cards/${id}/reset`, { method: 'POST' })
  // handle response, return updated card
}
```

### No New Files

No new components, routes, types, or composables needed.

## Testing

- Playwright test: navigate to a card that has been studied (non-"new" state), click Reset Progress, confirm dialog, verify SRS stats panel shows reset values (state = "new", dashes for other fields)
