<script setup lang="ts">
import { h, ref, onMounted, computed } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import type { ColumnDef, SortingState } from '@tanstack/vue-table'
import {
  FlexRender,
  getCoreRowModel,
  getSortedRowModel,
  useVueTable,
} from '@tanstack/vue-table'
import { valueUpdater } from '@/lib/utils'
import { useDecksStore } from '@/stores/decks'
import type { DeckDetail, DeckCard } from '@/types'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog'
import { Checkbox } from '@/components/ui/checkbox'

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

function formatDate(iso: string | null): string {
  if (!iso) return '—'
  const d = new Date(iso)
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
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
  await decks.deleteDeck(deck.value.id, deleteCards.value)
  router.replace('/decks')
}

async function removeCard(cardId: string) {
  if (!deck.value) return
  await decks.removeCard(deck.value.id, cardId)
  await refresh()
}

const columns: ColumnDef<DeckCard>[] = [
  {
    accessorKey: 'front',
    header: 'Front',
    cell: ({ row }) => {
      const val = row.getValue('front') as string
      const display = val.length > 80 ? val.slice(0, 80) + '…' : val
      const source = row.original.sourceFile
      return h('div', { class: 'min-w-0' }, [
        h(RouterLink, {
          to: `/cards/${row.original.id}`,
          class: 'hover:text-accent transition-colors',
        }, () => display),
        source
          ? h('div', { class: 'truncate text-[11px] text-muted-foreground mt-0.5' }, source)
          : null,
      ])
    },
  },
  {
    accessorKey: 'state',
    header: 'State',
    meta: { className: 'w-[80px]' },
    cell: ({ row }) => h(Badge, { variant: 'outline', class: 'text-[10px]' }, () => row.getValue('state')),
  },
  {
    accessorKey: 'dueAt',
    header: 'Due',
    meta: { className: 'w-[120px] hidden sm:table-cell' },
    cell: ({ row }) => h('span', { class: 'text-muted-foreground' }, formatDate(row.getValue('dueAt') as string | null)),
  },
  {
    id: 'actions',
    enableSorting: false,
    meta: { className: 'w-[70px]' },
    cell: ({ row }) => h(Button, {
      variant: 'ghost',
      size: 'sm',
      class: 'h-6 text-[10px] text-destructive hover:text-destructive',
      onClick: () => removeCard(row.original.id),
    }, () => 'Remove'),
  },
]

const sorting = ref<SortingState>([])

const cardData = computed(() => deck.value?.cards ?? [])

const stateCounts = computed(() => {
  const counts: Record<string, number> = {}
  for (const c of cardData.value) {
    counts[c.state] = (counts[c.state] || 0) + 1
  }
  return counts
})

const table = useVueTable({
  get data() { return cardData.value },
  columns,
  getCoreRowModel: getCoreRowModel(),
  getSortedRowModel: getSortedRowModel(),
  onSortingChange: updaterOrValue => valueUpdater(updaterOrValue, sorting),
  state: {
    get sorting() { return sorting.value },
  },
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
          v-if="deck.dueCount > 0"
          size="sm"
          class="text-xs glow-accent"
          @click="router.push(`/review?deckId=${deck.id}`)"
        >
          Study this deck
        </Button>
        <Button variant="outline" size="sm" class="h-7 text-[10px]" @click="openEdit">Edit</Button>
        <Button variant="outline" size="sm" class="h-7 text-[10px] text-destructive hover:text-destructive" @click="openDelete">Delete</Button>
      </div>
    </div>

    <!-- Stat bar -->
    <div class="bg-muted/50 rounded-lg px-4 py-3 flex items-center gap-5">
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
      <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Cards</div>

      <!-- Card table -->
      <div v-if="deck.cards.length > 0" class="rounded border border-border/60">
        <Table>
          <TableHeader>
            <TableRow v-for="headerGroup in table.getHeaderGroups()" :key="headerGroup.id">
              <TableHead
                v-for="header in headerGroup.headers"
                :key="header.id"
                class="h-9 text-[10px] uppercase tracking-[0.15em] text-muted-foreground cursor-pointer select-none"
                :class="(header.column.columnDef.meta as any)?.className"
                @click="header.column.getCanSort() ? header.column.toggleSorting() : undefined"
              >
                <div class="flex items-center gap-1">
                  <FlexRender
                    v-if="!header.isPlaceholder"
                    :render="header.column.columnDef.header"
                    :props="header.getContext()"
                  />
                  <span v-if="header.column.getIsSorted() === 'asc'" class="text-accent">↑</span>
                  <span v-else-if="header.column.getIsSorted() === 'desc'" class="text-accent">↓</span>
                </div>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow v-for="row in table.getRowModel().rows" :key="row.id" class="text-xs hover:bg-accent/5">
              <TableCell v-for="cell in row.getVisibleCells()" :key="cell.id" :class="(cell.column.columnDef.meta as any)?.className">
                <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </div>

      <div v-else class="py-12 text-center text-xs text-muted-foreground">
        No cards in this deck yet. Add cards from the Cards view.
      </div>
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

    <!-- Delete confirmation dialog -->
    <Dialog v-model:open="deleteOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete deck</DialogTitle>
          <DialogDescription>
            Are you sure you want to delete "{{ deck.name }}"?
          </DialogDescription>
        </DialogHeader>
        <div class="flex items-center gap-2">
          <Checkbox id="delete-cards" :checked="deleteCards" @update:checked="deleteCards = $event" />
          <label for="delete-cards" class="text-xs cursor-pointer select-none">
            Also delete all {{ deck.cardCount }} cards in this deck
          </label>
        </div>
        <DialogFooter class="gap-2">
          <Button variant="outline" size="sm" @click="deleteOpen = false">Cancel</Button>
          <Button variant="destructive" size="sm" @click="handleDelete">Delete</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
