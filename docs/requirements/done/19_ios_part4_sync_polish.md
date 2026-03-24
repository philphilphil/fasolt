# iOS App Part 4: Sync + Polish

## Overview

Automatic sync of offline review queue, connectivity-aware behavior, and UX polish (haptics, animations, error states).

## IOS-4.1 — SyncService (P0)

As a user, I want my offline ratings to be synced to the server automatically when I come back online.

**Acceptance criteria:**

- SyncService monitors `NetworkMonitor.isConnected`
- When connectivity restored, flush `PendingReview` items to `POST /api/review/rate`
- Mark reviews as `synced = true` after successful submission
- Delete synced reviews from SwiftData
- Handle conflicts gracefully (card deleted on server, etc.)
- Retry failed items on next connectivity change

## IOS-4.2 — Connectivity Indicator (P1)

As a user, I want to know when I'm offline so I understand why some features may not work.

**Acceptance criteria:**

- Show subtle "Offline" banner/indicator when `isConnected == false`
- Dismiss automatically when back online
- Study session: show indicator but allow rating (queued)
- Dashboard: show cached stats with "offline" note

## IOS-4.3 — Error States (P1)

As a user, I want clear feedback when something goes wrong.

**Acceptance criteria:**

- Network errors: show retry option, not a crash
- Auth expired and refresh failed: redirect to onboarding with message
- Empty states: "No cards yet" on dashboard, "No decks" on deck list
- Loading states: skeleton or spinner on all data-loading views

## IOS-4.4 — Haptics + Animation Polish (P2)

As a user, I want the study experience to feel responsive and satisfying.

**Acceptance criteria:**

- Card flip: 3D rotation animation (already implemented in Part 2)
- Rating button press: light haptic feedback
- Session complete: success haptic
- Dashboard stats: animate number changes on refresh
- Tab bar: smooth transitions

## IOS-4.5 — App Icon + Launch Screen (P2)

As a user, I want the app to look polished with a proper icon and launch screen.

**Acceptance criteria:**

- App icon (Fasolt branding)
- Launch screen matching the onboarding color scheme
- Light/dark mode support throughout
