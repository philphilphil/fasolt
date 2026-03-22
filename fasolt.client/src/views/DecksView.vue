<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useDecksStore } from '@/stores/decks'
import { Button } from '@/components/ui/button'
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

onMounted(() => decks.fetchDecks())

async function createDeck() {
  const name = newName.value.trim()
  if (!name) return
  await decks.createDeck(name, newDescription.value.trim() || undefined)
  newName.value = ''
  newDescription.value = ''
  dialogOpen.value = false
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <h1 class="text-lg font-semibold tracking-tight">Decks</h1>
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
          <DialogFooter>
            <Button @click="createDeck">Create</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>

    <div v-if="decks.loading" class="py-12 text-center text-sm text-muted-foreground">Loading...</div>

    <div v-else class="grid gap-2.5 sm:grid-cols-2">
      <Card
        v-for="deck in decks.decks"
        :key="deck.id"
        class="cursor-pointer border-border"
        @click="router.push(`/decks/${deck.id}`)"
      >
        <CardContent class="flex items-center justify-between p-4">
          <div>
            <div class="text-sm font-medium text-foreground">{{ deck.name }}</div>
            <div v-if="deck.description" class="mt-0.5 text-xs text-muted-foreground">{{ deck.description }}</div>
            <div class="mt-0.5 text-xs text-muted-foreground">
              {{ deck.cardCount }} cards
            </div>
          </div>
          <div class="flex items-center gap-3">
            <span v-if="deck.dueCount > 0" class="font-mono text-xs text-warning">
              {{ deck.dueCount }} due
            </span>
          </div>
        </CardContent>
      </Card>
    </div>

    <div v-if="!decks.loading && decks.decks.length === 0" class="py-12 text-center text-sm text-muted-foreground">
      No decks yet. Create one to organize your cards.
    </div>
  </div>
</template>
