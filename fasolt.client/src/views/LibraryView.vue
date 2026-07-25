<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useLibraryStore } from '@/stores/library'
import { useAuthStore } from '@/stores/auth'
import type { LibrarySort } from '@/types'
import PublicNav from '@/components/PublicNav.vue'
import AppFooter from '@/components/AppFooter.vue'

const route = useRoute()
const router = useRouter()
const library = useLibraryStore()
const auth = useAuthStore()

const query = ref(typeof route.query.q === 'string' ? route.query.q : '')
const loadError = ref('')

const sort = computed<LibrarySort>(() => (route.query.sort === 'recent' ? 'recent' : 'popular'))
const page = computed(() => {
  const raw = Number(route.query.page)
  return Number.isFinite(raw) && raw > 1 ? Math.floor(raw) : 1
})
const activeQuery = computed(() => (typeof route.query.q === 'string' ? route.query.q : ''))

const pageCount = computed(() =>
  library.pageSize > 0 ? Math.max(1, Math.ceil(library.totalCount / library.pageSize)) : 1,
)

let debounceTimer: ReturnType<typeof setTimeout> | null = null

async function load() {
  loadError.value = ''
  try {
    await library.fetchDecks({ q: activeQuery.value, sort: sort.value, page: page.value })
  } catch {
    loadError.value = 'Could not load the library. Please try again.'
  }
}

onMounted(load)
watch(() => [activeQuery.value, sort.value, page.value], load)

// Keep the box in sync when the URL changes from the outside (back button,
// a shared link). The query watcher below no-ops because nothing differs.
watch(activeQuery, (value) => {
  if (value !== query.value.trim()) query.value = value
})

// Typing rewrites the URL in place — search-as-you-type shouldn't stack up
// history entries, but the resulting URL still has to be shareable.
watch(query, (value) => {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    const trimmed = value.trim()
    if (trimmed === activeQuery.value) return
    router.replace({ query: buildQuery({ q: trimmed || undefined, page: undefined }) })
  }, 300)
})

onBeforeUnmount(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
})

function buildQuery(overrides: Record<string, string | undefined>) {
  const next: Record<string, string> = {}
  const current = {
    q: activeQuery.value || undefined,
    sort: route.query.sort === 'recent' ? 'recent' : undefined,
    page: page.value > 1 ? String(page.value) : undefined,
    ...overrides,
  }
  for (const [key, value] of Object.entries(current)) {
    if (value) next[key] = value
  }
  return next
}

function setSort(value: LibrarySort) {
  if (value === sort.value) return
  router.push({ query: buildQuery({ sort: value === 'recent' ? 'recent' : undefined, page: undefined }) })
}

function goToPage(value: number) {
  const target = Math.min(Math.max(1, value), pageCount.value)
  if (target === page.value) return
  router.push({ query: buildQuery({ page: target > 1 ? String(target) : undefined }) })
  window.scrollTo({ top: 0 })
}
</script>

<template>
  <div :class="auth.isAuthenticated ? '' : 'flex min-h-screen flex-col bg-paper-0 text-ink-0'">
    <PublicNav v-if="!auth.isAuthenticated" />

    <!-- Signed in, AppLayout already provides the <main> landmark; nesting a second one is invalid. -->
    <component
      :is="auth.isAuthenticated ? 'div' : 'main'"
      :class="auth.isAuthenticated ? '' : 'mx-auto w-full max-w-5xl flex-1 px-6 py-12'"
    >
      <div class="library-page">
        <header>
          <h1 class="page-title">Deck library</h1>
          <p class="page-sub">
            Public decks shared by other fasolt users. Import any of them into your own account.
          </p>
        </header>

        <div class="library-controls">
          <div class="search-shell">
            <svg class="search-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>
            <input
              v-model="query"
              type="search"
              class="search-field"
              placeholder="Search decks…"
              aria-label="Search public decks"
            >
          </div>
          <div class="sort-toggle" role="group" aria-label="Sort decks">
            <button
              type="button"
              class="fa-pill"
              :class="{ 'is-active': sort === 'popular' }"
              :aria-pressed="sort === 'popular'"
              @click="setSort('popular')"
            >
              Popular
            </button>
            <button
              type="button"
              class="fa-pill"
              :class="{ 'is-active': sort === 'recent' }"
              :aria-pressed="sort === 'recent'"
              @click="setSort('recent')"
            >
              Recent
            </button>
          </div>
        </div>

        <div v-if="loadError" class="empty">{{ loadError }}</div>

        <div v-else-if="library.loading" class="empty">Loading decks…</div>

        <div v-else-if="library.decks.length === 0" class="empty">
          <!-- A ?page= past the end also lands here, and the pagination controls below
               are hidden, so offer the way back explicitly. -->
          <template v-if="library.totalCount > 0 && page > 1">
            <p>Page {{ page }} is past the end of the library.</p>
            <button type="button" class="fa-btn empty-action" @click="goToPage(1)">Back to page 1</button>
          </template>
          <template v-else-if="activeQuery">No decks match “{{ activeQuery }}”.</template>
          <template v-else>No public decks yet. Publish one of yours to get the library started.</template>
        </div>

        <template v-else>
          <div class="library-meta">
            <span class="meta-text">
              <strong>{{ library.totalCount.toLocaleString() }}</strong>
              {{ library.totalCount === 1 ? 'deck' : 'decks' }}
              <template v-if="activeQuery"> matching “{{ activeQuery }}”</template>
            </span>
          </div>

          <div class="deck-list">
            <RouterLink
              v-for="deck in library.decks"
              :key="deck.id"
              :to="`/library/${deck.id}`"
              class="deck-row"
            >
              <div class="deck-row-text">
                <div class="deck-row-name">{{ deck.name }}</div>
                <div v-if="deck.description" class="deck-row-sub">{{ deck.description }}</div>
                <div class="deck-row-facts">
                  <span v-if="deck.authorHandle" class="deck-handle">@{{ deck.authorHandle }}</span>
                  <span v-if="deck.authorHandle" class="fact-sep">·</span>
                  <span><span class="fa-num">{{ deck.cardCount.toLocaleString() }}</span> {{ deck.cardCount === 1 ? 'card' : 'cards' }}</span>
                  <template v-if="deck.copyCount > 0">
                    <span class="fact-sep">·</span>
                    <span><span class="fa-num">{{ deck.copyCount.toLocaleString() }}</span> {{ deck.copyCount === 1 ? 'import' : 'imports' }}</span>
                  </template>
                </div>
              </div>
              <svg class="deck-chevron" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m9 6 6 6-6 6"/></svg>
            </RouterLink>
          </div>

          <div v-if="pageCount > 1" class="pagination">
            <button type="button" class="fa-btn" :disabled="page <= 1" @click="goToPage(page - 1)">
              Previous
            </button>
            <span class="page-indicator">Page <span class="fa-num">{{ page }}</span> of <span class="fa-num">{{ pageCount }}</span></span>
            <button type="button" class="fa-btn" :disabled="page >= pageCount" @click="goToPage(page + 1)">
              Next
            </button>
          </div>
        </template>
      </div>
    </component>

    <AppFooter v-if="!auth.isAuthenticated" />
  </div>
</template>

<style scoped>
.library-page {
  padding: 40px 8px 48px;
  max-width: 760px;
  margin: 0 auto;
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 22px;
}
.page-sub {
  margin: 6px 0 0;
  font-size: 13.5px;
  color: var(--ink-2);
}

.library-controls {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}
.search-shell {
  flex: 1 1 260px;
  display: flex;
  align-items: center;
  gap: 10px;
  height: 32px;
  padding: 0 12px;
  border: 1px solid var(--rule-1);
  border-radius: 8px;
  background: var(--paper-1);
  color: var(--ink-2);
  transition: border-color .12s, background .12s;
}
.search-shell:focus-within {
  border-color: var(--accent);
  background: var(--paper-0);
}
.search-icon { flex-shrink: 0; }
.search-field {
  flex: 1;
  min-width: 0;
  border: none;
  outline: none;
  background: transparent;
  color: var(--ink-0);
  font: inherit;
  font-size: 13px;
}
.search-field::-webkit-search-cancel-button { -webkit-appearance: none; }
.sort-toggle { display: flex; gap: 6px; }

.library-meta { font-size: 13px; color: var(--ink-1); }
.meta-text strong { color: var(--ink-0); font-weight: 600; }

/* Deck list — rows on the page, hairline separators, no outer box */
.deck-list { display: flex; flex-direction: column; }
.deck-row {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 15px 4px;
  border-bottom: 1px solid var(--rule-1);
  text-decoration: none;
  color: inherit;
  border-radius: 8px;
  transition: background .12s;
}
.deck-row:hover { background: var(--paper-2); }
.deck-row:focus-visible { outline: 2px solid var(--accent); outline-offset: -2px; }
.deck-row-text { flex: 1; min-width: 0; }
.deck-row-name {
  font-size: 15.5px;
  color: var(--ink-0);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.deck-row-sub {
  font-size: 12.5px;
  color: var(--ink-1);
  margin-top: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.deck-row-facts {
  display: flex;
  align-items: baseline;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 4px;
  font-size: 12px;
  color: var(--ink-2);
}
.deck-handle { color: var(--accent-text); }
.fact-sep { color: var(--ink-3); }
.deck-chevron { flex: none; color: var(--ink-3); }

.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 14px;
  padding-top: 6px;
}
.page-indicator { font-size: 13px; color: var(--ink-2); }

.empty {
  padding: 40px;
  text-align: center;
  color: var(--ink-2);
  font-size: 13px;
}
.empty p { margin: 0; }
.empty-action { margin-top: 12px; }
</style>
