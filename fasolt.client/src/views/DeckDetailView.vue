<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { useDecksStore } from '@/stores/decks'
import { useCardsStore } from '@/stores/cards'
import { useLibraryStore } from '@/stores/library'
import type { Deck, DeckDetail } from '@/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog'
import { Checkbox } from '@/components/ui/checkbox'
import { History, Link2 } from 'lucide-vue-next'
import CardTable from '@/components/CardTable.vue'
import BulkActionBar from '@/components/BulkActionBar.vue'
import AddToDeckDialog from '@/components/AddToDeckDialog.vue'
import DeckShareSection from '@/components/DeckShareSection.vue'

const route = useRoute()
const router = useRouter()
const decks = useDecksStore()
const cardsStore = useCardsStore()
const library = useLibraryStore()

const deck = ref<DeckDetail | null>(null)
const loading = ref(true)

/** A linked deck belongs to its author: read-only content, but our own SRS state. */
const isLinked = computed(() => deck.value?.isLinked === true)

const convertOpen = ref(false)
const unlinkOpen = ref(false)
const linkBusy = ref(false)
const linkError = ref('')

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

async function load(id: string) {
  loading.value = true
  selectedIds.value = []
  linkError.value = ''
  try {
    deck.value = await decks.getDeckDetail(id)
  } catch {
    // Gone (deleted, or a linked deck the author unpublished) — back to the list.
    router.replace('/decks')
  } finally {
    loading.value = false
  }
}

onMounted(() => load(route.params.id as string))

// Converting a link to a copy lands on a different deck id on the same route,
// which reuses this component instead of remounting it.
watch(() => route.params.id, (id, previous) => {
  if (!id || id === previous) return
  load(id as string)
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
  // On a linked deck this pauses the subscription, not the author's deck.
  await decks.setSuspended(deck.value.id, !deck.value.isSuspended)
  deck.value = await decks.getDeckDetail(deck.value.id)
}

// --- Linked decks ---
async function convertToCopy() {
  if (!deck.value || linkBusy.value) return
  linkBusy.value = true
  linkError.value = ''
  try {
    const copy = await decks.convertToCopy(deck.value.id)
    convertOpen.value = false
    router.replace(`/decks/${copy.id}`)
  } catch {
    linkError.value = 'Could not convert this deck to a copy. Please try again.'
  } finally {
    linkBusy.value = false
  }
}

async function unlink() {
  if (!deck.value || linkBusy.value) return
  linkBusy.value = true
  linkError.value = ''
  try {
    await library.unsubscribeDeck(deck.value.id)
    decks.decks = decks.decks.filter(d => d.id !== deck.value!.id)
    unlinkOpen.value = false
    router.replace('/decks')
  } catch {
    linkError.value = 'Could not unlink this deck. Please try again.'
  } finally {
    linkBusy.value = false
  }
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

function onVisibilityUpdated(updated: Deck) {
  if (!deck.value) return
  // The visibility endpoint returns a DeckDto (no cards) — merge so the card
  // list and selection survive a publish/unpublish.
  deck.value = { ...deck.value, ...updated }
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
        <div class="deck-title-line">
          <h1 class="page-title">{{ deck.name }}</h1>
          <RouterLink v-if="isLinked" :to="`/library/${deck.id}`" class="linked-chip">
            <Link2 class="h-3 w-3" />
            Linked<template v-if="deck.authorHandle"> · @{{ deck.authorHandle }}</template>
          </RouterLink>
          <span v-else-if="deck.visibility !== 'private'" class="visibility-chip">
            {{ deck.visibility === 'public' ? 'Public' : 'Unlisted' }}
          </span>
        </div>
        <p v-if="deck.description" class="deck-description">{{ deck.description }}</p>
        <p v-if="isLinked" class="deck-attribution">
          This deck follows the author's updates. Its cards are read-only — your study progress is your own.
        </p>
        <p v-else-if="deck.copiedFromDeckPublicId" class="deck-attribution">
          Copied from
          <RouterLink :to="`/library/${deck.copiedFromDeckPublicId}`" class="fa-link">
            <template v-if="deck.copiedFromHandle">@{{ deck.copiedFromHandle }}</template>
            <template v-else>the library</template>
          </RouterLink>
        </p>
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
        <button v-if="!isLinked" class="fa-btn" @click="router.push(`/decks/${deck.id}/snapshots`)">
          <History class="h-3.5 w-3.5" />Snapshots
        </button>
        <button class="fa-btn" @click="toggleSuspended">
          <template v-if="isLinked">{{ deck.isSuspended ? 'Resume' : 'Pause' }}</template>
          <template v-else>{{ deck.isSuspended ? 'Unsuspend' : 'Suspend' }}</template>
        </button>
        <!-- The deck id is the author's — nothing the subscriber can address by it. -->
        <button v-if="!isLinked" class="fa-btn" @click="copyDeckId">
          {{ idCopied ? 'Copied!' : 'Copy ID' }}
        </button>
        <template v-if="isLinked">
          <button class="fa-btn" @click="convertOpen = true">Convert to copy</button>
          <button class="fa-btn delete-btn" @click="unlinkOpen = true">Unlink</button>
        </template>
        <template v-else>
          <button class="fa-btn" @click="openEdit">Edit</button>
          <button class="fa-btn delete-btn" @click="openDelete">Delete</button>
        </template>
      </div>
    </header>

    <div v-if="linkError" class="bulk-error">{{ linkError }}</div>

    <!-- Inactive banner -->
    <div v-if="deck.isSuspended" class="suspended-banner">
      <template v-if="isLinked">You've paused this linked deck. Its cards are excluded from study.</template>
      <template v-else>This deck is suspended. Cards are excluded from study.</template>
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

    <!-- Sharing — only the author can change how their deck is shared. -->
    <template v-if="!isLinked">
      <DeckShareSection
        :deck-id="deck.id"
        :visibility="deck.visibility"
        :copy-count="deck.copyCount"
        @updated="onVisibilityUpdated"
      />

      <div class="fa-rule" />
    </template>

    <!-- Cards section -->
    <div class="cards-section">
      <div class="fa-cap section-label">Cards in this deck</div>

      <BulkActionBar
        v-if="!isLinked && selectedCount > 0"
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
        :selectable="!isLinked"
        :read-only="isLinked"
      >
        <template #empty>No cards in this deck yet.</template>
      </CardTable>

      <div v-else class="cards-empty">
        <template v-if="isLinked">This deck has no cards yet.</template>
        <template v-else>No cards in this deck yet. Add cards from the Cards view.</template>
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

    <!-- Convert a linked deck into an owned copy -->
    <Dialog v-model:open="convertOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Convert to a copy?</DialogTitle>
          <DialogDescription>
            "{{ deck.name }}" becomes your own deck: {{ deck.cardCount }} card{{ deck.cardCount === 1 ? '' : 's' }}
            are copied into your account and your review progress comes with them. The copy stops
            following
            <template v-if="deck.authorHandle">@{{ deck.authorHandle }}</template>
            <template v-else>the author</template>, so their future changes won't reach it — but you
            can edit the cards yourself.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter class="gap-2">
          <Button variant="outline" size="sm" :disabled="linkBusy" @click="convertOpen = false">Cancel</Button>
          <Button size="sm" :disabled="linkBusy" @click="convertToCopy">
            {{ linkBusy ? 'Converting…' : 'Convert to copy' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Unlink a linked deck -->
    <Dialog v-model:open="unlinkOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Unlink this deck?</DialogTitle>
          <DialogDescription>
            "{{ deck.name }}" is removed from your decks along with your review progress for its
            cards. The author's deck is not affected, and you can link it again from the library.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter class="gap-2">
          <Button variant="outline" size="sm" :disabled="linkBusy" @click="unlinkOpen = false">Cancel</Button>
          <Button variant="destructive" size="sm" :disabled="linkBusy" @click="unlink">
            {{ linkBusy ? 'Unlinking…' : 'Unlink' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

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
.deck-title-line {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.visibility-chip {
  font-size: 11px;
  font-weight: 500;
  color: var(--accent-text);
  padding: 1px 8px;
  border: 1px solid var(--accent);
  border-radius: 999px;
}
.linked-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  font-weight: 500;
  color: var(--accent-text);
  padding: 1px 8px;
  border: 1px solid var(--accent);
  border-radius: 999px;
  text-decoration: none;
  white-space: nowrap;
}
.linked-chip:hover { background: var(--accent-soft); }
.deck-description {
  margin: 6px 0 0;
  font-size: 14px;
  color: var(--ink-1);
}
.deck-attribution {
  margin: 6px 0 0;
  font-size: 12.5px;
  color: var(--ink-2);
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
