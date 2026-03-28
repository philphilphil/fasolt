# Study UX Improvements Design

**Date:** 2026-03-28
**Issues:** #43, #44, #46

## Overview

Three related study UX improvements shipped together: deck selection on iOS dashboard, early-exit summary screen, and wider card content on both platforms.

---

## #43 — iOS: Deck selection on Dashboard

### What changes

Add an "active decks with due cards" section to `DashboardView`, below the existing hero card and stat pills.

### Behavior

- Show active decks that have due cards (dueCount > 0)
- Each row displays: deck name, total card count, due count badge
- Tapping a deck row launches `StudyView` with that deck's ID
- The hero "Study Now" button remains unchanged — studies all due cards across all decks
- Section hidden if no decks have due cards

### Data source

`DashboardViewModel` already fetches overview stats. Decks with due counts come from `DeckRepository.fetchDecks()` (GET `/api/decks`), which returns `dueCount` per deck. Add a deck fetch to the dashboard load flow.

### Layout

Matches the web StudyView pattern:
- Section label: "Study by deck" (small uppercase tracking)
- Deck rows: name left-aligned, due badge right-aligned
- Tap target: full row

---

## #44 — iOS: Show summary on early exit

### What changes

When the user taps the X button during a study session, show `StudySummaryView` with stats instead of dismissing immediately.

### Behavior

- If the user has studied at least 1 card: set session state to `.summary`, rendering `StudySummaryView` with current stats (cards studied, rating breakdown, skipped count)
- If no cards studied yet (X tapped immediately): dismiss the view without summary
- The "Done" button on the summary screen dismisses as before

### Implementation

The X button's action currently calls `dismiss()`. Change it to check `studyViewModel.totalStudied > 0`:
- If > 0: set state to `.summary`
- If 0: call `dismiss()`

No new views needed — `StudySummaryView` already handles partial stats correctly since it reads from the ViewModel's running counters.

---

## #46 — Wider card content display

### Web changes

- `ReviewCard.vue`: Increase content max-width from `max-w-lg` (512px) to `max-w-2xl` (672px)
- `StudyView.vue`: Increase container from `max-w-[480px]` to `max-w-2xl` (672px) to match

### iOS changes

- `StudyView` card padding: Reduce horizontal padding from 24pt to 16pt
- Keep vertical padding at 24pt (or reduce to 20pt if it looks better with less horizontal)

### Scope

Only the study/review card display is affected. Card list views, detail views, and dashboard are not changed.
