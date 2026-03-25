# Misc Bugs & UI Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 14 UI bugs and improvements from GitHub issue #20 — unified card table, card detail tweaks, dark mode toggle in nav, search improvements, MCP route change, display name removal, and iOS foreground refresh.

**Architecture:** All changes are isolated per-area. The card table unification extracts a shared component. Backend changes are limited to removing DisplayName and expanding the UpdateCard endpoint. iOS adds a scenePhase observer.

**Tech Stack:** Vue 3, TypeScript, TanStack Table, shadcn-vue, Tailwind CSS 3, .NET 10, EF Core, SwiftUI

**Spec:** `docs/superpowers/specs/2026-03-25-misc-bugs-ui-fixes-design.md`

---

### Task 1: Search improvements — reorder and resize

**Files:**
- Modify: `fasolt.client/src/components/SearchResults.vue`
- Modify: `fasolt.client/src/composables/useSearch.ts`

- [ ] **Step 1: Update SearchResults.vue — increase max height and reorder sections**

Change `max-h-[400px]` to `max-h-[1200px]`. Move the Decks section before the Cards section. Update the border-top conditional to be on Cards instead of Decks.

```vue
<!-- In SearchResults.vue, line 46: change max-h -->
<!-- Old: max-h-[400px] -->
<!-- New: max-h-[1200px] -->

<!-- Reorder: Decks block first, then Cards block -->
<!-- Move lines 83-101 (Decks section) before lines 62-80 (Cards section) -->
<!-- Update the border-t class: move it from Decks header to Cards header -->
```

The Decks section (currently lines 83-101) should come first. The Cards section (currently lines 62-80) should come second. Update the `border-t` conditional:
- Decks header: remove `:class="{ 'border-t border-border': results.cards.length > 0 }"`
- Cards header: add `:class="{ 'border-t border-border': results.decks.length > 0 }"`

- [ ] **Step 2: Update useSearch.ts — reorder flatItems to decks first**

In `useSearch.ts` line 20-25, change the flatItems computed to put decks before cards:

```typescript
const flatItems = computed<SearchItem[]>(() => {
  const items: SearchItem[] = []
  for (const deck of results.value.decks) items.push({ type: 'deck', data: deck })
  for (const card of results.value.cards) items.push({ type: 'card', data: card })
  return items
})
```

- [ ] **Step 3: Test in browser**

Run: Start the app, press ⌘K, search for something that returns both cards and decks. Verify:
- Decks appear above cards
- The result window is much taller
- Keyboard navigation still works (up/down/enter/escape)

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/components/SearchResults.vue fasolt.client/src/composables/useSearch.ts
git commit -m "fix: reorder search results (decks first) and increase dropdown height (#20)"
```

---

### Task 2: MCP route change

**Files:**
- Modify: `fasolt.client/src/router/index.ts`
- Modify: `fasolt.client/src/layouts/AppLayout.vue`
- Modify: `fasolt.client/src/components/BottomNav.vue`

- [ ] **Step 1: Update router**

In `fasolt.client/src/router/index.ts`, change the MCP route path:

```typescript
// Old:
{ path: '/mcp', name: 'mcp', component: () => import('@/views/McpView.vue') },
// New:
{ path: '/mcp-setup', name: 'mcp', component: () => import('@/views/McpView.vue') },
```

- [ ] **Step 2: Update AppLayout nav tab**

In `fasolt.client/src/layouts/AppLayout.vue`, update the tabs array:

```typescript
// Old:
{ label: 'MCP', value: '/mcp' },
// New:
{ label: 'MCP', value: '/mcp-setup' },
```

- [ ] **Step 3: Update BottomNav**

In `fasolt.client/src/components/BottomNav.vue`, update the tabs array:

```typescript
// Old:
{ name: 'MCP', path: '/mcp', icon: '⏚' },
// New:
{ name: 'MCP', path: '/mcp-setup', icon: '⏚' },
```

- [ ] **Step 4: Test in browser**

Navigate to `/mcp-setup` — should show the MCP help page. Navigation tabs should link correctly. `/mcp` should 404 (this is correct — the backend MCP endpoint lives there now without conflict).

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/src/router/index.ts fasolt.client/src/layouts/AppLayout.vue fasolt.client/src/components/BottomNav.vue
git commit -m "fix: move MCP help page to /mcp-setup to avoid endpoint conflict (#20)"
```

---

### Task 3: Dark mode toggle in nav bar

**Files:**
- Modify: `fasolt.client/src/composables/useDarkMode.ts`
- Modify: `fasolt.client/src/components/TopBar.vue`
- Modify: `fasolt.client/src/views/SettingsView.vue`

- [ ] **Step 1: Simplify useDarkMode composable**

Replace the three-way cycle with a simple light/dark toggle. System is the default (no localStorage entry). Once the user clicks, it stores their choice.

```typescript
import { ref, onMounted, onUnmounted } from 'vue'

const STORAGE_KEY = 'fasolt-theme'

// Shared state across all component instances
const isDark = ref(false)

export function useDarkMode() {
  let mediaQuery: MediaQueryList | null = null
  let handler: ((e: MediaQueryListEvent) => void) | null = null

  function apply() {
    const stored = localStorage.getItem(STORAGE_KEY)
    let shouldBeDark: boolean
    if (stored === 'dark') {
      shouldBeDark = true
    } else if (stored === 'light') {
      shouldBeDark = false
    } else {
      // System default
      shouldBeDark = mediaQuery?.matches ?? false
    }
    isDark.value = shouldBeDark
    document.documentElement.classList.toggle('dark', shouldBeDark)
  }

  function toggle() {
    // Toggle to opposite of current state and persist
    const newTheme = isDark.value ? 'light' : 'dark'
    localStorage.setItem(STORAGE_KEY, newTheme)
    apply()
  }

  onMounted(() => {
    mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
    handler = () => apply()
    mediaQuery.addEventListener('change', handler)
    apply()
  })

  onUnmounted(() => {
    if (mediaQuery && handler) {
      mediaQuery.removeEventListener('change', handler)
    }
  })

  return { isDark, toggle }
}
```

- [ ] **Step 2: Add toggle button to TopBar**

In `fasolt.client/src/components/TopBar.vue`, import `useDarkMode` and add a sun/moon icon button next to the user menu.

Add to script setup:

```typescript
import { useDarkMode } from '@/composables/useDarkMode'
const { isDark, toggle } = useDarkMode()
```

Add before the user menu dropdown (inside the `<div class="flex items-center gap-1.5">` wrapper):

```vue
<Button
  variant="ghost"
  size="sm"
  class="h-8 w-8 p-0"
  aria-label="Toggle dark mode"
  @click="toggle"
>
  <!-- Sun icon (shown in dark mode → click for light) -->
  <svg v-if="isDark" xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="5"/><line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/><line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/></svg>
  <!-- Moon icon (shown in light mode → click for dark) -->
  <svg v-else xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>
</Button>
```

- [ ] **Step 3: Remove Appearance card from SettingsView**

In `fasolt.client/src/views/SettingsView.vue`:
- Remove the `useDarkMode` import and the `const { theme, setTheme } = useDarkMode()` line
- Remove the entire Appearance `<Card>` block (the last card in the template)

- [ ] **Step 4: Test in browser**

- Click the moon/sun icon in the top-right nav — should toggle theme
- Check Settings page — Appearance section should be gone
- Refresh page — theme preference should persist
- Clear localStorage `fasolt-theme` — should follow system preference

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/src/composables/useDarkMode.ts fasolt.client/src/components/TopBar.vue fasolt.client/src/views/SettingsView.vue
git commit -m "feat: move dark mode toggle to nav bar with sun/moon icon (#20)"
```

---

### Task 4: Display name removal — backend

**Files:**
- Modify: `fasolt.Server/Domain/Entities/AppUser.cs`
- Modify: `fasolt.Server/Application/Dtos/AccountDtos.cs`
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`
- Modify: `fasolt.Server/Application/Dtos/AdminDtos.cs`
- Modify: `fasolt.Server/Application/Services/AdminService.cs`
- Modify: `fasolt.Server/Infrastructure/Data/DevSeedData.cs`
- Create: EF Core migration

- [ ] **Step 1: Remove DisplayName from AppUser entity**

In `fasolt.Server/Domain/Entities/AppUser.cs`, remove the `DisplayName` property:

```csharp
using Microsoft.AspNetCore.Identity;

namespace Fasolt.Server.Domain.Entities;

public class AppUser : IdentityUser
{
}
```

- [ ] **Step 2: Remove DisplayName from DTOs**

In `fasolt.Server/Application/Dtos/AccountDtos.cs`:
- Change `UserInfoResponse` to remove DisplayName parameter:
  ```csharp
  public record UserInfoResponse(string Email, bool IsAdmin);
  ```
- Remove the `UpdateProfileRequest` record entirely

In `fasolt.Server/Application/Dtos/AdminDtos.cs`:
- Remove DisplayName from AdminUserDto:
  ```csharp
  public record AdminUserDto(
      string Id,
      string Email,
      int CardCount,
      int DeckCount,
      bool IsLockedOut);
  ```

- [ ] **Step 3: Update AccountEndpoints**

In `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`:
- Remove the `MapPut("/profile", UpdateProfile)` route registration (line 16)
- Remove the entire `UpdateProfile` method (lines 40-53)
- Update `GetMe` to not pass DisplayName:
  ```csharp
  return Results.Ok(new UserInfoResponse(user.Email!, isAdmin));
  ```
- Update `ConfirmEmailChange` to not pass DisplayName:
  ```csharp
  return Results.Ok(new UserInfoResponse(user.Email!, isAdmin));
  ```

- [ ] **Step 4: Update AdminService**

In `fasolt.Server/Application/Services/AdminService.cs`, remove DisplayName from the Select projection:

```csharp
.Select(u => new AdminUserDto(
    u.Id,
    u.Email!,
    _db.Cards.Count(c => c.UserId == u.Id),
    _db.Decks.Count(d => d.UserId == u.Id),
    u.LockoutEnabled && u.LockoutEnd > DateTimeOffset.UtcNow))
```

- [ ] **Step 5: Update DevSeedData**

In `fasolt.Server/Infrastructure/Data/DevSeedData.cs`, remove the `DisplayName` lines from both user creation blocks:

```csharp
// Admin user — remove: DisplayName = "Dev User",
var adminUser = new AppUser
{
    UserName = DevEmail,
    Email = DevEmail,
    EmailConfirmed = true,
};

// Regular user — remove: DisplayName = "Regular User",
var regularUser = new AppUser
{
    UserName = RegularEmail,
    Email = RegularEmail,
    EmailConfirmed = true,
};
```

- [ ] **Step 6: Create EF Core migration**

```bash
cd /Users/phil/Projects/fasolt && dotnet ef migrations add RemoveDisplayName --project fasolt.Server
```

- [ ] **Step 7: Verify build**

```bash
cd /Users/phil/Projects/fasolt && dotnet build fasolt.Server
```

- [ ] **Step 8: Commit**

```bash
git add fasolt.Server/
git commit -m "refactor: remove DisplayName from user model and endpoints (#20)"
```

---

### Task 5: Display name removal — frontend

**Files:**
- Modify: `fasolt.client/src/stores/auth.ts`
- Modify: `fasolt.client/src/components/TopBar.vue`
- Modify: `fasolt.client/src/views/SettingsView.vue`

- [ ] **Step 1: Update auth store**

In `fasolt.client/src/stores/auth.ts`:
- Remove `displayName` from the `User` interface:
  ```typescript
  interface User {
    email: string
    isAdmin: boolean
  }
  ```
- Remove the entire `updateProfile` function
- Remove `updateProfile` from the return statement

- [ ] **Step 2: Update TopBar**

In `fasolt.client/src/components/TopBar.vue`:
- Update `userInitial` to only use email:
  ```typescript
  const userInitial = computed(() => {
    if (auth.user?.email) return auth.user.email[0].toUpperCase()
    return '?'
  })
  ```
- Update `userLabel` to only use email:
  ```typescript
  const userLabel = computed(() => auth.user?.email || '')
  ```

- [ ] **Step 3: Remove display name from SettingsView**

In `fasolt.client/src/views/SettingsView.vue`:
- Remove all display name refs and function: `displayName`, `displayNameSuccess`, `displayNameError`, `saveDisplayName`
- Remove the `displayName.value = auth.user?.displayName || ''` from onMounted
- Remove the entire "Display name" `<Card>` block from the template

- [ ] **Step 4: Test in browser**

- Login → TopBar should show email initial
- Settings page should not have Display name card
- User menu dropdown should show email

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/src/stores/auth.ts fasolt.client/src/components/TopBar.vue fasolt.client/src/views/SettingsView.vue
git commit -m "refactor: remove display name from frontend (#20)"
```

---

### Task 6: Card detail view improvements

**Files:**
- Modify: `fasolt.client/src/views/CardDetailView.vue`
- Modify: `fasolt.Server/Application/Dtos/CardDtos.cs`
- Modify: `fasolt.Server/Api/Endpoints/CardEndpoints.cs` or `fasolt.Server/Application/Services/CardService.cs`
- Modify: `fasolt.client/src/stores/cards.ts`

- [ ] **Step 1: Make deck names clickable links**

In `fasolt.client/src/views/CardDetailView.vue`, update the metadata section (line 157). Replace the plain deck name text with RouterLinks:

```vue
<!-- Old: -->
<span v-if="card.decks.length > 0">Decks: <span class="text-foreground">{{ card.decks.map(d => d.name).join(', ') }}</span></span>

<!-- New: -->
<span v-if="card.decks.length > 0">Decks:
  <template v-for="(d, i) in card.decks" :key="d.id">
    <RouterLink :to="`/decks/${d.id}`" class="text-foreground hover:text-accent transition-colors">{{ d.name }}</RouterLink><span v-if="i < card.decks.length - 1">, </span>
  </template>
</span>
```

- [ ] **Step 2: Move metadata to dedicated lines below header**

Restructure the metadata block. Keep it at the same location but make source+section on one line and decks on the next:

```vue
<!-- Metadata -->
<div class="space-y-1 text-xs text-muted-foreground">
  <div v-if="card.sourceFile || card.sourceHeading" class="flex flex-wrap gap-x-6">
    <span v-if="card.sourceFile">Source: <span class="text-foreground">{{ card.sourceFile }}</span></span>
    <span v-if="card.sourceHeading">Section: <span class="text-foreground">{{ card.sourceHeading }}</span></span>
  </div>
  <div v-if="card.decks.length > 0">
    Decks:
    <template v-for="(d, i) in card.decks" :key="d.id">
      <RouterLink :to="`/decks/${d.id}`" class="text-foreground hover:text-accent transition-colors">{{ d.name }}</RouterLink><span v-if="i < card.decks.length - 1">, </span>
    </template>
  </div>
</div>
```

- [ ] **Step 3: Expand UpdateCardRequest to accept source and deck fields**

In `fasolt.Server/Application/Dtos/CardDtos.cs`, add optional fields:

```csharp
public record UpdateCardRequest(
    string Front,
    string Back,
    string? FrontSvg = null,
    string? BackSvg = null,
    string? SourceFile = null,
    string? SourceHeading = null,
    List<string>? DeckIds = null);
```

Then update the card update logic in the service/endpoint to apply SourceFile, SourceHeading, and DeckIds when provided. For DeckIds: clear existing DeckCard entries and add new ones matching the provided IDs.

Check `fasolt.Server/Application/Services/CardService.cs` for the update method and extend it to handle the new fields.

- [ ] **Step 4: Update frontend card store updateCard method**

In `fasolt.client/src/stores/cards.ts`, expand the `updateCard` data parameter:

```typescript
async function updateCard(id: string, data: {
  front: string
  back: string
  frontSvg?: string | null
  backSvg?: string | null
  sourceFile?: string | null
  sourceHeading?: string | null
  deckIds?: string[]
}): Promise<Card> {
```

- [ ] **Step 5: Add source and deck editing to CardDetailView edit mode**

In the edit mode section of `CardDetailView.vue`, add input fields for source file, source heading, and a deck multi-select before the front/back textareas.

Add refs in script setup:

```typescript
const editSourceFile = ref('')
const editSourceHeading = ref('')
const editDeckIds = ref<string[]>([])
```

Import the decks store:

```typescript
import { useDecksStore } from '@/stores/decks'
const decksStore = useDecksStore()
```

Fetch decks on mount (add to the onMounted):

```typescript
decksStore.fetchDecks()
```

Update `startEdit()`:

```typescript
function startEdit() {
  if (!card.value) return
  front.value = card.value.front
  back.value = card.value.back
  editFrontSvg.value = card.value.frontSvg ?? ''
  editBackSvg.value = card.value.backSvg ?? ''
  editSourceFile.value = card.value.sourceFile ?? ''
  editSourceHeading.value = card.value.sourceHeading ?? ''
  editDeckIds.value = card.value.decks.map(d => d.id)
  error.value = ''
  editing.value = true
}
```

Update `save()` to pass the new fields:

```typescript
card.value = await cardsStore.updateCard(card.value.id, {
  front: front.value,
  back: back.value,
  frontSvg: editFrontSvg.value || null,
  backSvg: editBackSvg.value || null,
  sourceFile: editSourceFile.value || null,
  sourceHeading: editSourceHeading.value || null,
  deckIds: editDeckIds.value,
})
```

Add template fields in the edit mode section, before the Front textarea:

```vue
<div class="grid grid-cols-2 gap-3">
  <div class="space-y-1">
    <label class="text-[11px] font-medium text-muted-foreground">Source file</label>
    <Input v-model="editSourceFile" placeholder="e.g. notes.md" class="h-8 text-xs" />
  </div>
  <div class="space-y-1">
    <label class="text-[11px] font-medium text-muted-foreground">Section</label>
    <Input v-model="editSourceHeading" placeholder="e.g. Chapter 1" class="h-8 text-xs" />
  </div>
</div>
<div class="space-y-1">
  <label class="text-[11px] font-medium text-muted-foreground">Decks</label>
  <div class="flex flex-wrap gap-2">
    <label
      v-for="d in decksStore.decks"
      :key="d.id"
      class="flex items-center gap-1.5 text-xs cursor-pointer"
    >
      <input
        type="checkbox"
        :checked="editDeckIds.includes(d.id)"
        class="rounded border-border"
        @change="editDeckIds.includes(d.id) ? editDeckIds = editDeckIds.filter(id => id !== d.id) : editDeckIds.push(d.id)"
      />
      {{ d.name }}
    </label>
  </div>
</div>
```

- [ ] **Step 6: Test in browser**

- Open a card detail page
- Verify deck names are clickable and navigate to the deck page
- Verify source and section appear on their own line, decks below
- Click Edit — verify source, section, and deck checkboxes appear
- Change values and save — verify they persist

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Application/Dtos/CardDtos.cs fasolt.Server/Application/Services/CardService.cs fasolt.Server/Api/Endpoints/CardEndpoints.cs fasolt.client/src/views/CardDetailView.vue fasolt.client/src/stores/cards.ts
git commit -m "feat: add source/section/deck editing to card detail view (#20)"
```

---

### Task 7: Unified card table — extract shared component

**Files:**
- Create: `fasolt.client/src/components/CardTable.vue`
- Modify: `fasolt.client/src/views/CardsView.vue`
- Modify: `fasolt.client/src/views/DeckDetailView.vue`

- [ ] **Step 1: Create CardTable.vue component**

Create `fasolt.client/src/components/CardTable.vue`. This component receives cards via props and renders a TanStack table with configurable columns and actions.

Props:
- `cards: Card[] | DeckCard[]` — the card data to display
- `showDecks: boolean` — whether to show the Decks column (true in /cards, false in /decks)
- `showPagination: boolean` — whether to paginate (true in /cards, false in /decks)
- `pageSize: number` — default 20

Events:
- `edit(card)` — edit button clicked
- `delete(card)` — delete button clicked
- `remove(card)` — remove from deck clicked (only in deck view)
- `addToDeck(card)` — `+` button clicked (only in cards view)

The component includes:
- Columns: Front (with source subtitle), State badge, Decks (conditional), Due (formatted `dd.mm.yyyy`), Actions (conditional)
- Sorting via TanStack
- Due date formatted as `dd.mm.yyyy` with enough column width (`w-[100px]`)

```vue
<script setup lang="ts">
import { h, ref } from 'vue'
import { RouterLink } from 'vue-router'
import type { ColumnDef, SortingState } from '@tanstack/vue-table'
import {
  FlexRender,
  getCoreRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useVueTable,
} from '@tanstack/vue-table'
import { valueUpdater } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'

interface CardRow {
  id: string
  front: string
  sourceFile?: string | null
  sourceHeading?: string | null
  frontSvg?: string | null
  backSvg?: string | null
  state: string
  dueAt?: string | null
  decks?: { id: string; name: string }[]
}

const props = withDefaults(defineProps<{
  cards: CardRow[]
  showDecks?: boolean
  showPagination?: boolean
  pageSize?: number
}>(), {
  showDecks: false,
  showPagination: false,
  pageSize: 20,
})

const emit = defineEmits<{
  edit: [card: CardRow]
  delete: [card: CardRow]
  remove: [card: CardRow]
  addToDeck: [card: CardRow]
}>()

function formatDue(iso: string | null | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  const dd = String(d.getDate()).padStart(2, '0')
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const yyyy = d.getFullYear()
  return `${dd}.${mm}.${yyyy}`
}

const columns: ColumnDef<CardRow>[] = [
  {
    accessorKey: 'front',
    header: 'Front',
    cell: ({ row }) => {
      const val = row.getValue('front') as string
      const display = val.length > 80 ? val.slice(0, 80) + '…' : val
      const source = row.original.sourceFile
      return h('div', { class: 'min-w-0' }, [
        h(RouterLink, {
          to: `/cards/${row.original.id}`,
          class: 'hover:text-accent transition-colors',
        }, () => display),
        source
          ? h('div', { class: 'truncate text-[11px] text-muted-foreground mt-0.5' }, source)
          : null,
      ])
    },
  },
  {
    accessorKey: 'state',
    header: 'State',
    meta: { className: 'w-[80px]' },
    cell: ({ row }) => h(Badge, { variant: 'outline', class: 'text-[10px]' }, () => row.getValue('state')),
  },
  ...(props.showDecks ? [{
    id: 'decks',
    header: 'Decks',
    meta: { className: 'w-[160px]' },
    accessorFn: (row: CardRow) => (row.decks ?? []).map(d => d.name).join(', '),
    cell: ({ row }: any) => {
      const deckList = row.original.decks ?? []
      return h('div', { class: 'flex flex-wrap gap-1' }, [
        ...deckList.map((d: any) => h(Badge, { key: d.id, variant: 'outline', class: 'text-[10px] whitespace-nowrap' }, () => d.name)),
        h('button', {
          class: 'text-[10px] text-muted-foreground hover:text-accent',
          onClick: () => emit('addToDeck', row.original),
        }, '+'),
      ])
    },
  } as ColumnDef<CardRow>] : []),
  {
    accessorKey: 'dueAt',
    header: 'Due',
    meta: { className: 'w-[100px]' },
    cell: ({ row }) => h('span', { class: 'text-muted-foreground whitespace-nowrap' }, formatDue(row.getValue('dueAt') as string | null)),
  },
  {
    id: 'actions',
    enableSorting: false,
    enableHiding: false,
    meta: { className: 'w-[120px]' },
    cell: ({ row }) => {
      const card = row.original
      const buttons = [
        h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-[10px]', onClick: () => emit('edit', card) }, () => 'Edit'),
      ]
      if (!props.showDecks) {
        // Deck detail view: add Remove button
        buttons.push(
          h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-[10px] text-muted-foreground', onClick: () => emit('remove', card) }, () => 'Remove')
        )
      }
      buttons.push(
        h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-[10px] text-muted-foreground hover:text-destructive', onClick: () => { emit('delete', card) } }, () => '×')
      )
      return h('div', { class: 'flex gap-1' }, buttons)
    },
  },
]

const sorting = ref<SortingState>([])

const table = useVueTable({
  get data() { return props.cards },
  columns,
  getCoreRowModel: getCoreRowModel(),
  getSortedRowModel: getSortedRowModel(),
  ...(props.showPagination ? { getPaginationRowModel: getPaginationRowModel() } : {}),
  onSortingChange: updaterOrValue => valueUpdater(updaterOrValue, sorting),
  state: {
    get sorting() { return sorting.value },
  },
  initialState: {
    pagination: { pageSize: props.pageSize },
  },
})
</script>

<template>
  <div>
    <div class="rounded border border-border/60">
      <Table>
        <TableHeader>
          <TableRow v-for="headerGroup in table.getHeaderGroups()" :key="headerGroup.id">
            <TableHead
              v-for="header in headerGroup.headers"
              :key="header.id"
              class="h-9 text-[10px] uppercase tracking-[0.15em] text-muted-foreground cursor-pointer select-none"
              :class="(header.column.columnDef.meta as any)?.className"
              @click="header.column.getCanSort() ? header.column.toggleSorting() : undefined"
            >
              <div class="flex items-center gap-1">
                <FlexRender
                  v-if="!header.isPlaceholder"
                  :render="header.column.columnDef.header"
                  :props="header.getContext()"
                />
                <span v-if="header.column.getIsSorted() === 'asc'" class="text-accent">↑</span>
                <span v-else-if="header.column.getIsSorted() === 'desc'" class="text-accent">↓</span>
              </div>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <template v-if="table.getRowModel().rows?.length">
            <TableRow v-for="row in table.getRowModel().rows" :key="row.id" class="text-xs hover:bg-accent/5">
              <TableCell v-for="cell in row.getVisibleCells()" :key="cell.id" :class="(cell.column.columnDef.meta as any)?.className">
                <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
              </TableCell>
            </TableRow>
          </template>
          <template v-else>
            <TableRow>
              <TableCell :colspan="columns.length" class="h-24 text-center text-xs text-muted-foreground">
                <slot name="empty">No cards.</slot>
              </TableCell>
            </TableRow>
          </template>
        </TableBody>
      </Table>
    </div>

    <!-- Pagination -->
    <div v-if="showPagination && table.getPageCount() > 1" class="flex items-center justify-between text-[11px] text-muted-foreground mt-4">
      <span>{{ table.getFilteredRowModel().rows.length }} card(s)</span>
      <div class="flex items-center gap-2">
        <Button variant="outline" size="sm" class="h-7 text-[10px]" :disabled="!table.getCanPreviousPage()" @click="table.previousPage()">
          Previous
        </Button>
        <span>Page {{ table.getState().pagination.pageIndex + 1 }} of {{ table.getPageCount() }}</span>
        <Button variant="outline" size="sm" class="h-7 text-[10px]" :disabled="!table.getCanNextPage()" @click="table.nextPage()">
          Next
        </Button>
      </div>
    </div>
  </div>
</template>
```

- [ ] **Step 2: Update CardsView to use CardTable**

Refactor `fasolt.client/src/views/CardsView.vue`:
- Remove the inline TanStack table setup, column definitions, and table template
- Import and use `CardTable` with `showDecks` and `showPagination` set to true
- Keep the toolbar (filters) and dialogs
- Add the Active checkbox and Deck filter dropdown to the toolbar
- Pass filtered cards to `CardTable` instead of using the internal table filtering for active/deck filters

Add active filter state:

```typescript
const activeOnly = ref(true)
const deckFilter = ref('')
```

Add computed for filtered cards:

```typescript
const filteredCards = computed(() => {
  let result = cardsStore.cards

  // Active filter: hide cards belonging only to inactive decks
  if (activeOnly.value) {
    result = result.filter(card => {
      if (card.decks.length === 0) return true // no deck = active
      return card.decks.some(d => {
        const deck = decks.decks.find(dd => dd.id === d.id)
        return deck ? deck.isActive : true
      })
    })
  }

  // Deck filter
  if (deckFilter.value === 'none') {
    result = result.filter(card => card.decks.length === 0)
  } else if (deckFilter.value) {
    result = result.filter(card => card.decks.some(d => d.id === deckFilter.value))
  }

  // Text filter
  if (filterValue.value) {
    const q = filterValue.value.toLowerCase()
    result = result.filter(card => card.front.toLowerCase().includes(q))
  }

  // Source filter
  if (sourceFilter.value) {
    result = result.filter(card => card.sourceFile === sourceFilter.value)
  }

  // State filter
  if (stateFilter.value) {
    result = result.filter(card => card.state === stateFilter.value)
  }

  return result
})
```

Add Active checkbox and Deck filter to the toolbar template:

```vue
<label class="flex items-center gap-1.5 text-xs cursor-pointer">
  <input type="checkbox" v-model="activeOnly" class="rounded border-border" />
  Active
</label>
<select
  v-model="deckFilter"
  class="h-8 rounded border border-border bg-transparent px-2 text-xs text-foreground"
>
  <option value="">All decks</option>
  <option value="none">None (no deck)</option>
  <option v-for="d in decks.decks" :key="d.id" :value="d.id">{{ d.name }}</option>
</select>
```

Replace the table section with:

```vue
<CardTable
  :cards="filteredCards"
  show-decks
  show-pagination
  @edit="openEdit"
  @delete="(card) => deleteTarget = card"
  @add-to-deck="(card) => addToDeckCard = card"
>
  <template #empty>No cards yet. Create one or use the MCP agent to generate cards from your notes.</template>
</CardTable>
```

Update the delete dialog to show deck names:

```vue
<DialogDescription>
  <template v-if="deleteTarget?.decks?.length">
    This card will be permanently deleted and removed from: <strong>{{ deleteTarget.decks.map(d => d.name).join(', ') }}</strong>.
  </template>
  <template v-else>
    This card will be permanently deleted.
  </template>
</DialogDescription>
```

- [ ] **Step 3: Update DeckDetailView to use CardTable**

Refactor `fasolt.client/src/views/DeckDetailView.vue`:
- Remove the inline TanStack table setup, column definitions, and table template
- Import and use `CardTable`
- Add edit and delete card support (import `CardEditDialog` and wire up events)

Import the cards store and edit dialog:

```typescript
import { useCardsStore } from '@/stores/cards'
import CardEditDialog from '@/components/CardEditDialog.vue'

const cardsStore = useCardsStore()
const editTarget = ref<DeckCard | null>(null)
const editOpen = ref(false)
const deleteCardTarget = ref<DeckCard | null>(null)
const deleteCardError = ref('')
```

Add handlers:

```typescript
function openEditCard(card: DeckCard) {
  editTarget.value = card
  editOpen.value = true
}

async function confirmDeleteCard() {
  if (!deleteCardTarget.value) return
  deleteCardError.value = ''
  try {
    await cardsStore.deleteCard(deleteCardTarget.value.id)
    deleteCardTarget.value = null
    await refresh()
  } catch {
    deleteCardError.value = 'Failed to delete card.'
  }
}
```

Replace the card table section with:

```vue
<CardTable
  v-if="deck.cards.length > 0"
  :cards="deck.cards"
  @edit="openEditCard"
  @delete="(card) => deleteCardTarget = card"
  @remove="(card) => removeCard(card.id)"
>
  <template #empty>No cards in this deck yet.</template>
</CardTable>
```

Add the edit and delete card dialogs:

```vue
<CardEditDialog
  v-model:open="editOpen"
  :card="editTarget"
  @updated="refresh()"
/>

<Dialog :open="!!deleteCardTarget" @update:open="deleteCardTarget = null">
  <DialogContent>
    <DialogHeader>
      <DialogTitle>Delete card</DialogTitle>
      <DialogDescription>
        This card will be permanently deleted.
      </DialogDescription>
    </DialogHeader>
    <div v-if="deleteCardError" class="text-xs text-destructive">{{ deleteCardError }}</div>
    <DialogFooter>
      <Button variant="outline" @click="deleteCardTarget = null">Cancel</Button>
      <Button variant="destructive" @click="confirmDeleteCard">Delete</Button>
    </DialogFooter>
  </DialogContent>
</Dialog>
```

- [ ] **Step 4: Test in browser**

- `/cards`: Verify columns are Front (with source), State, Decks, Due (`dd.mm.yyyy`), Actions (Edit, ×)
- `/cards`: Verify Active checkbox hides cards from inactive decks only
- `/cards`: Verify Deck filter works including "None"
- `/cards`: Verify delete dialog shows deck names
- `/decks/:id`: Verify columns are Front (with source), State, Due (`dd.mm.yyyy`), Actions (Edit, Remove, ×)
- `/decks/:id`: Verify Edit opens dialog, Remove removes from deck, × deletes card

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/src/components/CardTable.vue fasolt.client/src/views/CardsView.vue fasolt.client/src/views/DeckDetailView.vue
git commit -m "feat: unify card tables with shared CardTable component (#20)"
```

---

### Task 8: iOS foreground refresh

**Files:**
- Modify: `fasolt.ios/Fasolt/FasoltApp.swift`

- [ ] **Step 1: Add scenePhase observer to FasoltApp**

In `fasolt.ios/Fasolt/FasoltApp.swift`, add a `@Environment(\.scenePhase)` property and observe transitions to `.active`:

```swift
import SwiftUI
import SwiftData

@main
struct FasoltApp: App {
    @State private var authService = AuthService()
    @State private var networkMonitor = NetworkMonitor()
    @Environment(\.scenePhase) private var scenePhase
    @State private var lastRefresh: Date = .distantPast

    var body: some Scene {
        WindowGroup {
            Group {
                if authService.isAuthenticated {
                    MainTabView()
                } else {
                    OnboardingView()
                }
            }
            .animation(.default, value: authService.isAuthenticated)
            .onChange(of: scenePhase) { oldPhase, newPhase in
                if newPhase == .active && oldPhase != .active {
                    let now = Date()
                    if now.timeIntervalSince(lastRefresh) > 30 {
                        lastRefresh = now
                        NotificationCenter.default.post(name: .appDidBecomeActive, object: nil)
                    }
                }
            }
        }
        .environment(authService)
        .environment(networkMonitor)
        .modelContainer(for: [Card.self, CachedDeck.self, PendingReview.self])
    }
}

extension Notification.Name {
    static let appDidBecomeActive = Notification.Name("appDidBecomeActive")
}
```

- [ ] **Step 2: Add refresh observers to view models**

In each view model that loads data (DashboardViewModel, DeckListViewModel, CardListViewModel), add an observer for the notification. For example, in `DashboardViewModel`:

```swift
// Add to init or the view's .onAppear/.task:
NotificationCenter.default.addObserver(forName: .appDidBecomeActive, object: nil, queue: .main) { [weak self] _ in
    Task { @MainActor in
        await self?.refresh()
    }
}
```

Alternatively, the views themselves can listen using `.onReceive(NotificationCenter.default.publisher(for: .appDidBecomeActive))` and call their view model's load/refresh method.

- [ ] **Step 3: Build and verify**

```bash
cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -sdk iphonesimulator -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5
```

- [ ] **Step 4: Commit**

```bash
git add fasolt.ios/
git commit -m "feat: auto-refresh iOS app data when returning from background (#20)"
```

---

### Task 9: Final Playwright UI test

**Files:** None (test only)

- [ ] **Step 1: Start the full stack**

Ensure `./dev.sh` is running.

- [ ] **Step 2: Run Playwright tests covering the changes**

Test the following flows:
1. Login as `dev@fasolt.local` / `Dev1234!`
2. Navigate to `/cards` — verify Active checkbox exists and is checked, verify Deck filter dropdown exists
3. Verify card table columns: Front, State, Decks, Due, Actions
4. Navigate to a deck detail page — verify columns: Front, State, Due, Actions (with Edit, Remove, ×)
5. Click the dark mode toggle in the nav bar — verify theme switches
6. Navigate to `/mcp-setup` — verify the MCP help page loads
7. Open search (⌘K), search for a term — verify decks appear before cards
8. Navigate to Settings — verify no Display Name or Appearance sections
9. Open a card detail — verify deck names are clickable links

- [ ] **Step 3: Commit any test files if needed**

```bash
git commit -m "test: add Playwright tests for misc UI fixes (#20)"
```

- [ ] **Step 4: Close the GitHub issue**

```bash
gh issue close 20
```
