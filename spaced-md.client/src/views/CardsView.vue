<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useCardsStore } from '@/stores/cards'
import { useFilesStore } from '@/stores/files'
import type { Card } from '@/types'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import CardCreateDialog from '@/components/CardCreateDialog.vue'
import CardEditDialog from '@/components/CardEditDialog.vue'

const cards = useCardsStore()
const files = useFilesStore()

const filterFileId = ref<string>('')
const editTarget = ref<Card | null>(null)
const editOpen = ref(false)
const deleteTarget = ref<Card | null>(null)
const deleteError = ref('')
const createOpen = ref(false)

onMounted(async () => {
  await Promise.all([cards.fetchCards(), files.fetchFiles()])
})

async function applyFilter() {
  await cards.fetchCards(filterFileId.value || undefined)
}

function getFileName(fileId: string | null): string {
  if (!fileId) return '—'
  const f = files.files.find(f => f.id === fileId)
  return f?.fileName ?? '(deleted)'
}

function truncate(text: string, max = 60): string {
  return text.length > max ? text.slice(0, max) + '…' : text
}

function formatDate(date: string): string {
  return new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

function openEdit(card: Card) {
  editTarget.value = card
  editOpen.value = true
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleteError.value = ''
  try {
    await cards.deleteCard(deleteTarget.value.id)
    deleteTarget.value = null
  } catch {
    deleteError.value = 'Failed to delete card.'
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <select
          v-model="filterFileId"
          class="h-7 rounded-md border border-border bg-transparent px-2 text-xs"
          @change="applyFilter"
        >
          <option value="">All files</option>
          <option v-for="f in files.files" :key="f.id" :value="f.id">{{ f.fileName }}</option>
        </select>
      </div>
      <Button size="sm" class="h-7 text-xs" @click="createOpen = true">New card</Button>
    </div>

    <Table v-if="cards.cards.length > 0">
      <TableHeader>
        <TableRow class="text-[10px] uppercase tracking-wider text-muted-foreground hover:bg-transparent">
          <TableHead class="h-8">Front</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Source</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Type</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Created</TableHead>
          <TableHead class="h-8 w-20"></TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow v-for="card in cards.cards" :key="card.id" class="text-xs">
          <TableCell class="font-medium text-foreground max-w-[200px] truncate">{{ truncate(card.front) }}</TableCell>
          <TableCell class="hidden text-muted-foreground sm:table-cell font-mono">{{ getFileName(card.fileId) }}</TableCell>
          <TableCell class="hidden sm:table-cell">
            <Badge variant="secondary" class="text-[10px]">{{ card.cardType }}</Badge>
          </TableCell>
          <TableCell class="hidden text-muted-foreground sm:table-cell">{{ formatDate(card.createdAt) }}</TableCell>
          <TableCell class="flex gap-1">
            <Button variant="ghost" size="sm" class="h-6 text-[10px]" @click="openEdit(card)">Edit</Button>
            <Button variant="ghost" size="sm" class="h-6 text-[10px] text-muted-foreground hover:text-destructive" @click="deleteTarget = card">&times;</Button>
          </TableCell>
        </TableRow>
      </TableBody>
    </Table>

    <div v-if="!cards.loading && cards.cards.length === 0" class="py-12 text-center text-sm text-muted-foreground">
      No cards yet. Create one from a file or start from scratch.
    </div>

    <CardCreateDialog
      v-model:open="createOpen"
      card-type="custom"
      :initial-fronts="['']"
      initial-back=""
      @created="cards.fetchCards(filterFileId || undefined)"
    />

    <CardEditDialog
      v-model:open="editOpen"
      :card="editTarget"
      @updated="cards.fetchCards(filterFileId || undefined)"
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
  </div>
</template>
