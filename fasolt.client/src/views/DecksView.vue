<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useDecksStore } from '@/stores/decks'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from '@/components/ui/dialog'

const router = useRouter()
const decks = useDecksStore()
const newName = ref('')
const newDescription = ref('')
const dialogOpen = ref(false)
const createError = ref('')

onMounted(() => decks.fetchDecks())

const sortedDecks = computed(() =>
  [...decks.decks].sort((a, b) => {
    if (a.isActive !== b.isActive) return a.isActive ? -1 : 1
    return 0
  })
)

async function createDeck() {
  const name = newName.value.trim()
  if (!name) return
  createError.value = ''
  try {
    await decks.createDeck(name, newDescription.value.trim() || undefined)
    newName.value = ''
    newDescription.value = ''
    dialogOpen.value = false
  } catch {
    createError.value = 'Failed to create deck. Please try again.'
  }
}
</script>

<template>
  <div class="space-y-5">
    <div class="flex items-center justify-between">
      <h1 class="text-lg font-bold tracking-tight">Decks</h1>
      <Dialog v-model:open="dialogOpen">
        <DialogTrigger as-child>
          <Button size="sm" class="text-xs">New deck</Button>
        </DialogTrigger>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create deck</DialogTitle>
          </DialogHeader>
          <div class="space-y-3">
            <Input v-model="newName" placeholder="Deck name" @keydown.enter="createDeck" />
            <Input v-model="newDescription" placeholder="Description (optional)" @keydown.enter="createDeck" />
          </div>
          <div v-if="createError" class="text-xs text-destructive">{{ createError }}</div>
          <DialogFooter>
            <Button @click="createDeck">Create</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>

    <div v-if="decks.loading" class="py-12 text-center text-xs text-muted-foreground">Loading...</div>

    <div v-else class="grid gap-2.5 sm:grid-cols-2">
      <Card
        v-for="deck in sortedDecks"
        :key="deck.id"
        class="cursor-pointer border-border/60 hover:border-accent/30 transition-colors"
        :class="{ 'opacity-50': !deck.isActive }"
        @click="router.push(`/decks/${deck.id}`)"
      >
        <CardContent class="p-4">
          <div class="flex items-start justify-between">
            <div>
              <div class="text-sm font-semibold text-foreground">{{ deck.name }}</div>
              <div v-if="deck.description" class="mt-0.5 text-[11px] text-muted-foreground">{{ deck.description }}</div>
            </div>
            <div class="flex items-center gap-1 ml-3">
              <span v-if="deck.dueCount > 0" class="text-xs text-warning whitespace-nowrap">
                {{ deck.dueCount }} due
              </span>
              <Badge v-if="!deck.isActive" variant="outline" class="text-[10px] ml-2">Inactive</Badge>
            </div>
          </div>
          <div class="mt-2 pt-2 border-t border-border/40 text-[11px] text-muted-foreground">
            {{ deck.cardCount }} cards
          </div>
        </CardContent>
      </Card>
    </div>

    <div v-if="!decks.loading && decks.decks.length === 0" class="py-12 text-center text-xs text-muted-foreground">
      No decks yet. Create one to organize your cards.
    </div>
  </div>
</template>
