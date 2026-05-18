<script setup lang="ts">
import { h, ref, computed, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import type { ColumnDef, SortingState } from '@tanstack/vue-table'
import {
  FlexRender,
  getCoreRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useVueTable,
} from '@tanstack/vue-table'
import { valueUpdater, stripMarkdown, deckColor } from '@/lib/utils'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Image } from 'lucide-vue-next'
import { formatDateDot } from '@/lib/formatDate'

const router = useRouter()

const props = withDefaults(defineProps<{
  cards: any[]
  showDecks?: boolean
  showPagination?: boolean
  pageSize?: number
  deckContext?: { id: string; name: string } | null
  selectable?: boolean
  selectedIds?: string[]
}>(), {
  showDecks: false,
  showPagination: false,
  pageSize: 30,
  deckContext: null,
  selectable: false,
  selectedIds: () => [],
})

const emit = defineEmits<{
  delete: [card: any]
  remove: [card: any]
  addToDeck: [card: any]
  suspend: [card: any]
  'update:selectedIds': [ids: string[]]
}>()

// ---- Selection model (controlled by parent) ----
const selectedSet = computed(() => new Set(props.selectedIds))

function setSelection(ids: string[]) {
  emit('update:selectedIds', ids)
}

function toggleRow(id: string) {
  const next = new Set(selectedSet.value)
  if (next.has(id)) next.delete(id); else next.add(id)
  setSelection([...next])
}

const pageRowIds = computed(() =>
  table.getRowModel().rows.map(r => r.original.id as string)
)
const pageAllSelected = computed(() =>
  pageRowIds.value.length > 0 && pageRowIds.value.every(id => selectedSet.value.has(id))
)
const pageSomeSelected = computed(() =>
  pageRowIds.value.some(id => selectedSet.value.has(id)) && !pageAllSelected.value
)

function toggleSelectAllOnPage() {
  if (pageAllSelected.value) {
    const next = new Set(selectedSet.value)
    for (const id of pageRowIds.value) next.delete(id)
    setSelection([...next])
  } else {
    const next = new Set(selectedSet.value)
    for (const id of pageRowIds.value) next.add(id)
    setSelection([...next])
  }
}

// Drop selections that fall outside the current filtered data set.
watch(() => props.cards, (cards) => {
  if (!props.selectable || selectedSet.value.size === 0) return
  const visible = new Set(cards.map(c => c.id))
  const filtered = props.selectedIds.filter(id => visible.has(id))
  if (filtered.length !== props.selectedIds.length) setSelection(filtered)
})

// ---- Columns ----
const columns = computed<ColumnDef<any>[]>(() => {
  const cols: ColumnDef<any>[] = []

  if (props.selectable) {
    cols.push({
      id: 'select',
      enableSorting: false,
      meta: { className: 'w-[34px] py-2' },
      header: () => h('label', { class: 'sel-check', onClick: (e: MouseEvent) => e.stopPropagation() }, [
        h('input', {
          type: 'checkbox',
          checked: pageAllSelected.value,
          indeterminate: pageSomeSelected.value,
          onChange: () => toggleSelectAllOnPage(),
        }),
        h('span', { class: ['sel-box', pageAllSelected.value && 'is-checked', pageSomeSelected.value && 'is-indeterminate'] }),
      ]),
      cell: ({ row }) => {
        const id = row.original.id as string
        const checked = selectedSet.value.has(id)
        return h('label', { class: 'sel-check', onClick: (e: MouseEvent) => e.stopPropagation() }, [
          h('input', {
            type: 'checkbox',
            checked,
            onChange: () => toggleRow(id),
          }),
          h('span', { class: ['sel-box', checked && 'is-checked'] }),
        ])
      },
    })
  }

  cols.push({
    accessorKey: 'front',
    header: 'Front',
    cell: ({ row }) => {
      const val = stripMarkdown(row.getValue('front') as string)
      const display = val.length > 100 ? val.slice(0, 100) + '…' : val
      const source = row.original.sourceFile
      const hasSvg = row.original.frontSvg || row.original.backSvg
      const cardLink = props.deckContext
        ? `/cards/${row.original.id}?deckId=${props.deckContext.id}`
        : `/cards/${row.original.id}`
      return h('div', { class: 'cell-front' }, [
        h('div', { class: 'cell-front-row' }, [
          h(RouterLink, {
            to: cardLink,
            class: 'cell-front-text',
          }, () => display),
          hasSvg ? h(Image, { class: 'cell-front-icon' }) : null,
        ]),
        source
          ? h('div', { class: 'cell-front-source fa-mono' }, source)
          : null,
      ])
    },
  })

  cols.push({
    accessorKey: 'back',
    header: 'Back',
    enableSorting: false,
    meta: { className: 'col-back' },
    cell: ({ row }) => {
      const val = stripMarkdown(row.original.back || '')
      const display = val.length > 90 ? val.slice(0, 90) + '…' : val
      return h('div', { class: 'cell-back' }, display)
    },
  })

  cols.push({
    accessorKey: 'state',
    header: 'State',
    meta: { className: 'col-state' },
    cell: ({ row }) => {
      const state = row.getValue('state') as string
      return h('span', { class: ['state-chip', `state-${state}`] }, state)
    },
  })

  if (props.showDecks) {
    cols.push({
      id: 'decks',
      header: 'Decks',
      enableSorting: false,
      meta: { className: 'col-decks' },
      accessorFn: (row) => row.decks?.map((d: any) => d.name).join(', ') ?? '',
      cell: ({ row }) => {
        const deckList = row.original.decks ?? []
        if (deckList.length === 0) {
          return h('span', { class: 'deck-empty fa-mono' }, '—')
        }
        return h('div', { class: 'deck-chips' },
          deckList.map((d: any) =>
            h('span', { key: d.id, class: 'deck-chip', title: d.name }, [
              h('span', { class: 'fa-tag', style: { background: deckColor(d.id) } }),
              h('span', { class: 'deck-chip-name' }, d.name),
            ])
          )
        )
      },
    })
  }

  cols.push({
    accessorKey: 'dueAt',
    header: 'Due',
    meta: { className: 'col-due whitespace-nowrap' },
    cell: ({ row }) => {
      const due = formatDateDot(row.getValue('dueAt') as string | null)
      return h('span', { class: 'cell-due fa-mono' }, due || '—')
    },
  })

  // When selection mode is on, drop the per-row action cluster entirely — bulk
  // bar handles the actions, and the Front column's RouterLink opens the card.
  if (!props.selectable) {
    cols.push({
      id: 'actions',
      enableSorting: false,
      enableHiding: false,
      meta: { className: 'col-actions' },
      cell: ({ row }) => {
        const card = row.original
        const buttons = [
          h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-[10px]', onClick: () => {
            const editLink = props.deckContext
              ? `/cards/${card.id}?edit=true&deckId=${props.deckContext.id}`
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
        return h('div', { class: 'cell-actions' }, buttons)
      },
    })
  }

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
  state: { get sorting() { return sorting.value } },
  initialState: { pagination: { pageSize: props.pageSize } },
})
</script>

<template>
  <div class="card-table-wrap">
    <Table class="card-table">
      <TableHeader>
        <TableRow v-for="headerGroup in table.getHeaderGroups()" :key="headerGroup.id" class="card-table-headrow">
          <TableHead
            v-for="header in headerGroup.headers"
            :key="header.id"
            class="card-table-head"
            :class="[(header.column.columnDef.meta as any)?.className, header.column.getCanSort() ? 'sortable' : '']"
            @click="header.column.getCanSort() ? header.column.toggleSorting() : undefined"
          >
            <span class="card-table-head-inner">
              <FlexRender
                v-if="!header.isPlaceholder"
                :render="header.column.columnDef.header"
                :props="header.getContext()"
              />
              <span v-if="header.column.getIsSorted() === 'asc'" class="sort-icon">↑</span>
              <span v-else-if="header.column.getIsSorted() === 'desc'" class="sort-icon">↓</span>
            </span>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <template v-if="table.getRowModel().rows?.length">
          <TableRow
            v-for="row in table.getRowModel().rows"
            :key="row.id"
            class="card-table-row"
            :class="{
              'is-suspended': row.original.isSuspended,
              'is-selected': selectable && selectedSet.has(row.original.id),
            }"
          >
            <TableCell
              v-for="cell in row.getVisibleCells()"
              :key="cell.id"
              class="card-table-cell"
              :class="(cell.column.columnDef.meta as any)?.className"
            >
              <FlexRender :render="cell.column.columnDef.cell" :props="cell.getContext()" />
            </TableCell>
          </TableRow>
        </template>
        <template v-else>
          <TableRow>
            <TableCell :colspan="columns.length" class="empty-cell">
              <slot name="empty">No cards.</slot>
            </TableCell>
          </TableRow>
        </template>
      </TableBody>
    </Table>
  </div>

  <div v-if="showPagination && table.getPageCount() > 1" class="card-table-pagination">
    <span class="fa-mono">{{ table.getFilteredRowModel().rows.length }} card{{ table.getFilteredRowModel().rows.length === 1 ? '' : 's' }} · page {{ table.getState().pagination.pageIndex + 1 }} / {{ table.getPageCount() }}</span>
    <div class="card-table-pag-actions">
      <button class="fa-btn" :disabled="!table.getCanPreviousPage()" @click="table.previousPage()">← prev</button>
      <button class="fa-btn" :disabled="!table.getCanNextPage()" @click="table.nextPage()">next →</button>
    </div>
  </div>
</template>

<style scoped>
.card-table-wrap {
  border: 1px solid var(--rule-1);
  border-radius: 10px;
  background: var(--paper-1);
  overflow: hidden;
}
/* `:deep` needed: shadcn Table.vue renders <div><table/></div>, so the inner
   <table> is one level below Table.vue's root and doesn't inherit our scope
   attribute. Without :deep, this selector silently fails to match. */
:deep(.card-table) {
  table-layout: fixed;
}

:deep(.card-table-headrow) {
  background: var(--paper-2);
}
.card-table-head {
  font-family: 'Geist Mono', ui-monospace, monospace;
  text-transform: uppercase;
  font-size: 10px;
  letter-spacing: 0.18em;
  color: var(--ink-2);
  padding: 10px 14px;
  height: 36px;
  border-bottom: 1px solid var(--rule-1);
}
.card-table-head.sortable { cursor: pointer; user-select: none; }
.card-table-head.sortable:hover { color: var(--ink-0); }
.card-table-head-inner {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}
.sort-icon { color: var(--accent); font-family: 'Geist Mono', monospace; }

:deep(.card-table-row) {
  border-bottom: 1px solid var(--rule-2);
  transition: background .1s;
}
:deep(.card-table-row:last-child) { border-bottom: none; }
:deep(.card-table-row:hover) { background: var(--paper-2); }
:deep(.card-table-row.is-suspended) { opacity: 0.5; }
:deep(.card-table-row.is-selected) { background: var(--accent-soft); }
:deep(.card-table-row.is-selected:hover) {
  background: color-mix(in oklch, var(--accent-soft) 70%, var(--paper-2));
}

.card-table-cell {
  padding: 11px 14px;
  font-size: 13px;
  color: var(--ink-1);
  vertical-align: top;
}

/* Column widths */
:deep(.col-back) { width: 26%; }
:deep(.col-state) { width: 96px; }
:deep(.col-decks) { width: 180px; }
:deep(.col-due) { width: 90px; }
:deep(.col-actions) { width: 180px; }

/* Front column */
:deep(.cell-front) { min-width: 0; }
:deep(.cell-front-row) {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}
:deep(.cell-front-text) {
  font-size: 13.5px;
  color: var(--ink-0);
  text-decoration: none;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  transition: color .12s;
}
:deep(.cell-front-text:hover) { color: var(--accent); }
:deep(.cell-front-icon) {
  width: 12px;
  height: 12px;
  color: var(--ink-3);
  flex-shrink: 0;
}
:deep(.cell-front-source) {
  font-size: 11px;
  color: var(--ink-3);
  margin-top: 3px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* Back column — width is enforced by .col-back + table-layout: fixed;
   the inner div just clips its single line of text. */
:deep(.cell-back) {
  font-size: 12.5px;
  color: var(--ink-2);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* Hide low-priority columns on narrow screens so 7 columns don't fight for
   a phone-width container. Users can still see the Back content by opening
   the card. */
@media (max-width: 900px) {
  :deep(.col-back) { display: none; }
}
@media (max-width: 640px) {
  :deep(.col-decks), :deep(.col-due) { display: none; }
}

/* State chips */
:deep(.state-chip) {
  display: inline-block;
  font-size: 10.5px;
  font-family: 'Geist Mono', ui-monospace, monospace;
  padding: 2px 7px;
  border: 1px solid currentColor;
  border-radius: 3px;
  text-transform: lowercase;
  letter-spacing: 0.02em;
  opacity: 0.85;
  color: var(--ink-1);
}
:deep(.state-chip.state-new) { color: var(--c-easy); }
:deep(.state-chip.state-learning),
:deep(.state-chip.state-relearning) { color: var(--c-hard); }
:deep(.state-chip.state-review) { color: var(--ink-1); }

/* Deck chips (read-only) */
:deep(.deck-chips) {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
:deep(.deck-chip) {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 2px 7px 2px 5px;
  border: 1px solid var(--rule-1);
  border-radius: 999px;
  font-size: 11px;
  color: var(--ink-1);
  background: var(--paper-1);
  white-space: nowrap;
  max-width: 160px;
}
:deep(.deck-chip-name) {
  overflow: hidden;
  text-overflow: ellipsis;
}
:deep(.deck-empty) {
  color: var(--ink-3);
  font-size: 12px;
}

/* Due column */
:deep(.cell-due) {
  font-size: 12px;
  color: var(--ink-2);
}

/* Action cells */
:deep(.cell-actions) {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

/* Selection checkbox */
:deep(.sel-check) {
  display: inline-flex;
  align-items: center;
  cursor: pointer;
  position: relative;
}
:deep(.sel-check input) {
  position: absolute;
  inset: 0;
  width: 16px;
  height: 16px;
  opacity: 0;
  margin: 0;
  cursor: pointer;
}
:deep(.sel-box) {
  width: 16px;
  height: 16px;
  border: 1px solid var(--rule-1);
  border-radius: 3.5px;
  background: var(--paper-1);
  position: relative;
  transition: background .12s, border-color .12s;
}
:deep(.sel-box::after) {
  content: '';
  position: absolute;
  inset: 0;
  display: none;
}
:deep(.sel-box.is-checked) {
  background: var(--accent);
  border-color: var(--accent);
}
:deep(.sel-box.is-checked::after) {
  display: block;
  background: no-repeat center/10px 10px url("data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='white' stroke-width='3' stroke-linecap='round' stroke-linejoin='round'><path d='m5 12 5 5L20 7'/></svg>");
}
:deep(.sel-box.is-indeterminate) {
  background: var(--accent);
  border-color: var(--accent);
}
:deep(.sel-box.is-indeterminate::after) {
  display: block;
  background: no-repeat center/10px 2px linear-gradient(white, white);
}
:deep(.card-table-row:hover .sel-box:not(.is-checked):not(.is-indeterminate)) {
  border-color: var(--ink-3);
}

/* Empty */
.empty-cell {
  height: 96px;
  text-align: center;
  font-size: 13px;
  color: var(--ink-2);
}

/* Pagination */
.card-table-pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 10px;
  font-size: 12px;
  color: var(--ink-2);
}
.card-table-pagination .fa-mono { font-size: 11px; }
.card-table-pag-actions {
  display: flex;
  gap: 6px;
}
.card-table-pag-actions .fa-btn {
  height: 26px;
  padding: 0 10px;
  font-size: 12px;
}
</style>
