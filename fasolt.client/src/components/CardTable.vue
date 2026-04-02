<script setup lang="ts">
import { h, ref, computed } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import type { ColumnDef, SortingState } from '@tanstack/vue-table'
import {
  FlexRender,
  getCoreRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useVueTable,
} from '@tanstack/vue-table'
import { valueUpdater, stripMarkdown } from '@/lib/utils'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Image } from 'lucide-vue-next'
import { formatDateDot } from '@/lib/formatDate'

const router = useRouter()
const copiedCardId = ref<string | null>(null)

function copyCardId(id: string) {
  navigator.clipboard.writeText(id)
  copiedCardId.value = id
  setTimeout(() => copiedCardId.value = null, 2000)
}

const props = withDefaults(defineProps<{
  cards: any[]
  showDecks?: boolean
  showPagination?: boolean
  pageSize?: number
  deckContext?: { id: string; name: string } | null
}>(), {
  showDecks: false,
  showPagination: false,
  pageSize: 20,
  deckContext: null,
})

const emit = defineEmits<{
  delete: [card: any]
  remove: [card: any]
  addToDeck: [card: any]
  suspend: [card: any]
}>()

const columns = computed<ColumnDef<any>[]>(() => {
  const cols: ColumnDef<any>[] = [
    {
      accessorKey: 'front',
      header: 'Front',
      cell: ({ row }) => {
        const val = stripMarkdown(row.getValue('front') as string)
        const display = val.length > 80 ? val.slice(0, 80) + '…' : val
        const source = row.original.sourceFile
        const hasSvg = row.original.frontSvg || row.original.backSvg
        const cardLink = props.deckContext
          ? `/cards/${row.original.id}?deckId=${props.deckContext.id}&deckName=${encodeURIComponent(props.deckContext.name)}`
          : `/cards/${row.original.id}`
        return h('div', { class: 'min-w-0' }, [
          h('div', { class: 'flex items-center gap-1.5' }, [
            h(RouterLink, {
              to: cardLink,
              class: 'hover:text-accent transition-colors',
            }, () => display),
            hasSvg ? h(Image, { class: 'size-3 text-muted-foreground shrink-0' }) : null,
          ]),
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
  ]

  if (props.showDecks) {
    cols.push({
      id: 'decks',
      header: 'Decks',
      meta: { className: 'w-[160px]' },
      accessorFn: (row) => row.decks?.map((d: any) => d.name).join(', ') ?? '',
      cell: ({ row }) => {
        const deckList = row.original.decks ?? []
        return h('div', { class: 'flex flex-wrap gap-1' }, [
          ...deckList.map((d: any) => h(Badge, { key: d.id, variant: 'outline', class: 'text-[10px] whitespace-nowrap' }, () => d.name)),
          h('button', {
            class: 'text-[10px] text-muted-foreground hover:text-accent',
            onClick: () => emit('addToDeck', row.original),
          }, '+'),
        ])
      },
    })
  }

  cols.push({
    accessorKey: 'dueAt',
    header: 'Due',
    meta: { className: 'w-[100px] whitespace-nowrap' },
    cell: ({ row }) => h('span', { class: 'text-muted-foreground' }, formatDateDot(row.getValue('dueAt') as string | null)),
  })

  cols.push({
    id: 'actions',
    enableSorting: false,
    enableHiding: false,
    meta: { className: 'w-[160px]' },
    cell: ({ row }) => {
      const card = row.original
      const buttons = [
        h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-[10px]', onClick: () => copyCardId(card.id) },
          () => copiedCardId.value === card.id ? 'Copied!' : 'Copy ID'),
        h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-[10px]', onClick: () => {
          const editLink = props.deckContext
            ? `/cards/${card.id}?edit=true&deckId=${props.deckContext.id}&deckName=${encodeURIComponent(props.deckContext.name)}`
            : `/cards/${card.id}?edit=true`
          router.push(editLink)
        } }, () => 'Edit'),
      ]
      if (!props.showDecks) {
        buttons.push(
          h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-[10px]', onClick: () => emit('remove', card) }, () => 'Remove'),
        )
      }
      buttons.push(
        h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-[10px]', onClick: () => emit('suspend', card) }, () => card.isSuspended ? 'Unsuspend' : 'Suspend'),
      )
      buttons.push(
        h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-[10px] text-muted-foreground hover:text-destructive', onClick: () => emit('delete', card) }, () => '×'),
      )
      return h('div', { class: 'flex gap-1' }, buttons)
    },
  })

  return cols
})

const sorting = ref<SortingState>([])

const table = useVueTable({
  get data() { return props.cards },
  get columns() { return columns.value },
  getCoreRowModel: getCoreRowModel(),
  getPaginationRowModel: getPaginationRowModel(),
  getSortedRowModel: getSortedRowModel(),
  onSortingChange: updaterOrValue => valueUpdater(updaterOrValue, sorting),
  state: {
    get sorting() { return sorting.value },
  },
  initialState: {
    pagination: { pageSize: props.pageSize },
  },
})
</script>

<template>
  <div class="rounded border border-border/60">
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
        <template v-if="table.getRowModel().rows?.length">
          <TableRow v-for="row in table.getRowModel().rows" :key="row.id" :class="['text-xs hover:bg-accent/5', row.original.isSuspended && 'opacity-50']">
            <TableCell v-for="cell in row.getVisibleCells()" :key="cell.id" :class="(cell.column.columnDef.meta as any)?.className">
              <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
            </TableCell>
          </TableRow>
        </template>
        <template v-else>
          <TableRow>
            <TableCell :colspan="columns.length" class="h-24 text-center text-xs text-muted-foreground">
              <slot name="empty">No cards.</slot>
            </TableCell>
          </TableRow>
        </template>
      </TableBody>
    </Table>
  </div>

  <!-- Pagination -->
  <div v-if="showPagination && table.getPageCount() > 1" class="flex items-center justify-between text-[11px] text-muted-foreground mt-4">
    <span>{{ table.getFilteredRowModel().rows.length }} card(s)</span>
    <div class="flex items-center gap-2">
      <Button variant="outline" size="sm" class="h-7 text-[10px]" :disabled="!table.getCanPreviousPage()" @click="table.previousPage()">
        Previous
      </Button>
      <span>Page {{ table.getState().pagination.pageIndex + 1 }} of {{ table.getPageCount() }}</span>
      <Button variant="outline" size="sm" class="h-7 text-[10px]" :disabled="!table.getCanNextPage()" @click="table.nextPage()">
        Next
      </Button>
    </div>
  </div>
</template>
