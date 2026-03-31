# GDPR Account Deletion & Data Export

**Issue:** #73
**Date:** 2026-03-31

## Overview

Add two endpoints to fulfill GDPR Article 17 (right to be forgotten) and Article 20 (data portability). Users can export all their data as a structured JSON file and permanently delete their account. UI added to the Settings page.

## API Endpoints

### `POST /api/account/export`

Returns a JSON file download of all user data.

- Auth: `[EmailVerified]`
- Response: `200 OK`, `Content-Type: application/json`, `Content-Disposition: attachment; filename="fasolt-export-{date}.json"`
- Synchronous — queries all user-owned entities, serializes, streams response

### `DELETE /api/account`

Permanently deletes the account and all associated data.

- Auth: `[EmailVerified]`
- Request body (local accounts): `{ "password": "..." }`
- Request body (GitHub accounts): `{ "confirmEmail": "user@example.com" }`
- Response: `200 OK`, clears auth cookie
- On failure (wrong password/email mismatch): `400 Bad Request`

## Export JSON Structure

Organized by domain concept. All fields included for completeness. Internal DB GUIDs omitted — publicIds are the external identifiers.

```json
{
  "exportedAt": "2026-03-31T12:00:00Z",
  "account": {
    "email": "user@example.com",
    "emailConfirmed": true,
    "externalProvider": null,
    "desiredRetention": 0.9,
    "maximumInterval": 36500,
    "notificationIntervalHours": 8
  },
  "decks": [
    {
      "name": "Biology",
      "description": "...",
      "isSuspended": false,
      "createdAt": "...",
      "cards": ["<publicId>", "<publicId>"]
    }
  ],
  "cards": [
    {
      "publicId": "abc123",
      "front": "What is mitosis?",
      "back": "Cell division...",
      "frontSvg": null,
      "backSvg": null,
      "sourceFile": "biology/cells.md",
      "sourceHeading": "Cell Division",
      "state": "Review",
      "stability": 12.5,
      "difficulty": 5.2,
      "step": 0,
      "dueAt": "...",
      "lastReviewedAt": "...",
      "isSuspended": false,
      "createdAt": "..."
    }
  ],
  "sources": ["biology/cells.md", "history/ww2.md"],
  "snapshots": [
    {
      "deckName": "Biology",
      "version": 3,
      "cardCount": 42,
      "data": { "..." : "..." },
      "createdAt": "..."
    }
  ],
  "consentGrants": [
    { "clientId": "...", "grantedAt": "..." }
  ],
  "deviceToken": {
    "token": "...",
    "createdAt": "...",
    "updatedAt": "..."
  }
}
```

**Key decisions:**
- Decks reference cards by publicId — avoids duplicating card data
- Cards are a flat list — a card can be in multiple decks or none, so nesting would be lossy
- Sources as a convenience array — derived from cards, useful for quick scanning
- Scheduling fields (stability, difficulty, step, state) included for potential SRS history migration

## Deletion Logic

**Validation:**
- Local accounts: verify password via `UserManager.CheckPasswordAsync`
- GitHub accounts: verify submitted email matches user's email

**Deletion order:**
1. Delete OpenIddict tokens where `Subject == userId`
2. Delete OpenIddict authorizations where `Subject == userId`
3. Delete the `AppUser` — EF Core cascade handles: cards, decks, deck-cards, snapshots, consent grants, device tokens
4. Sign the user out (clear auth cookie)

No soft-delete. GDPR right to be forgotten means data is permanently removed.

## Frontend UI

New "Your Data" card section at the bottom of the Settings page:

- **Export Data** button — triggers `POST /api/account/export`, browser downloads the JSON file
- **Delete Account** button (destructive red styling) — opens a confirmation dialog

**Deletion confirmation dialog:**
- Local accounts: password input + "This action is permanent and cannot be undone" warning
- GitHub accounts: "Type your email to confirm" input + same warning
- Confirm button (destructive) + Cancel button
- On success: redirect to landing page

Uses existing shadcn-vue `Dialog`, `Button`, `Input` components.

## Architecture

Single `AccountDataService` in `Application/Services/` handles both export and deletion. Endpoints added to existing `AccountEndpoints.cs`. Follows the existing minimal API extension method pattern.

## Entities in scope

| Entity | Export | Cascade Delete |
|--------|--------|----------------|
| AppUser (+ Identity tables) | Yes (profile fields) | Yes (Identity deletion) |
| Card | Yes | Yes (cascade) |
| Deck | Yes | Yes (cascade) |
| DeckCard | Yes (as deck→card refs) | Yes (cascade) |
| DeckSnapshot | Yes | Yes (cascade) |
| ConsentGrant | Yes | Yes (cascade) |
| DeviceToken | Yes | Yes (cascade) |
| OpenIddict Authorizations | No (infrastructure) | Yes (manual delete by Subject) |
| OpenIddict Tokens | No (infrastructure) | Yes (manual delete by Subject) |
