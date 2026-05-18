<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import { useCardsStore } from '@/stores/cards'
import { useDecksStore } from '@/stores/decks'
import type { Card } from '@/types'
import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import CardCreateDialog from '@/components/CardCreateDialog.vue'
import CardDeleteDialog from '@/components/CardDeleteDialog.vue'
import CardTable from '@/components/CardTable.vue'
import BulkActionBar from '@/components/BulkActionBar.vue'
import AddToDeckDialog from '@/components/AddToDeckDialog.vue'
import RemoveFromDeckDialog from '@/components/RemoveFromDeckDialog.vue'

const route = useRoute()
const cardsStore = useCardsStore()
const decks = useDecksStore()

// Single-card dialogs (delete only — single-card add-to-deck is gone, replaced
// by bulk Move to deck)
const deleteTarget = ref<Card | null>(null)
const deleteOpen = ref(false)
const createOpen = ref(false)

// Bulk
const selectedIds = ref<string[]>([])
const addToDeckOpen = ref(false)
const removeFromDeckOpen = ref(false)
const bulkDeleteOpen = ref(false)
const bulkBusy = ref(false)
const bulkError = ref('')

// Filters
const filterValue = ref('')
const sourceFilter = ref('')
const stateFilter = ref('')
const hideSuspended = ref(true)
const deckFilter = ref('')

onMounted(async () => {
  await Promise.all([cardsStore.fetchCards(), decks.fetchDecks()])
  const sf = route.query.sourceFile
  if (sf && typeof sf === 'string') sourceFilter.value = sf
})

function onCardDeleted() {
  deleteTarget.value = null
  deleteOpen.value = false
}

const filteredCards = computed(() => {
  let result = cardsStore.cards
  if (hideSuspended.value) result = result.filter(c => !c.isSuspended)
  if (deckFilter.value === 'none') result = result.filter(c => c.decks.length === 0)
  else if (deckFilter.value) result = result.filter(c => c.decks.some(d => d.id === deckFilter.value))
  if (filterValue.value) {
    const q = filterValue.value.toLowerCase()
    result = result.filter(c => c.front.toLowerCase().includes(q) || c.back.toLowerCase().includes(q))
  }
  if (sourceFilter.value) result = result.filter(c => c.sourceFile === sourceFilter.value)
  if (stateFilter.value) result = result.filter(c => c.state === stateFilter.value)
  return result
})

const selectedCards = computed(() =>
  selectedIds.value
    .map(id => cardsStore.cards.find(c => c.id === id))
    .filter((c): c is Card => !!c)
)
const selectedCount = computed(() => selectedCards.value.length)

const someSuspended = computed(() => selectedCards.value.some(c => c.isSuspended))
const allSuspended = computed(() => selectedCards.value.length > 0 && selectedCards.value.every(c => c.isSuspended))
const canRemoveFromDeck = computed(() => selectedCards.value.some(c => c.decks.length > 0))

const totalCards = computed(() => cardsStore.cards.length)
const stateCounts = computed(() => {
  const out = { review: 0, learning: 0, new: 0, relearning: 0 }
  for (const c of cardsStore.cards) if (c.state in out) out[c.state as keyof typeof out]++
  return out
})

const deckFilterLabel = computed(() => {
  if (deckFilter.value === '') return 'All decks'
  if (deckFilter.value === 'none') return 'No deck'
  return decks.decks.find(d => d.id === deckFilter.value)?.name || '—'
})
const stateFilterLabel = computed(() => stateFilter.value || 'any')

function clearFilters() {
  filterValue.value = ''
  sourceFilter.value = ''
  stateFilter.value = ''
  deckFilter.value = ''
}
const hasActiveFilters = computed(() =>
  !!(filterValue.value || sourceFilter.value || stateFilter.value || deckFilter.value)
)

// ---- Bulk actions ----
async function bulkSuspend(target: boolean) {
  bulkBusy.value = true
  bulkError.value = ''
  try {
    const toChange = selectedCards.value
      .filter(c => c.isSuspended !== target)
      .map(c => c.id)
    await cardsStore.setSuspendedBulk(toChange, target)
  } catch {
    bulkError.value = `Failed to ${target ? 'suspend' : 'unsuspend'} cards.`
  } finally {
    bulkBusy.value = false
  }
}

async function bulkDeleteConfirmed() {
  bulkBusy.value = true
  bulkError.value = ''
  try {
    await cardsStore.deleteCardsBulk(selectedIds.value)
    selectedIds.value = []
    bulkDeleteOpen.value = false
  } catch {
    bulkError.value = 'Failed to delete cards. The list has been refreshed.'
    selectedIds.value = []
    await cardsStore.fetchCards()
  } finally {
    bulkBusy.value = false
  }
}

async function onDeckMembershipChanged() {
  // After add/remove, refetch so deck chips and counts reflect reality.
  // Gate the bulk bar so a second action can't dispatch against a stale
  // selection while the refetch is still in flight.
  bulkBusy.value = true
  try {
    await cardsStore.fetchCards()
  } finally {
    selectedIds.value = []
    bulkBusy.value = false
  }
}
</script>

<template>
  <div class="cards-page">
    <header class="cards-head">
      <div class="cards-title">
        <h1 class="page-title fa-serif">Cards</h1>
        <span class="fa-mono cards-summary">
          {{ totalCards.toLocaleString() }} total ·
          {{ stateCounts.review }} review ·
          {{ stateCounts.learning + stateCounts.relearning }} learning ·
          {{ stateCounts.new }} new
        </span>
      </div>
      <div class="head-actions">
        <button class="fa-btn fa-btn-primary" @click="createOpen = true">
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14M5 12h14"/></svg>
          New card
        </button>
      </div>
    </header>

    <!-- Filter bar -->
    <div class="filter-bar">
      <div class="search-pill">
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" style="color: var(--ink-2);"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>
        <input v-model="filterValue" class="search-input-bare" type="text" placeholder="Search front, back, or source…" />
      </div>

      <span class="fa-vrule" style="height:16px;" />

      <label class="filter-pill" :class="{ 'is-active': deckFilter !== '' }">
        <span class="fa-cap pill-label">Deck</span>
        <select v-model="deckFilter" class="pill-select">
          <option value="">All decks</option>
          <option value="none">No deck</option>
          <option v-for="d in decks.decks" :key="d.id" :value="d.id">{{ d.name }}</option>
        </select>
        <span class="pill-value">{{ deckFilterLabel }}</span>
        <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" style="color: var(--ink-3);"><path d="m6 9 6 6 6-6"/></svg>
      </label>

      <label class="filter-pill" :class="{ 'is-active': stateFilter !== '' }">
        <span class="fa-cap pill-label">State</span>
        <select v-model="stateFilter" class="pill-select">
          <option value="">any</option>
          <option value="new">new</option>
          <option value="learning">learning</option>
          <option value="review">review</option>
          <option value="relearning">relearning</option>
        </select>
        <span class="pill-value">{{ stateFilterLabel }}</span>
        <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" style="color: var(--ink-3);"><path d="m6 9 6 6 6-6"/></svg>
      </label>

      <label class="filter-pill" :class="{ 'is-active': sourceFilter !== '' }">
        <span class="fa-cap pill-label">Source</span>
        <input v-model="sourceFilter" type="text" placeholder="any" class="pill-input" />
      </label>

      <span class="fa-vrule" style="height:16px;" />

      <label class="filter-check">
        <input type="checkbox" v-model="hideSuspended" />
        <span class="check-box">
          <svg v-if="hideSuspended" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="m5 12 5 5L20 7"/></svg>
        </span>
        <span class="check-label">Hide suspended</span>
      </label>

      <button v-if="hasActiveFilters" class="filter-clear" @click="clearFilters">Clear</button>
    </div>

    <!-- Bulk action bar — only rendered when something's selected -->
    <BulkActionBar
      v-if="selectedCount > 0"
      :count="selectedCount"
      :some-suspended="someSuspended"
      :all-suspended="allSuspended"
      :can-remove-from-deck="canRemoveFromDeck"
      :busy="bulkBusy"
      @add-to-deck="addToDeckOpen = true"
      @remove-from-deck="removeFromDeckOpen = true"
      @suspend="bulkSuspend(true)"
      @unsuspend="bulkSuspend(false)"
      @delete="bulkDeleteOpen = true"
      @clear="selectedIds = []"
    />

    <!-- Result count + sort -->
    <div v-else class="result-row">
      <span class="fa-mono">
        Showing {{ filteredCards.length.toLocaleString() }} of {{ totalCards.toLocaleString() }}
      </span>
      <span class="fa-mono result-hint">Select rows to suspend, change decks, or delete in bulk</span>
    </div>

    <CardTable
      v-model:selectedIds="selectedIds"
      :cards="filteredCards"
      show-decks
      show-pagination
      selectable
      @delete="(card) => { deleteTarget = card; deleteOpen = true }"
      @suspend="(card) => cardsStore.setSuspended(card.id, !card.isSuspended)"
    >
      <template #empty>
        No cards match. Try clearing filters, or ask your AI to generate cards from your notes.
      </template>
    </CardTable>

    <CardCreateDialog
      v-model:open="createOpen"
      :initial-fronts="['']"
      initial-back=""
      @created="cardsStore.fetchCards()"
    />

    <CardDeleteDialog
      v-model:open="deleteOpen"
      :card="deleteTarget"
      @deleted="onCardDeleted"
    />

    <AddToDeckDialog
      v-model:open="addToDeckOpen"
      :card-ids="selectedIds"
      @added="onDeckMembershipChanged"
    />

    <RemoveFromDeckDialog
      v-model:open="removeFromDeckOpen"
      :cards="selectedCards"
      @removed="onDeckMembershipChanged"
    />

    <!-- Bulk delete confirm -->
    <Dialog :open="bulkDeleteOpen" @update:open="bulkDeleteOpen = $event">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete {{ selectedCount }} card{{ selectedCount === 1 ? '' : 's' }}?</DialogTitle>
          <DialogDescription>
            This permanently removes the cards and their review history. This action cannot be undone.
          </DialogDescription>
        </DialogHeader>
        <div v-if="bulkError" class="bulk-error">{{ bulkError }}</div>
        <DialogFooter>
          <Button variant="outline" :disabled="bulkBusy" @click="bulkDeleteOpen = false">Cancel</Button>
          <Button variant="destructive" :disabled="bulkBusy" @click="bulkDeleteConfirmed">
            {{ bulkBusy ? 'Deleting…' : `Delete ${selectedCount}` }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>

<style scoped>
.cards-page {
  padding: 24px 0 40px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}
.cards-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 14px;
  flex-wrap: wrap;
}
.cards-title {
  display: flex;
  align-items: baseline;
  gap: 14px;
  flex-wrap: wrap;
}
.page-title {
  font-size: 34px;
  line-height: 1;
  letter-spacing: -0.02em;
  margin: 0;
  color: var(--ink-0);
}
.cards-summary { font-size: 12px; color: var(--ink-2); }
.head-actions { display: flex; gap: 8px; }

.filter-bar {
  display: flex;
  gap: 10px;
  align-items: center;
  padding: 10px 12px;
  border: 1px solid var(--rule-1);
  border-radius: 10px;
  background: var(--paper-1);
  flex-wrap: wrap;
}
.search-pill {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  min-width: 200px;
}
.search-input-bare {
  flex: 1;
  border: none;
  outline: none;
  background: transparent;
  font: inherit;
  color: var(--ink-0);
  font-size: 13px;
}
.search-input-bare::placeholder { color: var(--ink-3); }

.filter-pill {
  position: relative;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border: 1px solid var(--rule-1);
  border-radius: 999px;
  background: transparent;
  color: var(--ink-1);
  font-size: 12px;
  cursor: pointer;
  transition: border-color .12s, background .12s;
}
.filter-pill:hover { border-color: var(--ink-3); }
.filter-pill.is-active {
  border-color: var(--accent);
  background: var(--accent-soft);
}
.filter-pill.is-active .pill-label { color: var(--accent); }
.filter-pill.is-active .pill-value { color: var(--accent-hi); }
.pill-label { font-size: 9px; color: var(--ink-2); }
.pill-value { color: var(--ink-0); }
.pill-select {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
  font: inherit;
  border: none;
}
.pill-input {
  border: none;
  outline: none;
  background: transparent;
  font: inherit;
  color: var(--ink-0);
  font-size: 12px;
  width: 80px;
}
.pill-input::placeholder { color: var(--ink-3); }

.filter-check {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--ink-1);
  cursor: pointer;
  user-select: none;
}
.filter-check input { display: none; }
.check-box {
  width: 14px;
  height: 14px;
  border: 1px solid var(--rule-1);
  border-radius: 3px;
  background: var(--paper-1);
  display: grid;
  place-items: center;
  color: var(--accent-on);
  transition: background .12s, border-color .12s;
}
.filter-check input:checked + .check-box {
  background: var(--accent);
  border-color: var(--accent);
}

.filter-clear {
  background: none;
  border: none;
  font: inherit;
  font-size: 12px;
  color: var(--ink-2);
  cursor: pointer;
  text-decoration: underline;
  text-decoration-color: var(--rule-1);
  text-underline-offset: 3px;
  transition: color .12s;
}
.filter-clear:hover { color: var(--ink-0); }

.result-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 11px;
  color: var(--ink-2);
  padding: 0 4px;
}
.result-hint { color: var(--ink-3); }

.bulk-error {
  font-size: 12px;
  color: var(--c-again);
  padding: 6px 0;
}
</style>
