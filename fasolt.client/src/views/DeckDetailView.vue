<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { useDecksStore } from '@/stores/decks'
import { useCardsStore } from '@/stores/cards'
import type { DeckDetail } from '@/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog'
import { Checkbox } from '@/components/ui/checkbox'
import { History } from 'lucide-vue-next'
import CardTable from '@/components/CardTable.vue'
import BulkActionBar from '@/components/BulkActionBar.vue'
import AddToDeckDialog from '@/components/AddToDeckDialog.vue'

const route = useRoute()
const router = useRouter()
const decks = useDecksStore()
const cardsStore = useCardsStore()

const deck = ref<DeckDetail | null>(null)
const loading = ref(true)

const editOpen = ref(false)
const editName = ref('')
const editDescription = ref('')

const deleteOpen = ref(false)
const deleteCards = ref(false)
const deleteError = ref('')

const idCopied = ref(false)

// Bulk selection
const selectedIds = ref<string[]>([])
const addToDeckOpen = ref(false)
const bulkDeleteOpen = ref(false)
const bulkBusy = ref(false)
const bulkError = ref('')

const selectedCards = computed(() =>
  selectedIds.value
    .map(id => deck.value?.cards.find(c => c.id === id))
    .filter((c): c is NonNullable<typeof c> => !!c)
)
const selectedCount = computed(() => selectedCards.value.length)
const someSuspended = computed(() => selectedCards.value.some(c => c.isSuspended))
const allSuspended = computed(() => selectedCards.value.length > 0 && selectedCards.value.every(c => c.isSuspended))

async function copyDeckId() {
  if (!deck.value) return
  await navigator.clipboard.writeText(deck.value.id)
  idCopied.value = true
  setTimeout(() => idCopied.value = false, 2000)
}

onMounted(async () => {
  try {
    deck.value = await decks.getDeckDetail(route.params.id as string)
  } catch {
    router.replace('/decks')
  } finally {
    loading.value = false
  }
})

async function refresh() {
  deck.value = await decks.getDeckDetail(route.params.id as string)
}

function openEdit() {
  if (!deck.value) return
  editName.value = deck.value.name
  editDescription.value = deck.value.description || ''
  editOpen.value = true
}

async function saveEdit() {
  if (!deck.value || !editName.value.trim()) return
  await decks.updateDeck(deck.value.id, editName.value.trim(), editDescription.value.trim() || undefined)
  editOpen.value = false
  await refresh()
}

function openDelete() {
  deleteCards.value = false
  deleteOpen.value = true
}

async function handleDelete() {
  if (!deck.value) return
  deleteError.value = ''
  try {
    await decks.deleteDeck(deck.value.id, deleteCards.value)
    router.replace('/decks')
  } catch {
    deleteError.value = 'Failed to delete deck. Please try again.'
  }
}

async function toggleSuspended() {
  if (!deck.value) return
  await decks.setSuspended(deck.value.id, !deck.value.isSuspended)
  deck.value = await decks.getDeckDetail(deck.value.id)
}

// --- Bulk actions ---
async function bulkSuspend(target: boolean) {
  bulkBusy.value = true
  bulkError.value = ''
  try {
    const ids = selectedCards.value
      .filter(c => c.isSuspended !== target)
      .map(c => c.id)
    if (ids.length > 0) await cardsStore.setSuspendedBulk(ids, target)
    await refresh()
  } catch {
    bulkError.value = `Failed to ${target ? 'suspend' : 'unsuspend'} cards.`
  } finally {
    bulkBusy.value = false
  }
}

async function bulkRemoveFromThisDeck() {
  if (!deck.value) return
  bulkBusy.value = true
  bulkError.value = ''
  try {
    await decks.removeCards(deck.value.id, selectedIds.value)
    selectedIds.value = []
    await refresh()
  } catch {
    bulkError.value = 'Failed to remove cards from this deck.'
    await refresh()
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
    await refresh()
  } catch {
    bulkError.value = 'Failed to delete cards. The list has been refreshed.'
    selectedIds.value = []
    await refresh()
  } finally {
    bulkBusy.value = false
  }
}

async function onAddedToOtherDeck() {
  // Gate the bulk bar while the refetch is in flight so a second action
  // can't dispatch against a stale selection.
  bulkBusy.value = true
  try {
    await refresh()
  } finally {
    selectedIds.value = []
    bulkBusy.value = false
  }
}

const stateCounts = computed(() => {
  const counts: Record<string, number> = {}
  for (const c of deck.value?.cards ?? []) {
    counts[c.state] = (counts[c.state] || 0) + 1
  }
  return counts
})
</script>

<template>
  <div v-if="loading" class="loading-state">Loading…</div>

  <div v-else-if="deck" class="deck-page">
    <RouterLink to="/decks" class="fa-back">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>
      Decks
    </RouterLink>

    <!-- Header -->
    <header class="deck-header">
      <div class="deck-header-text">
        <h1 class="page-title">{{ deck.name }}</h1>
        <p v-if="deck.description" class="deck-description">{{ deck.description }}</p>
      </div>
      <div class="deck-actions">
        <button
          v-if="deck.dueCount > 0 && !deck.isSuspended"
          class="fa-btn fa-btn-primary"
          @click="router.push(`/review?deckId=${deck.id}`)"
        >
          Study this deck
        </button>
        <button
          v-if="deck.cardCount > 0 && !deck.isSuspended"
          class="fa-btn"
          data-testid="custom-study-button"
          @click="router.push(`/review?deckId=${deck.id}&mode=cram`)"
        >
          Custom study
        </button>
        <button class="fa-btn" @click="router.push(`/decks/${deck.id}/snapshots`)">
          <History class="h-3.5 w-3.5" />Snapshots
        </button>
        <button class="fa-btn" @click="toggleSuspended">
          {{ deck.isSuspended ? 'Unsuspend' : 'Suspend' }}
        </button>
        <button class="fa-btn" @click="copyDeckId">
          {{ idCopied ? 'Copied!' : 'Copy ID' }}
        </button>
        <button class="fa-btn" @click="openEdit">Edit</button>
        <button class="fa-btn delete-btn" @click="openDelete">Delete</button>
      </div>
    </header>

    <!-- Inactive banner -->
    <div v-if="deck.isSuspended" class="suspended-banner">
      This deck is suspended. Cards are excluded from study.
    </div>

    <!-- Stat line — quiet whitespace, no boxed tile -->
    <div class="stat-line">
      <span class="stat">
        <span class="stat-num fa-num">{{ deck.cardCount }}</span>
        <span class="stat-label">cards</span>
      </span>
      <span class="stat-sep">·</span>
      <span class="stat">
        <span class="stat-num fa-num" :class="{ 'is-due': deck.dueCount > 0 }">{{ deck.dueCount }}</span>
        <span class="stat-label">due</span>
      </span>
      <span class="stat-sep">·</span>
      <span class="stat-states">
        <span v-for="state in ['new', 'learning', 'review', 'relearning']" :key="state">
          <span class="fa-num">{{ stateCounts[state] || 0 }}</span> {{ state }}
        </span>
      </span>
    </div>

    <div class="fa-rule" />

    <!-- Cards section -->
    <div class="cards-section">
      <div class="fa-cap section-label">Cards in this deck</div>

      <BulkActionBar
        v-if="selectedCount > 0"
        :count="selectedCount"
        :some-suspended="someSuspended"
        :all-suspended="allSuspended"
        :can-remove-from-deck="true"
        :busy="bulkBusy"
        @add-to-deck="addToDeckOpen = true"
        @remove-from-deck="bulkRemoveFromThisDeck"
        @suspend="bulkSuspend(true)"
        @unsuspend="bulkSuspend(false)"
        @delete="bulkDeleteOpen = true"
        @clear="selectedIds = []"
      />

      <div v-if="bulkError" class="bulk-error">{{ bulkError }}</div>

      <CardTable
        v-if="deck.cards.length > 0"
        v-model:selectedIds="selectedIds"
        :cards="deck.cards"
        :deck-context="{ id: deck.id, name: deck.name }"
        selectable
      >
        <template #empty>No cards in this deck yet.</template>
      </CardTable>

      <div v-else class="cards-empty">
        No cards in this deck yet. Add cards from the Cards view.
      </div>
    </div>

    <!-- Edit deck dialog -->
    <Dialog v-model:open="editOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit deck</DialogTitle>
        </DialogHeader>
        <div class="space-y-3">
          <Input v-model="editName" placeholder="Deck name" @keydown.enter="saveEdit" />
          <Input v-model="editDescription" placeholder="Description (optional)" @keydown.enter="saveEdit" />
        </div>
        <DialogFooter>
          <Button @click="saveEdit">Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Delete deck dialog -->
    <Dialog v-model:open="deleteOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete deck</DialogTitle>
          <DialogDescription>
            Are you sure you want to delete "{{ deck.name }}"? All snapshots for this deck will also be deleted.
          </DialogDescription>
        </DialogHeader>
        <div class="flex items-center gap-2">
          <Checkbox id="delete-cards" :checked="deleteCards" @update:checked="deleteCards = $event" />
          <label for="delete-cards" class="text-sm cursor-pointer select-none">
            Also delete all {{ deck.cardCount }} cards in this deck
          </label>
        </div>
        <div v-if="deleteError" class="bulk-error">{{ deleteError }}</div>
        <DialogFooter class="gap-2">
          <Button variant="outline" size="sm" @click="deleteOpen = false">Cancel</Button>
          <Button variant="destructive" size="sm" @click="handleDelete">Delete</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Add selection to another deck -->
    <AddToDeckDialog
      v-model:open="addToDeckOpen"
      :card-ids="selectedIds"
      @added="onAddedToOtherDeck"
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
.deck-page {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.loading-state {
  padding: 48px 0;
  text-align: center;
  font-size: 14px;
  color: var(--ink-2);
}

/* Header */
.deck-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}
.deck-header-text { min-width: 0; }
.deck-page .page-title { font-size: 26px; }
.deck-description {
  margin: 6px 0 0;
  font-size: 14px;
  color: var(--ink-1);
}
.deck-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  justify-content: flex-end;
}
.delete-btn { color: var(--c-again); }
.delete-btn:hover { color: var(--c-again); border-color: var(--c-again); }

/* Suspended banner — quiet surface, hairline, no glow */
.suspended-banner {
  border: 1px solid var(--rule-1);
  background: var(--paper-2);
  border-radius: 9px;
  padding: 10px 14px;
  font-size: 13.5px;
  color: var(--ink-1);
}

/* Stat line — open row of numbers + labels, accent only on "due" */
.stat-line {
  display: flex;
  align-items: baseline;
  flex-wrap: wrap;
  gap: 10px;
  font-size: 14px;
  color: var(--ink-2);
}
.stat { display: inline-flex; align-items: baseline; gap: 5px; }
.stat-num { font-size: 17px; color: var(--ink-0); font-weight: 600; }
.stat-num.is-due { color: var(--accent-text); }
.stat-label { color: var(--ink-2); }
.stat-sep { color: var(--ink-3); }
.stat-states {
  display: inline-flex;
  align-items: baseline;
  gap: 12px;
  color: var(--ink-2);
}
.stat-states .fa-num { color: var(--ink-1); font-weight: 600; }

/* Cards section */
.cards-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.section-label { margin-bottom: 2px; }

.bulk-error { font-size: 13px; color: var(--c-again); }

.cards-empty {
  padding: 48px 0;
  text-align: center;
  font-size: 13.5px;
  color: var(--ink-2);
}
</style>
