<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import { useCardsStore } from '@/stores/cards'
import { useDecksStore } from '@/stores/decks'
import type { Card } from '@/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import CardCreateDialog from '@/components/CardCreateDialog.vue'
import CardDeleteDialog from '@/components/CardDeleteDialog.vue'
import CardTable from '@/components/CardTable.vue'
import { stripMarkdown } from '@/lib/utils'

const route = useRoute()
const cardsStore = useCardsStore()
const decks = useDecksStore()

const deleteTarget = ref<Card | null>(null)
const deleteOpen = ref(false)
const createOpen = ref(false)
const addToDeckCard = ref<Card | null>(null)
const addToDeckId = ref('')
const addToDeckError = ref('')

const filterValue = ref('')
const sourceFilter = ref('')
const stateFilter = ref('')
const activeOnly = ref(true)
const deckFilter = ref('')

onMounted(async () => {
  await Promise.all([cardsStore.fetchCards(), decks.fetchDecks()])
  const sf = route.query.sourceFile
  if (sf && typeof sf === 'string') {
    sourceFilter.value = sf
  }
})

function onCardDeleted() {
  deleteTarget.value = null
  deleteOpen.value = false
}

async function addCardToDeck() {
  if (!addToDeckCard.value || !addToDeckId.value) return
  addToDeckError.value = ''
  try {
    await decks.addCards(addToDeckId.value, [addToDeckCard.value.id])
    await cardsStore.fetchCards()
    addToDeckCard.value = null
    addToDeckId.value = ''
  } catch {
    addToDeckError.value = 'Failed to add card to deck. Please try again.'
  }
}

const filteredCards = computed(() => {
  let result = cardsStore.cards

  // Active filter: hide cards belonging only to inactive decks
  if (activeOnly.value) {
    result = result.filter(card => {
      if (card.decks.length === 0) return true
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
</script>

<template>
  <div class="space-y-4">
    <!-- Page header -->
    <div class="flex items-center justify-between">
      <h1 class="text-lg font-bold tracking-tight">Cards</h1>
      <Button size="sm" class="h-8 text-xs" @click="createOpen = true">New card</Button>
    </div>

    <!-- Toolbar -->
    <div class="flex items-center gap-2">
      <Input
        v-model="filterValue"
        placeholder="Filter cards..."
        class="h-8 max-w-[200px] text-xs"
      />
      <Input
        v-model="sourceFilter"
        placeholder="Filter by source..."
        class="h-8 max-w-[200px] text-xs"
      />
      <select
        v-model="stateFilter"
        class="h-8 rounded border border-border bg-transparent px-2 text-xs text-foreground"
      >
        <option value="">All states</option>
        <option value="new">new</option>
        <option value="learning">learning</option>
        <option value="review">review</option>
        <option value="relearning">relearning</option>
      </select>
      <select
        v-model="deckFilter"
        class="h-8 rounded border border-border bg-transparent px-2 text-xs text-foreground"
      >
        <option value="">All decks</option>
        <option value="none">None (no deck)</option>
        <option v-for="d in decks.decks" :key="d.id" :value="d.id">{{ d.name }}</option>
      </select>
      <label class="flex items-center gap-1.5 text-xs cursor-pointer">
        <input type="checkbox" v-model="activeOnly" class="rounded border-border" />
        Active
      </label>
    </div>

    <!-- Table -->
    <CardTable
      :cards="filteredCards"
      show-decks
      show-pagination
      @delete="(card) => { deleteTarget = card; deleteOpen = true }"
      @add-to-deck="(card) => addToDeckCard = card"
    >
      <template #empty>No cards yet. Create one or use the MCP agent to generate cards from your notes.</template>
    </CardTable>

    <!-- Dialogs -->
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

    <Dialog :open="!!addToDeckCard" @update:open="addToDeckCard = null">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add to deck</DialogTitle>
          <DialogDescription>
            Add "{{ addToDeckCard?.front ? stripMarkdown(addToDeckCard.front).slice(0, 40) : '' }}" to a deck.
          </DialogDescription>
        </DialogHeader>
        <select
          v-model="addToDeckId"
          class="w-full h-8 rounded border border-border bg-transparent px-2 text-xs text-foreground"
        >
          <option value="">Select a deck</option>
          <option v-for="d in decks.decks" :key="d.id" :value="d.id">{{ d.name }}</option>
        </select>
        <div v-if="addToDeckError" class="text-xs text-destructive">{{ addToDeckError }}</div>
        <DialogFooter>
          <Button variant="outline" @click="addToDeckCard = null">Cancel</Button>
          <Button :disabled="!addToDeckId" @click="addCardToDeck">Add</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
