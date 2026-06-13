<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { useCardsStore } from '@/stores/cards'
import { useDecksStore } from '@/stores/decks'
import { useMarkdown } from '@/composables/useMarkdown'
import { sanitizeSvg } from '@/composables/useSvgSanitizer'
import type { Card, DeckDetail } from '@/types'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { ChevronLeft, ChevronRight } from 'lucide-vue-next'
import CardDeleteDialog from '@/components/CardDeleteDialog.vue'
import { formatDate } from '@/lib/formatDate'
import { stripMarkdown } from '@/lib/utils'

const route = useRoute()
const router = useRouter()
const cardsStore = useCardsStore()
const decksStore = useDecksStore()
const { render } = useMarkdown()

const card = ref<Card | null>(null)
const loading = ref(true)
const deckCards = ref<DeckDetail | null>(null)

const deleteOpen = ref(false)
const editing = ref(false)
const front = ref('')
const back = ref('')
const editFrontSvg = ref('')
const editBackSvg = ref('')
const editSourceFile = ref('')
const editDeckIds = ref<string[]>([])
const saving = ref(false)
const error = ref('')
const resetOpen = ref(false)
const resetting = ref(false)
const resetSuccess = ref(false)
const idCopied = ref(false)

async function copyId() {
  if (!card.value) return
  await navigator.clipboard.writeText(card.value.id)
  idCopied.value = true
  setTimeout(() => idCopied.value = false, 2000)
}

const deckContext = computed(() => {
  const id = route.query.deckId as string | undefined
  if (!id) return null
  const name =
    card.value?.decks.find(d => d.id === id)?.name ??
    decksStore.decks.find(d => d.id === id)?.name
  return name ? { id, name } : null
})

const truncatedFront = computed(() => {
  if (!card.value) return ''
  const plain = stripMarkdown(card.value.front)
  return plain.length > 60 ? plain.slice(0, 60) + '…' : plain
})

const navIndex = computed(() => {
  if (!deckCards.value || !card.value) return -1
  return deckCards.value.cards.findIndex(c => c.id === card.value!.id)
})

const prevCardId = computed(() => {
  if (navIndex.value <= 0) return null
  return deckCards.value!.cards[navIndex.value - 1].id
})

const nextCardId = computed(() => {
  if (navIndex.value < 0 || !deckCards.value) return null
  if (navIndex.value >= deckCards.value.cards.length - 1) return null
  return deckCards.value.cards[navIndex.value + 1].id
})

function navigateTo(id: string) {
  if (!deckContext.value) return
  router.push(`/cards/${id}?deckId=${deckContext.value.id}`)
}

function onKeyDown(e: KeyboardEvent) {
  if (editing.value) return
  const t = e.target as HTMLElement | null
  if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable)) return
  if (e.metaKey || e.ctrlKey || e.altKey) return
  if (e.key === 'ArrowLeft' && prevCardId.value) {
    e.preventDefault()
    navigateTo(prevCardId.value)
  } else if (e.key === 'ArrowRight' && nextCardId.value) {
    e.preventDefault()
    navigateTo(nextCardId.value)
  }
}

async function loadCard(id: string) {
  loading.value = true
  try {
    card.value = await cardsStore.getCard(id)
  } catch {
    router.replace('/cards')
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await loadCard(route.params.id as string)
  decksStore.fetchDecks()
  const deckId = route.query.deckId as string | undefined
  if (deckId) {
    decksStore.getDeckDetail(deckId).then(d => deckCards.value = d).catch(() => {})
  }
  if (route.query.edit === 'true' && card.value) {
    startEdit()
  }
  window.addEventListener('keydown', onKeyDown)
})

onUnmounted(() => {
  window.removeEventListener('keydown', onKeyDown)
})

watch(() => route.params.id, async (newId, oldId) => {
  if (!newId || newId === oldId) return
  editing.value = false
  await loadCard(newId as string)
})

function startEdit() {
  if (!card.value) return
  front.value = card.value.front
  back.value = card.value.back
  editFrontSvg.value = card.value.frontSvg ?? ''
  editBackSvg.value = card.value.backSvg ?? ''
  editSourceFile.value = card.value.sourceFile ?? ''
  editDeckIds.value = card.value.decks.map(d => d.id)
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
      frontSvg: editFrontSvg.value,
      backSvg: editBackSvg.value,
      sourceFile: editSourceFile.value || null,
      deckIds: editDeckIds.value,
    })
    editing.value = false
  } catch {
    error.value = 'Failed to update card.'
  } finally {
    saving.value = false
  }
}

async function confirmReset() {
  if (!card.value) return
  resetting.value = true
  try {
    card.value = await cardsStore.resetProgress(card.value.id)
    resetOpen.value = false
    resetSuccess.value = true
    setTimeout(() => resetSuccess.value = false, 2000)
  } catch {
    error.value = 'Failed to reset progress. Please try again.'
    resetOpen.value = false
  } finally {
    resetting.value = false
  }
}

function onDeleted() {
  router.replace(deckContext.value ? `/decks/${deckContext.value.id}` : '/cards')
}
</script>

<template>
  <div v-if="loading" class="py-12 text-center text-sm text-ink-2">Loading…</div>

  <div v-else-if="card" class="space-y-6">
    <!-- Breadcrumb + deck navigation -->
    <div class="flex items-center justify-between gap-3 text-[11px] text-ink-2">
      <RouterLink v-if="deckContext" :to="`/decks/${deckContext.id}`" class="fa-back">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>
        {{ deckContext.name }}
      </RouterLink>
      <RouterLink v-else to="/cards" class="fa-back">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>
        Cards
      </RouterLink>
      <div
        v-if="deckContext && deckCards && navIndex >= 0 && deckCards.cards.length > 1"
        class="flex shrink-0 items-center gap-1"
      >
        <Button
          variant="ghost"
          size="sm"
          class="h-6 w-6 p-0"
          :disabled="!prevCardId"
          :title="'Previous card (←)'"
          @click="prevCardId && navigateTo(prevCardId)"
        >
          <ChevronLeft class="size-3.5" />
        </Button>
        <span class="fa-num px-1">{{ navIndex + 1 }} / {{ deckCards.cards.length }}</span>
        <Button
          variant="ghost"
          size="sm"
          class="h-6 w-6 p-0"
          :disabled="!nextCardId"
          :title="'Next card (→)'"
          @click="nextCardId && navigateTo(nextCardId)"
        >
          <ChevronRight class="size-3.5" />
        </Button>
      </div>
    </div>

    <!-- Header -->
    <div class="flex items-start justify-between">
      <div class="flex items-center gap-2.5">
        <h1 class="text-base font-semibold tracking-tight text-ink-0">{{ truncatedFront }}</h1>
        <Badge variant="outline" class="text-xs">{{ card.state }}</Badge>
      </div>
      <div class="flex items-center gap-2">
        <Button variant="outline" size="sm" class="h-7 text-xs" @click="copyId">
          {{ idCopied ? 'Copied!' : 'Copy ID' }}
        </Button>
        <Button v-if="!editing" variant="outline" size="sm" class="h-7 text-xs" @click="startEdit">Edit</Button>
        <Button
          v-if="!editing"
          variant="outline"
          size="sm"
          class="h-7 text-xs text-destructive hover:text-destructive"
          @click="resetOpen = true"
        >
          Reset Progress
        </Button>
        <Button
          variant="outline"
          size="sm"
          class="h-7 text-xs text-destructive hover:text-destructive"
          @click="deleteOpen = true"
        >
          Delete
        </Button>
      </div>
    </div>

    <!-- Metadata -->
    <div class="space-y-1 text-sm text-ink-1">
      <div v-if="card.sourceFile" class="flex flex-wrap gap-x-6">
        <span>Source: <span class="text-ink-0">{{ card.sourceFile }}</span></span>
      </div>
      <div v-if="card.decks.length > 0">
        Decks:
        <template v-for="(d, i) in card.decks" :key="d.id">
          <RouterLink :to="`/decks/${d.id}`" class="text-accent-text hover:underline transition-colors">{{ d.name }}</RouterLink><span v-if="i < card.decks.length - 1">, </span>
        </template>
      </div>
    </div>

    <!-- SRS Stats — open row of number + quiet label, one soft surface, no per-tile borders -->
    <div class="bg-paper-2 rounded-xl px-5 py-4 grid grid-cols-4 sm:grid-cols-7 gap-4">
      <div>
        <div class="fa-cap">State</div>
        <div class="text-sm font-semibold mt-1 text-ink-0">{{ card.state }}</div>
      </div>
      <div>
        <div class="fa-cap">Due</div>
        <div class="text-sm font-semibold mt-1 text-ink-0">{{ card.dueAt ? formatDate(card.dueAt) : '—' }}</div>
      </div>
      <div>
        <div class="fa-cap">Stability</div>
        <div class="text-sm font-semibold mt-1 text-ink-0 fa-num">{{ card.stability != null ? card.stability.toFixed(2) : '—' }}</div>
      </div>
      <div>
        <div class="fa-cap">Difficulty</div>
        <div class="text-sm font-semibold mt-1 text-ink-0 fa-num">{{ card.difficulty != null ? card.difficulty.toFixed(2) : '—' }}</div>
      </div>
      <div>
        <div class="fa-cap">Step</div>
        <div class="text-sm font-semibold mt-1 text-ink-0 fa-num">{{ card.step ?? '—' }}</div>
      </div>
      <div>
        <div class="fa-cap">Last review</div>
        <div class="text-sm font-semibold mt-1 text-ink-0">{{ card.lastReviewedAt ? formatDate(card.lastReviewedAt) : '—' }}</div>
      </div>
      <div>
        <div class="fa-cap">Created</div>
        <div class="text-sm font-semibold mt-1 text-ink-0">{{ formatDate(card.createdAt) }}</div>
      </div>
    </div>

    <div v-if="resetSuccess" class="text-sm" style="color: var(--c-good)">Progress reset.</div>

    <!-- Edit mode -->
    <div v-if="editing" class="space-y-4">
      <div class="space-y-1">
        <label class="fa-cap">Source file</label>
        <Input v-model="editSourceFile" placeholder="e.g. notes.md" class="h-8 text-sm" />
      </div>
      <div class="space-y-1">
        <label class="fa-cap">Decks</label>
        <div class="flex flex-wrap gap-2">
          <label
            v-for="d in decksStore.decks"
            :key="d.id"
            class="flex items-center gap-1.5 text-sm cursor-pointer text-ink-1"
          >
            <input
              type="checkbox"
              :checked="editDeckIds.includes(d.id)"
              class="rounded border-rule-1 accent-[var(--accent)]"
              @change="editDeckIds.includes(d.id) ? editDeckIds = editDeckIds.filter(id => id !== d.id) : editDeckIds.push(d.id)"
            />
            {{ d.name }}
          </label>
        </div>
      </div>
      <div class="space-y-1.5">
        <div class="fa-cap border-b border-rule-1 pb-1.5 mb-2">Front (question)</div>
        <textarea
          v-model="front"
          class="w-full rounded-lg border border-rule-1 bg-paper-1 px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-[var(--accent)]"
          rows="3"
        />
        <details class="mt-2">
          <summary class="cursor-pointer text-sm text-ink-2">SVG (front)</summary>
          <div class="mt-2 grid grid-cols-2 gap-2">
            <textarea
              v-model="editFrontSvg"
              class="min-h-[100px] rounded-lg border border-rule-1 bg-paper-1 px-3 py-2 text-sm fa-mono"
              placeholder="Paste SVG markup here…"
            />
            <div class="flex items-center justify-center rounded-lg border border-rule-1 bg-paper-2 p-2 min-h-[100px]">
              <div v-if="editFrontSvg" class="max-h-[200px] w-full [&>svg]:max-h-[200px] [&>svg]:w-full" v-html="sanitizeSvg(editFrontSvg)" />
              <span v-else class="text-sm text-ink-2">Preview</span>
            </div>
          </div>
          <Button v-if="editFrontSvg" variant="ghost" size="sm" class="mt-1 text-sm" @click="editFrontSvg = ''">Clear SVG</Button>
        </details>
      </div>
      <div class="space-y-1.5">
        <div class="fa-cap border-b border-rule-1 pb-1.5 mb-2">Back (answer)</div>
        <textarea
          v-model="back"
          class="w-full rounded-lg border border-rule-1 bg-paper-1 px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-[var(--accent)]"
          rows="8"
        />
        <details class="mt-2">
          <summary class="cursor-pointer text-sm text-ink-2">SVG (back)</summary>
          <div class="mt-2 grid grid-cols-2 gap-2">
            <textarea
              v-model="editBackSvg"
              class="min-h-[100px] rounded-lg border border-rule-1 bg-paper-1 px-3 py-2 text-sm fa-mono"
              placeholder="Paste SVG markup here…"
            />
            <div class="flex items-center justify-center rounded-lg border border-rule-1 bg-paper-2 p-2 min-h-[100px]">
              <div v-if="editBackSvg" class="max-h-[200px] w-full [&>svg]:max-h-[200px] [&>svg]:w-full" v-html="sanitizeSvg(editBackSvg)" />
              <span v-else class="text-sm text-ink-2">Preview</span>
            </div>
          </div>
          <Button v-if="editBackSvg" variant="ghost" size="sm" class="mt-1 text-sm" @click="editBackSvg = ''">Clear SVG</Button>
        </details>
      </div>
      <div v-if="error" class="text-sm" style="color: var(--c-again)">{{ error }}</div>
      <div class="flex gap-2">
        <Button size="sm" class="text-sm" :disabled="saving" @click="save">
          {{ saving ? 'Saving...' : 'Save' }}
        </Button>
        <Button variant="outline" size="sm" class="text-sm" @click="editing = false">Cancel</Button>
      </div>
    </div>

    <!-- View mode -->
    <div v-else class="space-y-6">
      <div>
        <div class="fa-cap border-b border-rule-1 pb-1.5 mb-3">Front</div>
        <div v-if="card.frontSvg" class="mb-3 flex justify-center rounded-lg bg-paper-2 p-4">
          <div class="max-h-[300px] w-full [&>svg]:max-h-[300px] [&>svg]:w-full" v-html="sanitizeSvg(card.frontSvg)" />
        </div>
        <div class="fa-prose max-w-none" v-html="render(card.front)" />
      </div>
      <div>
        <div class="fa-cap border-b border-rule-1 pb-1.5 mb-3">Back</div>
        <div v-if="card.backSvg" class="mb-3 flex justify-center rounded-lg bg-paper-2 p-4">
          <div class="max-h-[300px] w-full [&>svg]:max-h-[300px] [&>svg]:w-full" v-html="sanitizeSvg(card.backSvg)" />
        </div>
        <div class="fa-prose max-w-none" v-html="render(card.back)" />
      </div>
    </div>

    <!-- Delete confirmation -->
    <CardDeleteDialog v-model:open="deleteOpen" :card="card" @deleted="onDeleted" />

    <Dialog v-model:open="resetOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Reset study progress</DialogTitle>
          <DialogDescription>
            This will clear all SRS data (stability, difficulty, scheduling) and return the card to "new" state.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" @click="resetOpen = false">Cancel</Button>
          <Button variant="destructive" :disabled="resetting" @click="confirmReset">
            {{ resetting ? 'Resetting...' : 'Reset' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
