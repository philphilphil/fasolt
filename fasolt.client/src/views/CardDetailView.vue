<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useCardsStore } from '@/stores/cards'
import { useMarkdown } from '@/composables/useMarkdown'
import type { Card } from '@/types'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const route = useRoute()
const router = useRouter()
const cardsStore = useCardsStore()
const { render } = useMarkdown()

function formatDate(iso: string): string {
  const d = new Date(iso)
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

const card = ref<Card | null>(null)
const loading = ref(true)

const deleteOpen = ref(false)
const editing = ref(false)
const front = ref('')
const back = ref('')
const saving = ref(false)
const error = ref('')

onMounted(async () => {
  try {
    card.value = await cardsStore.getCard(route.params.id as string)
  } catch {
    router.replace('/cards')
  } finally {
    loading.value = false
  }
})

function startEdit() {
  if (!card.value) return
  front.value = card.value.front
  back.value = card.value.back
  error.value = ''
  editing.value = true
}

async function save() {
  if (!card.value || !front.value.trim() || !back.value.trim()) {
    error.value = 'Front and back are required.'
    return
  }
  saving.value = true
  error.value = ''
  try {
    card.value = await cardsStore.updateCard(card.value.id, {
      front: front.value,
      back: back.value,
    })
    editing.value = false
  } catch {
    error.value = 'Failed to update card.'
  } finally {
    saving.value = false
  }
}

async function confirmDelete() {
  if (!card.value) return
  await cardsStore.deleteCard(card.value.id)
  router.replace('/cards')
}
</script>

<template>
  <div v-if="loading" class="py-12 text-center text-xs text-muted-foreground">Loading...</div>

  <div v-else-if="card" class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <Button variant="ghost" size="sm" class="h-7 text-[10px]" @click="router.push('/cards')">
          &larr; Cards
        </Button>
        <Badge variant="outline" class="text-[10px]">{{ card.state }}</Badge>
      </div>
      <div class="flex items-center gap-2">
        <Button v-if="!editing" variant="outline" size="sm" class="h-7 text-[10px]" @click="startEdit">Edit</Button>
        <Button
          variant="outline"
          size="sm"
          class="h-7 text-[10px] text-destructive hover:text-destructive"
          @click="deleteOpen = true"
        >
          Delete
        </Button>
      </div>
    </div>

    <!-- Metadata -->
    <div class="flex flex-wrap gap-x-6 gap-y-1 text-[11px] text-muted-foreground">
      <span v-if="card.sourceFile">Source: {{ card.sourceFile }}</span>
      <span v-if="card.sourceHeading">Section: {{ card.sourceHeading }}</span>
      <span v-if="card.decks.length > 0">Decks: {{ card.decks.map(d => d.name).join(', ') }}</span>
    </div>

    <!-- SRS Stats -->
    <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-6 gap-3">
      <div class="rounded border border-border/60 px-3 py-2">
        <div class="text-[10px] uppercase tracking-[0.1em] text-muted-foreground">State</div>
        <div class="text-xs font-medium mt-0.5">{{ card.state }}</div>
      </div>
      <div class="rounded border border-border/60 px-3 py-2">
        <div class="text-[10px] uppercase tracking-[0.1em] text-muted-foreground">Due</div>
        <div class="text-xs font-medium mt-0.5">{{ card.dueAt ? formatDate(card.dueAt) : '—' }}</div>
      </div>
      <div class="rounded border border-border/60 px-3 py-2">
        <div class="text-[10px] uppercase tracking-[0.1em] text-muted-foreground">Stability</div>
        <div class="text-xs font-medium mt-0.5">{{ card.stability != null ? card.stability.toFixed(2) : '—' }}</div>
      </div>
      <div class="rounded border border-border/60 px-3 py-2">
        <div class="text-[10px] uppercase tracking-[0.1em] text-muted-foreground">Difficulty</div>
        <div class="text-xs font-medium mt-0.5">{{ card.difficulty != null ? card.difficulty.toFixed(2) : '—' }}</div>
      </div>
      <div class="rounded border border-border/60 px-3 py-2">
        <div class="text-[10px] uppercase tracking-[0.1em] text-muted-foreground">Step</div>
        <div class="text-xs font-medium mt-0.5">{{ card.step ?? '—' }}</div>
      </div>
      <div class="rounded border border-border/60 px-3 py-2">
        <div class="text-[10px] uppercase tracking-[0.1em] text-muted-foreground">Last Review</div>
        <div class="text-xs font-medium mt-0.5">{{ card.lastReviewedAt ? formatDate(card.lastReviewedAt) : '—' }}</div>
      </div>
      <div class="rounded border border-border/60 px-3 py-2">
        <div class="text-[10px] uppercase tracking-[0.1em] text-muted-foreground">Created</div>
        <div class="text-xs font-medium mt-0.5">{{ formatDate(card.createdAt) }}</div>
      </div>
    </div>

    <!-- Edit mode -->
    <div v-if="editing" class="space-y-4">
      <div class="space-y-1">
        <label class="text-[10px] font-medium text-muted-foreground uppercase tracking-[0.1em]">Front (question)</label>
        <textarea
          v-model="front"
          class="w-full rounded border border-border bg-transparent px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
          rows="3"
        />
      </div>
      <div class="space-y-1">
        <label class="text-[10px] font-medium text-muted-foreground uppercase tracking-[0.1em]">Back (answer)</label>
        <textarea
          v-model="back"
          class="w-full rounded border border-border bg-transparent px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
          rows="8"
        />
      </div>
      <div v-if="error" class="text-xs text-destructive">{{ error }}</div>
      <div class="flex gap-2">
        <Button size="sm" class="text-xs" :disabled="saving" @click="save">
          {{ saving ? 'Saving...' : 'Save' }}
        </Button>
        <Button variant="outline" size="sm" class="text-xs" @click="editing = false">Cancel</Button>
      </div>
    </div>

    <!-- View mode -->
    <div v-else class="space-y-4">
      <div class="space-y-1">
        <label class="text-[10px] font-medium text-muted-foreground uppercase tracking-[0.1em]">Front</label>
        <div class="prose dark:prose-invert max-w-none rounded border border-border/60 p-4" v-html="render(card.front)" />
      </div>
      <div class="space-y-1">
        <label class="text-[10px] font-medium text-muted-foreground uppercase tracking-[0.1em]">Back</label>
        <div class="prose dark:prose-invert max-w-none rounded border border-border/60 p-4" v-html="render(card.back)" />
      </div>
    </div>

    <!-- Delete confirmation -->
    <Dialog v-model:open="deleteOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete card</DialogTitle>
          <DialogDescription>
            Are you sure you want to delete this card? It will be removed from study.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" @click="deleteOpen = false">Cancel</Button>
          <Button variant="destructive" @click="confirmDelete">Delete</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
