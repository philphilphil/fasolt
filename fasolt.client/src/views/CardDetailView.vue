<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
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

const truncatedFront = computed(() => {
  if (!card.value) return ''
  return card.value.front.length > 60 ? card.value.front.slice(0, 60) + '…' : card.value.front
})

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
    <!-- Breadcrumb -->
    <div class="text-[11px] text-muted-foreground">
      <RouterLink to="/cards" class="hover:text-foreground transition-colors">Cards</RouterLink>
      <span class="mx-1.5">/</span>
      <span class="text-foreground">{{ truncatedFront }}</span>
    </div>

    <!-- Header -->
    <div class="flex items-start justify-between">
      <div class="flex items-center gap-2.5">
        <h1 class="text-base font-bold tracking-tight">{{ truncatedFront }}</h1>
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
    <div class="flex flex-wrap gap-x-6 gap-y-1 text-xs text-muted-foreground">
      <span v-if="card.sourceFile">Source: <span class="text-foreground">{{ card.sourceFile }}</span></span>
      <span v-if="card.sourceHeading">Section: <span class="text-foreground">{{ card.sourceHeading }}</span></span>
      <span v-if="card.decks.length > 0">Decks: <span class="text-foreground">{{ card.decks.map(d => d.name).join(', ') }}</span></span>
    </div>

    <!-- SRS Stats -->
    <div class="bg-secondary rounded-lg px-4 py-3 grid grid-cols-4 sm:grid-cols-7 gap-4">
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">State</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.state }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Due</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.dueAt ? formatDate(card.dueAt) : '—' }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Stability</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.stability != null ? card.stability.toFixed(2) : '—' }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Difficulty</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.difficulty != null ? card.difficulty.toFixed(2) : '—' }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Step</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.step ?? '—' }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Last Review</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.lastReviewedAt ? formatDate(card.lastReviewedAt) : '—' }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Created</div>
        <div class="text-sm font-semibold mt-0.5">{{ formatDate(card.createdAt) }}</div>
      </div>
    </div>

    <!-- Edit mode -->
    <div v-if="editing" class="space-y-4">
      <div class="space-y-1">
        <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Front (question)</div>
        <textarea
          v-model="front"
          class="w-full rounded border border-border bg-transparent px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
          rows="3"
        />
      </div>
      <div class="space-y-1">
        <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Back (answer)</div>
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
    <div v-else class="space-y-5">
      <div>
        <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Front</div>
        <div class="prose dark:prose-invert max-w-none rounded border border-border/60 px-5 py-4" v-html="render(card.front)" />
      </div>
      <div>
        <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Back</div>
        <div class="prose dark:prose-invert max-w-none rounded border border-border/60 px-5 py-4" v-html="render(card.back)" />
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
