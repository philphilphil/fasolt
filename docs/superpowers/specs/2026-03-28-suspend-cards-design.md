# Suspend Cards — Design Spec

**Issue:** #45 — Add ability to pause cards
**Date:** 2026-03-28

## Overview

Users need the ability to suspend individual cards from study. Suspended cards are excluded from review sessions but remain visible (dimmed, sorted to end) in all list views. This uses the standard SRS term "suspend" (as in Anki) for clarity across the app and MCP tools.

## Data Model

### Card Entity

Add `bool IsSuspended { get; set; } = false` to the Card entity. EF migration adds the column with default `false`.

### Deck Entity — Rename

Rename `Deck.IsActive` to `Deck.IsSuspended` (default `false`), inverting the logic everywhere. A deck that was `IsActive = true` becomes `IsSuspended = false`. This gives consistent naming across cards and decks.

## API

### New Endpoint

`PUT /api/cards/{id}/suspended` — body: `{ "isSuspended": bool }`

### Renamed Endpoint

`PUT /api/decks/{id}/active` renamed to `PUT /api/decks/{id}/suspended` — body: `{ "isSuspended": bool }`

### Card DTOs

All card DTOs include `isSuspended` field.

### Deck DTOs

All deck DTOs replace `isActive` with `isSuspended` (inverted).

## Review Flow

### Due Card Query

The `GetDueCards` query adds `&& !c.IsSuspended` to the existing filter. Full filter becomes:

```
card is not suspended
AND (card has no decks OR card is in at least one non-suspended deck)
```

### Suspend During Review

1. Card is marked `IsSuspended = true` via API call
2. Current card is skipped (next due card shown, same as existing skip)
3. FSRS state is not modified — no review recorded
4. On unsuspend, card resumes with original due date and FSRS state

## MCP Tools

### All Tools — `isSuspended` Field

- `ListCards`, `SearchCards` — return `isSuspended` in response. Add optional `isSuspended` filter parameter (default: return all).
- `UpdateCards` — allow setting `isSuspended` on cards.
- `CreateCards` — accept optional `isSuspended` (default `false`).

### Fix: Deck Suspension Filtering

Existing MCP tools (`ListCards`, `SearchCards`) should respect deck `IsSuspended` where they currently don't. Add optional filter support.

## Web UI

### Card Table (CardsView, SourceView)

- Add "Suspend" action to card row actions menu
- Suspended cards: `opacity-50`, sorted to end of list
- Action changes to "Unsuspend" for suspended cards
- Visual indicator (badge) on suspended cards

### Review Session

- Add "Suspend" button alongside existing Skip button
- On click: suspend card via API, then skip to next card
- If all remaining cards become suspended, session ends normally

## iOS

### Review Session Only

- Add "Suspend" button alongside Skip in the toolbar
- On click: suspend via API, then skip to next card

### No Other iOS Changes

Card detail and card list changes deferred to a separate issue.

## Rename Scope: `IsActive` to `IsSuspended`

The deck rename touches ~19 backend files and ~10 frontend files. Most are mechanical: flip the boolean, rename the field. Migration designer files are auto-generated and will be regenerated. No iOS files reference `isActive` currently.

## Non-Goals

- No FSRS state modification on suspend/unsuspend
- No bulk suspend UI (MCP `UpdateCards` supports bulk)
- No iOS card detail or list changes (separate issue)
