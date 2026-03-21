# Epic 4: Spaced Repetition Study — Design Spec

## Scope

US-4.1 (Study due cards), US-4.2 (SM-2 scheduling), US-4.3 (Session summary), US-4.7 (Markdown rendering in review). P0 + P1 minus US-4.4 (study by group — depends on Epic 5).

US-4.4 (study by group), US-4.5 (daily limit), US-4.6 (undo), US-4.8 (suspend/bury) deferred.

## Decisions

- **SM-2 fields on Card entity**: EaseFactor, Interval, Repetitions, DueAt, State added directly to Card table.
- **Again cards stay in session**: Failed cards reappear later in the same session until rated Good+.
- **Session is client-side**: Backend provides due cards and accepts ratings. Session state (queue, failed cards, progress) lives in the frontend review store.
- **Markdown rendering in review**: Use existing `useMarkdown` composable with `v-html`.

## Database Schema Changes

Add to existing `Cards` table:

| Column | Type | Default | Notes |
|--------|------|---------|-------|
| EaseFactor | double | 2.5 | Minimum 1.3 per SM-2 |
| Interval | int | 0 | Days until next review |
| Repetitions | int | 0 | Consecutive successful reviews |
| DueAt | DateTimeOffset? | null | Null = new card (never studied) |
| State | string(20) | `new` | `new`, `learning`, `mature` |

Index on `(UserId, DueAt)` for due card queries.

Migration adds columns with defaults — existing cards become `new` state with `DueAt = null`.

## SM-2 Algorithm

`Sm2Algorithm` in `Application/Services/`:

**`Calculate(double easeFactor, int interval, int repetitions, int quality)`** returns `(double newEaseFactor, int newInterval, int newRepetitions, string state)`:

- If quality < 3 (Again or Hard with quality 0 or 2):
  - Reset: repetitions = 0, interval = 0
  - Again (quality 0): interval stays 0 (card stays in session, re-queued)
  - Hard (quality 2): interval = 1 day
- If quality >= 3 (Good = 4, Easy = 5):
  - repetitions++
  - If repetitions == 1: interval = 1
  - If repetitions == 2: interval = 6
  - If repetitions >= 3: interval = round(interval * easeFactor)
- Ease factor adjustment: EF' = EF + (0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02))
- Minimum ease factor: 1.3
- State: `new` if never studied, `learning` if repetitions < 3 or EF < 2.0, `mature` if repetitions >= 3 and EF >= 2.0

## Backend API

### POST /api/review/rate

Submit a rating for a card.

- Input: `{ cardId, quality }` where quality is 0 (Again), 2 (Hard), 4 (Good), or 5 (Easy)
- Validates card belongs to user and is not deleted
- Runs SM-2 algorithm with current card state + quality
- Updates card: EaseFactor, Interval, Repetitions, DueAt (now + interval days), State
- For Again (quality 0): DueAt set to now (stays available in session)
- Response: `{ cardId, easeFactor, interval, repetitions, dueAt, state }`

### GET /api/review/due

Get cards due for review.

- Query params: `?limit=50` (default 50)
- Returns cards where `DueAt IS NULL` (new) OR `DueAt <= now`
- Ordered by: DueAt ASC (oldest due first), then new cards
- Response: array of `{ id, front, back, cardType, sourceHeading, fileId, state, dueAt }`

### GET /api/review/stats

Get review statistics for dashboard.

- Response: `{ dueCount, totalCards, studiedToday, streak }`
- `dueCount`: cards where DueAt is null or <= now
- `totalCards`: all non-deleted cards
- `studiedToday`: cards with DueAt changed today (approximation: cards where DueAt > today and DueAt was updated today — simplified: count cards with Repetitions > 0 and DueAt between tomorrow-start and future)
- `streak`: consecutive days with at least one review — simplified for MVP: just return 0, wire properly when we have review history

## Frontend

### Update Card type

Add SM-2 fields to `Card` interface:

```typescript
export interface Card {
  id: string
  fileId: string | null
  sourceHeading: string | null
  front: string
  back: string
  cardType: 'file' | 'section' | 'custom'
  createdAt: string
  easeFactor: number
  interval: number
  repetitions: number
  dueAt: string | null
  state: 'new' | 'learning' | 'mature'
}
```

### Review Store (rewrite)

Replace mock review store with API-backed store:

- `dueCards`: fetched from `GET /api/review/due`
- `sessionQueue`: working queue including re-queued Again cards
- `currentCard`: front of queue
- `isFlipped`, `isActive`, `isComplete`
- `sessionStats`: { reviewed, again, hard, good, easy, startTime }
- `startSession()`: fetch due cards, initialize queue
- `flipCard()`, `rate(quality)`: flip state, call `POST /api/review/rate`, advance queue
- Again cards go to back of queue instead of being removed
- Session ends when queue is empty

### ReviewView + ReviewCard (wire to real data)

- Remove mock cards
- Call `review.startSession()` on mount
- `ReviewCard` renders card.front/back with `useMarkdown` (v-html) for markdown rendering (US-4.7)
- Session summary already exists in `SessionComplete` component — wire to `sessionStats`

### Navigation

- Add review entry point: "Study" button on dashboard and/or in the nav
- Show due count badge somewhere visible

### New: ReviewEndpoints.cs

All under `/api/review`, all require authorization.
