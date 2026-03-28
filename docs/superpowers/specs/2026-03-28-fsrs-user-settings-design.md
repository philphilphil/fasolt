# User-Configurable FSRS Scheduling Settings

**Issue:** #38
**Date:** 2026-03-28

## Overview

Let users configure FSRS scheduling parameters (desired retention, maximum interval) from web and iOS settings. Currently hardcoded globally in `Program.cs`.

## Decisions

- **Future-only** — changing settings affects future reviews only; already-scheduled `DueAt` values are not recalculated. The info box on each setting explains this to the user.
- **Per-request scheduler** — construct a new `Scheduler` instance per review request using the user's settings instead of the injected global singleton. No caching.
- **Columns on AppUser** — store settings directly on the user entity, matching the existing `NotificationIntervalHours` pattern.
- **EnableFuzzing stays hardcoded** — always `true`, not exposed to users.
- **Dedicated endpoint** — `GET/PUT /api/settings/scheduling`, parallel to `/api/notifications/settings`.

## Data Model

Add two nullable columns to `AppUser`:

| Column | Type | Range | Default (when null) |
|--------|------|-------|---------------------|
| `DesiredRetention` | `double?` | 0.70–0.97 | 0.9 |
| `MaximumInterval` | `int?` | 1–36500 | 36500 |

`null` means "use system default". No backfill needed for existing users.

EF Core migration adds the columns with `HasDefaultValue(null)`.

## API

### `GET /api/settings/scheduling`

Returns the user's current scheduling settings, resolving defaults for null values.

**Response:**
```json
{
  "desiredRetention": 0.9,
  "maximumInterval": 36500
}
```

### `PUT /api/settings/scheduling`

Updates one or both values. Server validates:
- `desiredRetention`: 0.70–0.97
- `maximumInterval`: 1–36500

Returns 400 with validation errors if out of range.

**Request:**
```json
{
  "desiredRetention": 0.85,
  "maximumInterval": 365
}
```

**Response:** same shape as GET, echoing the saved values.

## Backend Scheduling Change

Currently in `ReviewService.RateCard()`:
- The global `IScheduler` (registered via `AddFSRS()` in `Program.cs`) is injected and used for all users.

New flow:
1. Load the user's `DesiredRetention` and `MaximumInterval` from `AppUser` (already available via the card's `UserId`).
2. Construct a new `Scheduler` with those values (falling back to defaults for nulls) and `EnableFuzzing = true`.
3. Use that scheduler for `ReviewCard()`.

The global `AddFSRS()` registration in `Program.cs` remains for any non-review uses but is no longer used in the review path.

## Web UI

Add a "Scheduling" section to the existing `/settings` page (`SettingsView.vue`).

### Desired Retention
- Number input or slider, range 0.70–0.97, step 0.01
- Info box: *"How likely you want to remember a card when it comes up for review. Higher values (e.g. 0.95) mean more frequent reviews but stronger recall. Lower values (e.g. 0.85) mean fewer reviews but more forgetting. Changes apply to future reviews only — cards already scheduled keep their current due dates."*

### Maximum Interval
- Number input, range 1–36500 days
- Info box: *"The longest gap allowed between reviews, in days. For example, 365 means you'll see every card at least once a year. The default (36500 days = ~100 years) means there's effectively no cap."*

### Interactions
- Save button for the scheduling section
- Client-side validation matching server ranges
- Success/error toast on save

## iOS UI

Add a "Scheduling" section to `SettingsView.swift`, following the notification settings pattern.

### Implementation
- New `SchedulingSettingsViewModel` that calls `GET/PUT /api/settings/scheduling`
- Desired retention: slider or stepper (0.70–0.97, step 0.01)
- Maximum interval: text field with numeric keyboard (1–36500)
- Info text below each input matching the web copy
- Save triggers PUT, shows success/error feedback

## Validation

Server-side validation (single source of truth):
- `DesiredRetention`: must be between 0.70 and 0.97 inclusive
- `MaximumInterval`: must be between 1 and 36500 inclusive

Client-side validation mirrors these ranges for immediate feedback but the server is authoritative.

## Testing

- Unit test: `ReviewService` uses user's custom settings when constructing scheduler
- Unit test: `ReviewService` falls back to defaults when user settings are null
- Unit test: validation rejects out-of-range values
- Playwright: navigate to /settings, change scheduling values, save, verify persistence
- iOS: manual test of the scheduling settings section
