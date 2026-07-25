# Deck Sharing & Public Library — Design

GitHub issue: #133. Approved 2026-07-25 (conversation review, section by section).

## Goal

Users can share decks (unlisted link or public library listing). Others import a shared deck
either as a **copy** (decoupled clone, fully owned) or as a **link** (live subscription that
follows the owner's updates). A public, anonymously viewable library page lists public decks
and acts as a signup funnel.

## Decisions (settled — do not re-litigate)

- V1 ships copy + link + public library together (web). iOS gets a read-only library
  (browse + import, no publishing) in a follow-up App Store release. Android later.
- Open publishing: any verified user can publish; admin can unlist decks and ban users
  from publishing. Caps: ≤ 1000 cards per published deck, ≤ 20 public decks per user.
- Publishing requires a unique account-level handle (`AppUser.Handle`), claimed once, web-only.
- Link mode syncs automatically — subscribers always see the owner's current deck.
- Public library and deck pages are fully anonymous-viewable with SEO/OG meta.
- Data model: **Approach B** — SRS state is split out of `Card` into a per-user
  `ReviewState` table. Linked decks reference the owner's actual cards; sync is free.

## Data model

### ReviewState (new)

Composite PK `(UserId, CardId)`. Columns moved from `Card`: `Stability`, `Difficulty`,
`Step`, `DueAt`, `State`, `LastReviewedAt`, `IsSuspended`. Created **lazily on first
review** (or first suspend); a card with no ReviewState row for a user is "new".
Cascade delete with Card and User.

`Card` keeps: `Id`, `PublicId`, `UserId` (author), `SourceFile`, `Front`, `Back`,
`FrontSvg`, `BackSvg`, `CreatedAt`, `DeckCards`, `SearchVector`.

Migration backfills one `ReviewState(card.UserId, card.Id)` per existing card that has
been reviewed or suspended (i.e., not pristine-new), then drops the SRS columns from
`cards`. `ReviewLog` already has its own `UserId` — unchanged.

### Deck additions

- `Visibility` enum: `Private` (default) | `Unlisted` (anyone with link can view/import)
  | `Public` (listed in library). Plus `PublishedAt`.
- `Deck.IsSuspended` remains the owner's own toggle.
- Copy attribution: `CopiedFromDeckPublicId` (string, nullable) + `CopiedFromHandle`
  (snapshot, nullable) on Deck.

### DeckSubscription (new)

Composite PK `(UserId, DeckId)` — this IS a linked deck. `SubscribedAt`, `IsSuspended`
(subscriber's own pause, independent of owner). Subscriber's deck list = own decks +
subscriptions. Owner deletes a card → subscribers' ReviewState rows cascade. Owner
unpublishes (visibility → Private) or deletes the deck → subscriptions are removed.

### Copy import & convert-to-copy

Copy: clone deck + new Card rows owned by importer (fresh SRS, `SourceFile` = null),
attribution fields set. Convert link → copy: same clone, but re-key the subscriber's
existing ReviewState rows to the new card IDs in one transaction (SRS preserved), then
remove the subscription.

### AppUser

Nullable unique `Handle`: 3–30 chars, lowercase alphanumeric + hyphen. Plus
`CanPublish` (default true) for admin bans.

## API

Anonymous:
- `GET /api/library` — list/search public decks; pagination; sort `popular`
  (subscriber+copy count) | `recent`; full-text search on name/description.
- `GET /api/library/decks/{publicId}` — detail for Public/Unlisted decks: name,
  description, author handle, card count, sample card previews (front+back).
  `SourceFile` is never exposed on any public surface.

Authenticated:
- `POST /api/library/decks/{publicId}/copy`
- `POST /api/library/decks/{publicId}/subscribe`, `DELETE …/subscribe`
- `POST /api/decks/{id}/convert-to-copy`
- `PUT /api/decks/{id}/visibility` — Public requires handle + caps + `CanPublish`.
- `PUT /api/account/handle`

Admin (existing pattern): `POST /api/admin/decks/{id}/unlist`, per-user `CanPublish` toggle.

Behavior changes:
- `ReviewService`: due queue = authored cards + cards in subscribed decks, left-joined
  with my ReviewState (no row = new). Suspension: my `ReviewState.IsSuspended`; owner's
  `Deck.IsSuspended` for own decks; `DeckSubscription.IsSuspended` for linked decks.
- `CardService`/`DeckService`: content edits/deletes require `Card.UserId == me`;
  subscribers get 403 on linked content. `SearchService` includes subscribed decks' cards.
- Review submission upserts ReviewState (the lazy-creation point).
- Rate limiting: existing `api` limiter on import/subscribe/publish.

## Web frontend

Public routes (no auth guard), Things-anchored marigold design language:
- `/library` — search box, sort toggle (popular/recent), deck cards (name, description,
  @handle, card count).
- `/library/:publicId` — deck page: metadata, author, flip-able sample cards, CTAs
  **Copy** and **Link**. Logged out → CTAs go to register/login with `returnTo`.

Logged-in:
- Owner deck detail gains a Share section: visibility selector, share URL, handle prompt
  on first publish.
- Linked decks: subscribed badge + author handle, read-only card list, pause toggle,
  "Convert to copy" action.

SEO: backend middleware injects per-route `<title>`, meta description, OG tags into
`index.html` for `/library` and `/library/:publicId` (extend the #75 pattern); public
decks added to sitemap. No full SSR in v1.

## MCP

- `ListPublicDecks` (query, sort, pagination)
- `ImportDeck` (publicId, mode: copy | link)
- `PublishDeck` (visibility; returns share URL; structured error if no handle — direct
  user to set one on the web app)
- `ListDecks`/`GetOverview`: add `isLinked` + author handle on subscribed decks.
- `UpdateCards`/`DeleteCards`: structured error on linked content.

## iOS (follow-up release)

Library tab: browse/search, deck detail with previews, Copy/Link import. Linked decks:
badge, pause, convert-to-copy. No publishing UI. Study flow unchanged.

## Testing

- Migration: backfill exactly one ReviewState per reviewed/suspended card; golden-data
  test proving due queues are identical before/after the refactor.
- ReviewService: lazy creation, subscribed cards as new, per-user suspension isolation.
- Lifecycle: subscribe → study → owner edits/deletes → unpublish; convert-to-copy
  preserves SRS exactly.
- AuthZ: subscriber 403 on linked edits; anonymous limited to Public/Unlisted; caps;
  handle validation.
- Playwright: publish flow (incl. handle prompt), anonymous library browse, signup via
  deck CTA, copy import, link import, study linked deck, convert to copy.

## Rollout — 4 sequential PRs

1. **ReviewState refactor** — schema split + migration + service updates. Zero behavior
   change (golden-data test). Lands alone.
2. **Publishing & library** — visibility, handle, copy import, public API, library
   pages, SEO, admin unlist/ban, caps.
3. **Link mode** — DeckSubscription, subscribe/unsubscribe, convert-to-copy, review
   queue integration.
4. **MCP tools + iOS library.**

DB snapshot before deploying PR 1's migration is the rollback story.
