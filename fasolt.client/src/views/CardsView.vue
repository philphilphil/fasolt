<script setup lang="ts">
import { h, ref, onMounted, computed } from 'vue'
import { RouterLink } from 'vue-router'
import type {
  ColumnDef,
  ColumnFiltersState,
  SortingState,
  VisibilityState,
} from '@tanstack/vue-table'
import {
  FlexRender,
  getCoreRowModel,
  getPaginationRowModel,
  getFilteredRowModel,
  getSortedRowModel,
  useVueTable,
} from '@tanstack/vue-table'
import { valueUpdater } from '@/lib/utils'
import { useCardsStore } from '@/stores/cards'
import { useDecksStore } from '@/stores/decks'
import type { Card } from '@/types'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import CardCreateDialog from '@/components/CardCreateDialog.vue'
import CardEditDialog from '@/components/CardEditDialog.vue'

const cardsStore = useCardsStore()
const decks = useDecksStore()

const editTarget = ref<Card | null>(null)
const editOpen = ref(false)
const deleteTarget = ref<Card | null>(null)
const deleteError = ref('')
const createOpen = ref(false)
const addToDeckCard = ref<Card | null>(null)
const addToDeckId = ref('')

onMounted(async () => {
  await Promise.all([cardsStore.fetchCards(), decks.fetchDecks()])
})

function openEdit(card: Card) {
  editTarget.value = card
  editOpen.value = true
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleteError.value = ''
  try {
    await cardsStore.deleteCard(deleteTarget.value.id)
    deleteTarget.value = null
  } catch {
    deleteError.value = 'Failed to delete card.'
  }
}

async function addCardToDeck() {
  if (!addToDeckCard.value || !addToDeckId.value) return
  try {
    await decks.addCards(addToDeckId.value, [addToDeckCard.value.id])
    await cardsStore.fetchCards()
    addToDeckCard.value = null
    addToDeckId.value = ''
  } catch {
    // silently fail
  }
}

const columns: ColumnDef<Card>[] = [
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
          class: 'hover:underline',
        }, () => display),
        source
          ? h('div', { class: 'truncate font-mono text-xs text-muted-foreground mt-0.5' }, source)
          : null,
      ])
    },
  },
  {
    id: 'source',
    accessorFn: (row) => row.sourceFile ?? '',
    filterFn: (row, _id, value) => !value || row.original.sourceFile === value,
    enableHiding: false,
  },
  {
    accessorKey: 'state',
    header: 'State',
    meta: { className: 'w-[80px]' },
    cell: ({ row }) => h(Badge, { variant: 'outline', class: 'text-xs' }, () => row.getValue('state')),
    filterFn: (row, _id, value) => !value || row.getValue('state') === value,
  },
  {
    id: 'decks',
    header: 'Decks',
    meta: { className: 'w-[160px]' },
    accessorFn: (row) => row.decks.map(d => d.name).join(', '),
    cell: ({ row }) => {
      const deckList = row.original.decks
      return h('div', { class: 'flex flex-wrap gap-1' }, [
        ...deckList.map(d => h(Badge, { key: d.id, variant: 'outline', class: 'text-xs whitespace-nowrap' }, () => d.name)),
        h('button', {
          class: 'text-xs text-muted-foreground hover:text-foreground',
          onClick: () => { addToDeckCard.value = row.original },
        }, '+'),
      ])
    },
  },
  {
    id: 'actions',
    enableHiding: false,
    meta: { className: 'w-[90px]' },
    cell: ({ row }) => {
      const card = row.original
      return h('div', { class: 'flex gap-1' }, [
        h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-xs', onClick: () => openEdit(card) }, () => 'Edit'),
        h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-xs text-muted-foreground hover:text-destructive', onClick: () => { deleteTarget.value = card } }, () => '×'),
      ])
    },
  },
]

const sorting = ref<SortingState>([])
const columnFilters = ref<ColumnFiltersState>([])
const columnVisibility = ref<VisibilityState>({ source: false })

const table = useVueTable({
  get data() { return cardsStore.cards },
  columns,
  getCoreRowModel: getCoreRowModel(),
  getPaginationRowModel: getPaginationRowModel(),
  getSortedRowModel: getSortedRowModel(),
  getFilteredRowModel: getFilteredRowModel(),
  onSortingChange: updaterOrValue => valueUpdater(updaterOrValue, sorting),
  onColumnFiltersChange: updaterOrValue => valueUpdater(updaterOrValue, columnFilters),
  onColumnVisibilityChange: updaterOrValue => valueUpdater(updaterOrValue, columnVisibility),
  state: {
    get sorting() { return sorting.value },
    get columnFilters() { return columnFilters.value },
    get columnVisibility() { return columnVisibility.value },
  },
  initialState: {
    pagination: { pageSize: 20 },
  },
})

const filterValue = computed({
  get: () => (table.getColumn('front')?.getFilterValue() as string) ?? '',
  set: (val) => table.getColumn('front')?.setFilterValue(val),
})

const sourceFilter = ref('')
const stateFilter = ref('')

function applySourceFilter(val: string) {
  sourceFilter.value = val
  table.getColumn('source')?.setFilterValue(val || undefined)
}

function applyStateFilter(val: string) {
  stateFilter.value = val
  table.getColumn('state')?.setFilterValue(val || undefined)
}
</script>

<template>
  <div class="space-y-4">
    <!-- Toolbar -->
    <div class="flex items-center justify-between gap-2">
      <div class="flex items-center gap-2 flex-1">
        <Input
          v-model="filterValue"
          placeholder="Filter cards..."
          class="h-8 max-w-[200px] text-xs"
        />
        <Input
          :value="sourceFilter"
          placeholder="Filter by source..."
          class="h-8 max-w-[200px] text-xs"
          @input="applySourceFilter(($event.target as HTMLInputElement).value)"
        />
        <select
          :value="stateFilter"
          class="h-8 rounded-md border border-border bg-transparent px-2 text-xs"
          @change="applyStateFilter(($event.target as HTMLSelectElement).value)"
        >
          <option value="">All states</option>
          <option value="new">new</option>
          <option value="learning">learning</option>
          <option value="mature">mature</option>
        </select>
        <DropdownMenu>
          <DropdownMenuTrigger as-child>
            <Button variant="outline" size="sm" class="h-8 text-xs ml-auto">
              Columns
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuCheckboxItem
              v-for="column in table.getAllColumns().filter(c => c.getCanHide())"
              :key="column.id"
              class="capitalize text-xs"
              :model-value="column.getIsVisible()"
              @update:model-value="(value: boolean) => column.toggleVisibility(!!value)"
            >
              {{ column.id }}
            </DropdownMenuCheckboxItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
      <Button size="sm" class="h-8 text-xs" @click="createOpen = true">New card</Button>
    </div>

    <!-- Table -->
    <div class="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow v-for="headerGroup in table.getHeaderGroups()" :key="headerGroup.id">
            <TableHead
              v-for="header in headerGroup.headers"
              :key="header.id"
              class="h-9 text-xs uppercase tracking-wider text-muted-foreground cursor-pointer select-none"
              :class="(header.column.columnDef.meta as any)?.className"
              @click="header.column.getCanSort() ? header.column.toggleSorting() : undefined"
            >
              <div class="flex items-center gap-1">
                <FlexRender
                  v-if="!header.isPlaceholder"
                  :render="header.column.columnDef.header"
                  :props="header.getContext()"
                />
                <span v-if="header.column.getIsSorted() === 'asc'" class="text-xs">↑</span>
                <span v-else-if="header.column.getIsSorted() === 'desc'" class="text-xs">↓</span>
              </div>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <template v-if="table.getRowModel().rows?.length">
            <TableRow v-for="row in table.getRowModel().rows" :key="row.id" class="text-sm">
              <TableCell v-for="cell in row.getVisibleCells()" :key="cell.id" :class="(cell.column.columnDef.meta as any)?.className">
                <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
              </TableCell>
            </TableRow>
          </template>
          <template v-else>
            <TableRow>
              <TableCell :colspan="columns.length" class="h-24 text-center text-sm text-muted-foreground">
                No cards yet. Create one or use the MCP agent to generate cards from your notes.
              </TableCell>
            </TableRow>
          </template>
        </TableBody>
      </Table>
    </div>

    <!-- Pagination -->
    <div v-if="table.getPageCount() > 1" class="flex items-center justify-between text-xs text-muted-foreground">
      <span>{{ table.getFilteredRowModel().rows.length }} card(s)</span>
      <div class="flex items-center gap-2">
        <Button variant="outline" size="sm" class="h-7 text-xs" :disabled="!table.getCanPreviousPage()" @click="table.previousPage()">
          Previous
        </Button>
        <span>Page {{ table.getState().pagination.pageIndex + 1 }} of {{ table.getPageCount() }}</span>
        <Button variant="outline" size="sm" class="h-7 text-xs" :disabled="!table.getCanNextPage()" @click="table.nextPage()">
          Next
        </Button>
      </div>
    </div>

    <!-- Dialogs -->
    <CardCreateDialog
      v-model:open="createOpen"
      :initial-fronts="['']"
      initial-back=""
      @created="cardsStore.fetchCards()"
    />

    <CardEditDialog
      v-model:open="editOpen"
      :card="editTarget"
      @updated="cardsStore.fetchCards()"
    />

    <Dialog :open="!!deleteTarget" @update:open="deleteTarget = null">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete card</DialogTitle>
          <DialogDescription>
            Are you sure you want to delete this card? It will be removed from study.
          </DialogDescription>
        </DialogHeader>
        <div v-if="deleteError" class="text-xs text-destructive">{{ deleteError }}</div>
        <DialogFooter>
          <Button variant="outline" @click="deleteTarget = null">Cancel</Button>
          <Button variant="destructive" @click="confirmDelete">Delete</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <Dialog :open="!!addToDeckCard" @update:open="addToDeckCard = null">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add to deck</DialogTitle>
          <DialogDescription>
            Add "{{ addToDeckCard?.front?.slice(0, 40) }}" to a deck.
          </DialogDescription>
        </DialogHeader>
        <select
          v-model="addToDeckId"
          class="w-full h-8 rounded-md border border-border bg-transparent px-2 text-xs"
        >
          <option value="">Select a deck</option>
          <option v-for="d in decks.decks" :key="d.id" :value="d.id">{{ d.name }}</option>
        </select>
        <DialogFooter>
          <Button variant="outline" @click="addToDeckCard = null">Cancel</Button>
          <Button :disabled="!addToDeckId" @click="addCardToDeck">Add</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
