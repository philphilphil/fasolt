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
import { useFilesStore } from '@/stores/files'
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
const files = useFilesStore()
const decks = useDecksStore()

const editTarget = ref<Card | null>(null)
const editOpen = ref(false)
const deleteTarget = ref<Card | null>(null)
const deleteError = ref('')
const createOpen = ref(false)
const addToDeckCard = ref<Card | null>(null)
const addToDeckId = ref('')

onMounted(async () => {
  await Promise.all([cardsStore.fetchCards(), files.fetchFiles(), decks.fetchDecks()])
})

function getFileName(fileId: string | null): string {
  if (!fileId) return '—'
  const f = files.files.find(f => f.id === fileId)
  return f?.fileName ?? '(deleted)'
}

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
      const display = val.length > 60 ? val.slice(0, 60) + '…' : val
      return h(RouterLink, {
        to: `/cards/${row.original.id}`,
        class: 'hover:underline',
      }, () => display)
    },
  },
  {
    id: 'source',
    header: 'Source',
    accessorFn: (row) => getFileName(row.fileId),
    cell: ({ row }) => h('span', { class: 'font-mono' }, getFileName(row.original.fileId)),
    filterFn: (row, _id, value) => !value || row.original.fileId === value,
  },
  {
    accessorKey: 'cardType',
    header: 'Type',
    cell: ({ row }) => h(Badge, { variant: 'secondary', class: 'text-[10px]' }, () => row.getValue('cardType')),
    filterFn: (row, _id, value) => !value || row.getValue('cardType') === value,
  },
  {
    accessorKey: 'state',
    header: 'State',
    cell: ({ row }) => h(Badge, { variant: 'outline', class: 'text-[10px]' }, () => row.getValue('state')),
    filterFn: (row, _id, value) => !value || row.getValue('state') === value,
  },
  {
    id: 'decks',
    header: 'Decks',
    accessorFn: (row) => row.decks.map(d => d.name).join(', '),
    cell: ({ row }) => {
      const deckList = row.original.decks
      return h('div', { class: 'flex flex-wrap gap-1' }, [
        ...deckList.map(d => h(Badge, { key: d.id, variant: 'outline', class: 'text-[10px]' }, () => d.name)),
        h('button', {
          class: 'text-[10px] text-muted-foreground hover:text-foreground',
          onClick: () => { addToDeckCard.value = row.original },
        }, '+'),
      ])
    },
  },
  {
    id: 'actions',
    enableHiding: false,
    cell: ({ row }) => {
      const card = row.original
      return h('div', { class: 'flex gap-1' }, [
        h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-[10px]', onClick: () => openEdit(card) }, () => 'Edit'),
        h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-[10px] text-muted-foreground hover:text-destructive', onClick: () => { deleteTarget.value = card } }, () => '×'),
      ])
    },
  },
]

const sorting = ref<SortingState>([])
const columnFilters = ref<ColumnFiltersState>([])
const columnVisibility = ref<VisibilityState>({})

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
const typeFilter = ref('')
const stateFilter = ref('')

function applySourceFilter(val: string) {
  sourceFilter.value = val
  table.getColumn('source')?.setFilterValue(val || undefined)
}

function applyTypeFilter(val: string) {
  typeFilter.value = val
  table.getColumn('cardType')?.setFilterValue(val || undefined)
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
        <select
          :value="sourceFilter"
          class="h-8 rounded-md border border-border bg-transparent px-2 text-xs"
          @change="applySourceFilter(($event.target as HTMLSelectElement).value)"
        >
          <option value="">All files</option>
          <option v-for="f in files.files" :key="f.id" :value="f.id">{{ f.fileName }}</option>
        </select>
        <select
          :value="typeFilter"
          class="h-8 rounded-md border border-border bg-transparent px-2 text-xs"
          @change="applyTypeFilter(($event.target as HTMLSelectElement).value)"
        >
          <option value="">All types</option>
          <option value="file">file</option>
          <option value="section">section</option>
          <option value="custom">custom</option>
        </select>
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
              class="h-9 text-[10px] uppercase tracking-wider text-muted-foreground cursor-pointer select-none"
              @click="header.column.getCanSort() ? header.column.toggleSorting() : undefined"
            >
              <div class="flex items-center gap-1">
                <FlexRender
                  v-if="!header.isPlaceholder"
                  :render="header.column.columnDef.header"
                  :props="header.getContext()"
                />
                <span v-if="header.column.getIsSorted() === 'asc'" class="text-[10px]">↑</span>
                <span v-else-if="header.column.getIsSorted() === 'desc'" class="text-[10px]">↓</span>
              </div>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <template v-if="table.getRowModel().rows?.length">
            <TableRow v-for="row in table.getRowModel().rows" :key="row.id" class="text-xs">
              <TableCell v-for="cell in row.getVisibleCells()" :key="cell.id">
                <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
              </TableCell>
            </TableRow>
          </template>
          <template v-else>
            <TableRow>
              <TableCell :colspan="columns.length" class="h-24 text-center text-sm text-muted-foreground">
                No cards yet. Create one from a file or start from scratch.
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
      card-type="custom"
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
