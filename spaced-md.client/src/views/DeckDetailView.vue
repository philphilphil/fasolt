<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useDecksStore } from '@/stores/decks'
import { useFilesStore } from '@/stores/files'
import type { DeckDetail } from '@/types'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

const route = useRoute()
const router = useRouter()
const decks = useDecksStore()
const filesStore = useFilesStore()

const deck = ref<DeckDetail | null>(null)
const loading = ref(true)

const editOpen = ref(false)
const editName = ref('')
const editDescription = ref('')

onMounted(async () => {
  try {
    deck.value = await decks.getDeckDetail(route.params.id as string)
    await filesStore.fetchFiles()
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

async function handleDelete() {
  if (!deck.value) return
  await decks.deleteDeck(deck.value.id)
  router.replace('/decks')
}

async function removeCard(cardId: string) {
  if (!deck.value) return
  await decks.removeCard(deck.value.id, cardId)
  await refresh()
}

async function addFromFile(fileId: string) {
  if (!deck.value) return
  await decks.addFileCards(deck.value.id, fileId)
  await refresh()
}
</script>

<template>
  <div v-if="loading" class="py-12 text-center text-sm text-muted-foreground">Loading...</div>

  <div v-else-if="deck" class="space-y-4">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <Button variant="ghost" size="sm" class="h-7 text-xs" @click="router.push('/decks')">
          &larr; Decks
        </Button>
        <span class="text-sm font-medium">{{ deck.name }}</span>
      </div>
      <div class="flex items-center gap-2">
        <Button
          v-if="deck.dueCount > 0"
          size="sm"
          class="text-xs"
          @click="router.push(`/review?deckId=${deck.id}`)"
        >
          Study this deck
        </Button>

        <DropdownMenu>
          <DropdownMenuTrigger as-child>
            <Button variant="outline" size="sm" class="h-7 text-xs">Add from file</Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem
              v-for="file in filesStore.files"
              :key="file.id"
              @click="addFromFile(file.id)"
            >
              {{ file.fileName }}
            </DropdownMenuItem>
            <DropdownMenuItem v-if="filesStore.files.length === 0" disabled>
              No files uploaded
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>

        <Button variant="outline" size="sm" class="h-7 text-xs" @click="openEdit">Edit</Button>
        <Button variant="outline" size="sm" class="h-7 text-xs text-destructive hover:text-destructive" @click="handleDelete">Delete</Button>
      </div>
    </div>

    <!-- Description & stats -->
    <div v-if="deck.description" class="text-xs text-muted-foreground">{{ deck.description }}</div>
    <div class="flex gap-4 text-xs text-muted-foreground">
      <span>{{ deck.cardCount }} cards</span>
      <span v-if="deck.dueCount > 0" class="text-warning">{{ deck.dueCount }} due</span>
    </div>

    <!-- Card list -->
    <Table v-if="deck.cards.length > 0">
      <TableHeader>
        <TableRow class="text-[10px] uppercase tracking-wider text-muted-foreground hover:bg-transparent">
          <TableHead class="h-8">Front</TableHead>
          <TableHead class="h-8">Type</TableHead>
          <TableHead class="h-8">State</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Due</TableHead>
          <TableHead class="h-8 w-16" />
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow v-for="card in deck.cards" :key="card.id" class="text-xs">
          <TableCell class="font-medium text-foreground max-w-[300px] truncate">{{ card.front }}</TableCell>
          <TableCell><Badge variant="outline" class="text-[10px]">{{ card.cardType }}</Badge></TableCell>
          <TableCell class="text-muted-foreground">{{ card.state }}</TableCell>
          <TableCell class="hidden text-muted-foreground sm:table-cell">{{ card.dueAt || '—' }}</TableCell>
          <TableCell>
            <Button
              variant="ghost"
              size="sm"
              class="h-6 text-[10px] text-destructive hover:text-destructive"
              @click="removeCard(card.id)"
            >
              Remove
            </Button>
          </TableCell>
        </TableRow>
      </TableBody>
    </Table>

    <div v-else class="py-12 text-center text-sm text-muted-foreground">
      No cards in this deck yet. Add cards from a file.
    </div>

    <!-- Edit dialog -->
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
  </div>
</template>
