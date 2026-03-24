# iOS App Part 3: Deck Browser + Settings

## Overview

Browse decks and their cards, with offline caching. Settings screen with server URL, logout, and about info.

## IOS-3.1 — Deck List (P0)

As a user, I want to see all my decks with card counts and due counts so I can choose what to study.

**Acceptance criteria:**

- List all decks from `GET /api/decks`
- Show deck name, card count, due count
- Pull to refresh
- Tap deck to see its cards (IOS-3.2)
- Cache deck list in SwiftData for offline browsing

## IOS-3.2 — Deck Detail (P0)

As a user, I want to see all cards in a deck so I can browse what I've created.

**Acceptance criteria:**

- Show cards in selected deck from `GET /api/decks/{id}`
- Show card front, back, source file, state
- Tap a card to see full details (front/back)
- "Study This Deck" button starts a study session filtered to this deck

## IOS-3.3 — Settings Screen (P0)

As a user, I want to manage my connection and account from settings.

**Acceptance criteria:**

- Show current server URL
- Show current user email (from `GET /api/account/me`)
- Sign Out button (clears Keychain, returns to onboarding)
- App version info

## IOS-3.4 — Offline Deck Cache (P1)

As a user, I want to browse my decks and cards when offline.

**Acceptance criteria:**

- DeckRepository fetches from API when online, caches in SwiftData
- When offline, serve from SwiftData cache
- Show "offline" indicator when not connected
- Cache refreshed on each online access
