<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { useDecksStore } from '@/stores/decks'
import type { DeckDetail } from '@/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog'
import { Checkbox } from '@/components/ui/checkbox'
import { History } from 'lucide-vue-next'
import CardDeleteDialog from '@/components/CardDeleteDialog.vue'
import CardTable from '@/components/CardTable.vue'

const route = useRoute()
const router = useRouter()
const decks = useDecksStore()

const deck = ref<DeckDetail | null>(null)
const loading = ref(true)

const editOpen = ref(false)
const editName = ref('')
const editDescription = ref('')

const deleteOpen = ref(false)
const deleteCards = ref(false)
const deleteError = ref('')

const deleteCardTarget = ref<any>(null)
const deleteCardOpen = ref(false)

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

async function removeCard(cardId: string) {
  if (!deck.value) return
  await decks.removeCard(deck.value.id, cardId)
  await refresh()
}

async function toggleSuspended() {
  if (!deck.value) return
  await decks.setSuspended(deck.value.id, !deck.value.isSuspended)
  deck.value = await decks.getDeckDetail(deck.value.id)
}

async function onCardDeleted() {
  deleteCardTarget.value = null
  deleteCardOpen.value = false
  await refresh()
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
  <div v-if="loading" class="py-12 text-center text-xs text-muted-foreground">Loading...</div>

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
          class="text-xs"
          @click="router.push(`/review?deckId=${deck.id}`)"
        >
          Study this deck
        </Button>
        <Button variant="outline" size="sm" class="text-xs" @click="router.push(`/decks/${deck.id}/snapshots`)">
          <History class="h-3.5 w-3.5 mr-1" />Snapshots
        </Button>
        <Button variant="outline" size="sm" class="text-xs" @click="toggleSuspended">
          {{ deck.isSuspended ? 'Unsuspend' : 'Suspend' }}
        </Button>
        <Button variant="outline" size="sm" class="h-7 text-[10px]" @click="openEdit">Edit</Button>
        <Button variant="outline" size="sm" class="h-7 text-[10px] text-destructive hover:text-destructive" @click="openDelete">Delete</Button>
      </div>
    </div>

    <!-- Inactive banner -->
    <div v-if="deck.isSuspended" class="rounded-md border border-muted bg-muted/50 px-4 py-2 text-xs text-muted-foreground">
      This deck is suspended. Cards are excluded from study.
    </div>

    <!-- Stat bar -->
    <div class="bg-secondary rounded-lg px-4 py-3 flex items-center gap-5">
      <div>
        <span class="text-lg font-bold">{{ deck.cardCount }}</span>
        <span class="text-xs text-muted-foreground ml-1.5">cards</span>
      </div>
      <div class="w-px h-5 bg-border" />
      <div>
        <span class="text-lg font-bold text-warning">{{ deck.dueCount }}</span>
        <span class="text-xs text-muted-foreground ml-1.5">due</span>
      </div>
      <div class="w-px h-5 bg-border" />
      <div class="flex items-center gap-3 text-xs text-muted-foreground">
        <span v-for="state in ['new', 'learning', 'review', 'relearning']" :key="state">
          {{ stateCounts[state] || 0 }} {{ state }}
        </span>
      </div>
    </div>

    <!-- Cards section -->
    <div>
      <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Cards in this deck</div>

      <CardTable
        v-if="deck.cards.length > 0"
        :cards="deck.cards"
        @delete="(card) => { deleteCardTarget = card; deleteCardOpen = true }"
        @remove="(card) => removeCard(card.id)"
      >
        <template #empty>No cards in this deck yet.</template>
      </CardTable>

      <div v-else class="py-12 text-center text-xs text-muted-foreground">
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
          <label for="delete-cards" class="text-xs cursor-pointer select-none">
            Also delete all {{ deck.cardCount }} cards in this deck
          </label>
        </div>
        <div v-if="deleteError" class="text-xs text-destructive">{{ deleteError }}</div>
        <DialogFooter class="gap-2">
          <Button variant="outline" size="sm" @click="deleteOpen = false">Cancel</Button>
          <Button variant="destructive" size="sm" @click="handleDelete">Delete</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Delete card dialog -->
    <CardDeleteDialog
      v-model:open="deleteCardOpen"
      :card="deleteCardTarget"
      @deleted="onCardDeleted"
    />
  </div>
</template>
