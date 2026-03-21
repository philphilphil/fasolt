# Universal Search — Design Spec

## Overview

A unified search experience accessible from the header search bar or `⌘K`. Searches across cards, decks, and files with live results grouped by type, powered by Postgres full-text search.

## UX Flow

1. User clicks the existing search input in `TopBar.vue` or presses `⌘K`
2. Input becomes editable and focused
3. As the user types (300ms debounce), a dropdown appears below the input
4. Results are grouped by type: **Cards**, **Decks**, **Files**
5. Matching text is highlighted with `<mark>` tags
6. Keyboard navigation: `↑↓` to move between results, `Enter` to open, `Escape` to close
7. Click-outside closes the dropdown
8. Navigation on result click:
   - Cards → `/cards` (with query param to highlight/select the card)
   - Decks → `/decks/:id`
   - Files → `/files/:id`
9. Minimum 2 characters to trigger search
10. Empty results show "No results found"

## Backend

### Database Migration

Add stored generated tsvector columns with GIN indexes:

- **Card**: `SearchVector` — generated from `Front` and `Back` using `'english'` text search config
- **Deck**: `SearchVector` — generated from `Name` and `Description` using `'english'` config
- **MarkdownFile**: `SearchVector` — generated from `FileName` using `'simple'` config (no stemming for filenames)

SQL pattern for generated column:
```sql
ALTER TABLE "Cards" ADD COLUMN "SearchVector" tsvector
  GENERATED ALWAYS AS (to_tsvector('english', coalesce("Front",'') || ' ' || coalesce("Back",''))) STORED;
CREATE INDEX "IX_Cards_SearchVector" ON "Cards" USING gin ("SearchVector");
```

### Search Endpoint

`GET /api/search?q={query}`

- Requires authentication
- Validates: `q` must be at least 2 characters
- Converts query to tsquery using `plainto_tsquery`
- Runs 3 queries in parallel (cards, decks, files), all scoped to authenticated user
- Cards query respects existing soft-delete filter
- Returns max 10 results per type, ranked by `ts_rank`
- Uses `ts_headline` to generate highlighted snippets with `<mark>` tags

### Response Shape

```json
{
  "cards": [
    {
      "id": "guid",
      "headline": "What is <mark>React</mark> context API?",
      "cardType": "section",
      "state": "learning"
    }
  ],
  "decks": [
    {
      "id": "guid",
      "headline": "<mark>React</mark> Fundamentals",
      "cardCount": 12
    }
  ],
  "files": [
    {
      "id": "guid",
      "headline": "<mark>react</mark>-hooks-notes.md"
    }
  ]
}
```

### DTOs

```csharp
record SearchResponse(
    List<CardSearchResult> Cards,
    List<DeckSearchResult> Decks,
    List<FileSearchResult> Files);

record CardSearchResult(Guid Id, string Headline, string CardType, string State);
record DeckSearchResult(Guid Id, string Headline, int CardCount);
record FileSearchResult(Guid Id, string Headline);
```

### Endpoint Registration

New file `Api/Endpoints/SearchEndpoints.cs` following existing pattern:
- Static class with `MapSearchEndpoints()` extension method
- Registered in `Program.cs`

## Frontend

### Components

**Modified: `TopBar.vue`**
- Remove `readonly` from the search input
- Add `v-model` binding and focus/blur handlers
- Embed `SearchResults.vue` dropdown below the input
- Register `⌘K` keyboard shortcut to focus the input

**New: `SearchResults.vue`**
- Receives search results and loading state as props
- Renders grouped results (Cards, Decks, Files sections)
- Each section header with count
- Result items show highlighted text (rendered as HTML via `v-html`)
- Keyboard navigation state (active index)
- Emits `select` event with result type + id

**New: `composables/useSearch.ts`**
- Reactive `query` ref
- Debounced API call (300ms) using `watchDebounced` from VueUse or manual setTimeout
- `results` ref holding the `SearchResponse`
- `isLoading` ref
- `isOpen` computed (query length >= 2)
- Keyboard navigation logic (activeIndex, onKeyDown handler)
- `close()` method to reset state

### API Client

Add to `api/client.ts` or new `api/search.ts`:
```typescript
export async function searchAll(query: string): Promise<SearchResponse>
```

### Navigation

On result selection:
- Cards: `router.push({ path: '/cards', query: { highlight: id } })`
- Decks: `router.push(`/decks/${id}`)`
- Files: `router.push(`/files/${id}`)`

CardsView should read the `highlight` query param and scroll to / visually highlight the matching card.

## Searchable Fields

| Entity       | Fields               | FTS Config |
|-------------|----------------------|------------|
| Card        | Front, Back          | english    |
| Deck        | Name, Description    | english    |
| MarkdownFile| FileName             | simple     |

## Out of Scope

- Tag/group filtering (tags not yet implemented)
- File content search
- Search history / recent searches
- Search analytics
