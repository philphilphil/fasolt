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
  <div v-if="loading" class="py-12 text-center text-sm text-muted-foreground">Loading...</div>

  <div v-else-if="deck" class="space-y-6">
    <!-- Breadcrumb -->
    <div class="text-[11px] text-muted-foreground">
      <RouterLink to="/decks" class="hover:text-foreground transition-colors">Decks</RouterLink>
      <span class="mx-1.5">/</span>
      <span class="text-foreground">{{ deck.name }}</span>
    </div>

    <!-- Header -->
    <div class="flex items-start justify-between">
      <div>
        <h1 class="text-xl font-bold tracking-tight">{{ deck.name }}</h1>
        <p v-if="deck.description" class="text-sm text-muted-foreground mt-1">{{ deck.description }}</p>
      </div>
      <div class="flex items-center gap-2">
        <Button
          v-if="deck.dueCount > 0 && !deck.isSuspended"
          size="sm"
          class="text-sm"
          @click="router.push(`/review?deckId=${deck.id}`)"
        >
          Study this deck
        </Button>
        <Button
          v-if="deck.cardCount > 0 && !deck.isSuspended"
          variant="outline"
          size="sm"
          class="text-sm"
          data-testid="custom-study-button"
          @click="router.push(`/review?deckId=${deck.id}&mode=cram`)"
        >
          Custom study
        </Button>
        <Button variant="outline" size="sm" class="text-sm" @click="router.push(`/decks/${deck.id}/snapshots`)">
          <History class="h-3.5 w-3.5 mr-1" />Snapshots
        </Button>
        <Button variant="outline" size="sm" class="text-sm" @click="toggleSuspended">
          {{ deck.isSuspended ? 'Unsuspend' : 'Suspend' }}
        </Button>
        <Button variant="outline" size="sm" class="h-7 text-xs" @click="copyDeckId">
          {{ idCopied ? 'Copied!' : 'Copy ID' }}
        </Button>
        <Button variant="outline" size="sm" class="h-7 text-xs" @click="openEdit">Edit</Button>
        <Button variant="outline" size="sm" class="h-7 text-xs text-destructive hover:text-destructive" @click="openDelete">Delete</Button>
      </div>
    </div>

    <!-- Inactive banner -->
    <div v-if="deck.isSuspended" class="rounded-md border border-muted bg-muted/50 px-4 py-2 text-sm text-muted-foreground">
      This deck is suspended. Cards are excluded from study.
    </div>

    <!-- Stat bar -->
    <div class="bg-secondary rounded-lg px-4 py-3 flex items-center gap-5">
      <div>
        <span class="text-lg font-bold">{{ deck.cardCount }}</span>
        <span class="text-sm text-muted-foreground ml-1.5">cards</span>
      </div>
      <div class="w-px h-5 bg-border" />
      <div>
        <span class="text-lg font-bold text-warning">{{ deck.dueCount }}</span>
        <span class="text-sm text-muted-foreground ml-1.5">due</span>
      </div>
      <div class="w-px h-5 bg-border" />
      <div class="flex items-center gap-3 text-sm text-muted-foreground">
        <span v-for="state in ['new', 'learning', 'review', 'relearning']" :key="state">
          {{ stateCounts[state] || 0 }} {{ state }}
        </span>
      </div>
    </div>

    <!-- Cards section -->
    <div class="space-y-3">
      <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5">Cards in this deck</div>

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

      <div v-if="bulkError" class="text-sm text-destructive">{{ bulkError }}</div>

      <CardTable
        v-if="deck.cards.length > 0"
        v-model:selectedIds="selectedIds"
        :cards="deck.cards"
        :deck-context="{ id: deck.id, name: deck.name }"
        selectable
      >
        <template #empty>No cards in this deck yet.</template>
      </CardTable>

      <div v-else class="py-12 text-center text-sm text-muted-foreground">
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
        <div v-if="deleteError" class="text-sm text-destructive">{{ deleteError }}</div>
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
