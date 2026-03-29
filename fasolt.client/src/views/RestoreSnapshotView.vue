<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { useSnapshotsStore } from '@/stores/snapshots'
import { useDecksStore } from '@/stores/decks'
import type { SnapshotDiff, DeckDetail } from '@/types'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Checkbox } from '@/components/ui/checkbox'

const route = useRoute()
const router = useRouter()
const snapshotsStore = useSnapshotsStore()
const decksStore = useDecksStore()

const deckId = route.params.id as string
const snapshotId = route.params.snapshotId as string

const deck = ref<DeckDetail | null>(null)
const diff = ref<SnapshotDiff | null>(null)
const loading = ref(true)
const restoring = ref(false)
const error = ref('')

const selectedDeleted = ref<string[]>([])
const selectedModified = ref<string[]>([])

onMounted(async () => {
  try {
    const [d, diffResult] = await Promise.all([
      decksStore.getDeckDetail(deckId),
      snapshotsStore.getDiff(snapshotId),
    ])
    deck.value = d
    diff.value = diffResult
    selectedDeleted.value = diffResult.deleted.map(c => c.cardId)
  } catch {
    error.value = 'Failed to load snapshot diff.'
  } finally {
    loading.value = false
  }
})

const selectedCount = computed(() => selectedDeleted.value.length + selectedModified.value.length)

const isEmpty = computed(() => {
  if (!diff.value) return true
  return diff.value.deleted.length === 0 && diff.value.modified.length === 0 && diff.value.added.length === 0
})

function toggleDeleted(cardId: string, checked: boolean) {
  if (checked) {
    selectedDeleted.value = [...selectedDeleted.value, cardId]
  } else {
    selectedDeleted.value = selectedDeleted.value.filter(id => id !== cardId)
  }
}

function toggleModified(cardId: string, checked: boolean) {
  if (checked) {
    selectedModified.value = [...selectedModified.value, cardId]
  } else {
    selectedModified.value = selectedModified.value.filter(id => id !== cardId)
  }
}

const allSelected = computed(() => {
  if (!diff.value) return false
  return selectedDeleted.value.length === diff.value.deleted.length
    && selectedModified.value.length === diff.value.modified.length
})

function toggleAll() {
  if (!diff.value) return
  if (allSelected.value) {
    selectedDeleted.value = []
    selectedModified.value = []
  } else {
    selectedDeleted.value = diff.value.deleted.map(c => c.cardId)
    selectedModified.value = diff.value.modified.map(c => c.cardId)
  }
}

async function handleRestore() {
  restoring.value = true
  error.value = ''
  try {
    await snapshotsStore.restore(
      snapshotId,
      selectedDeleted.value,
      selectedModified.value,
    )
    router.push(`/decks/${deckId}`)
  } catch {
    error.value = 'Restore failed. Please try again.'
  } finally {
    restoring.value = false
  }
}

function formatStability(val: number | null) {
  if (val == null) return 'n/a'
  return val.toFixed(1) + 'd'
}

function formatDue(iso: string | null) {
  if (!iso) return 'n/a'
  const d = new Date(iso)
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}
</script>

<template>
  <div v-if="loading" class="py-12 text-center text-xs text-muted-foreground">Loading...</div>

  <div v-else class="space-y-6">
    <!-- Breadcrumb -->
    <div class="text-[11px] text-muted-foreground">
      <RouterLink to="/decks" class="hover:text-foreground transition-colors">Decks</RouterLink>
      <span class="mx-1.5">/</span>
      <RouterLink :to="`/decks/${deckId}`" class="hover:text-foreground transition-colors">{{ deck?.name }}</RouterLink>
      <span class="mx-1.5">/</span>
      <RouterLink :to="`/decks/${deckId}/snapshots`" class="hover:text-foreground transition-colors">Snapshots</RouterLink>
      <span class="mx-1.5">/</span>
      <span class="text-foreground">Restore</span>
    </div>

    <!-- Header -->
    <div class="flex items-start justify-between">
      <div>
        <h1 class="text-xl font-bold tracking-tight">Restore snapshot</h1>
        <p class="text-sm text-muted-foreground mt-1">Select which changes to revert. This will restore selected cards to their snapshot state.</p>
      </div>
      <Button variant="outline" size="sm" class="text-xs" @click="router.push(`/decks/${deckId}/snapshots`)">
        Back to snapshots
      </Button>
    </div>

    <!-- Error -->
    <div v-if="error && !diff" class="py-8 text-center text-xs text-destructive">
      {{ error }}
    </div>

    <!-- Empty diff -->
    <div v-else-if="isEmpty" class="py-12 text-center text-xs text-muted-foreground">
      No differences found. The deck matches this snapshot.
    </div>

    <!-- Diff sections -->
    <div v-else-if="diff" class="space-y-6">
      <!-- Deleted since snapshot -->
      <div v-if="diff.deleted.length > 0">
        <div class="flex items-center gap-2 mb-3">
          <Badge variant="destructive" class="text-[10px]">Deleted</Badge>
          <span class="text-xs text-muted-foreground">{{ diff.deleted.length }} card{{ diff.deleted.length !== 1 ? 's' : '' }} removed since snapshot</span>
        </div>
        <div class="space-y-2">
          <div
            v-for="card in diff.deleted"
            :key="card.cardId"
            class="flex items-start gap-3 rounded-lg border border-border px-4 py-3"
          >
            <Checkbox
              :checked="selectedDeleted.includes(card.cardId)"
              class="mt-0.5"
              @update:checked="toggleDeleted(card.cardId, $event as boolean)"
            />
            <div class="min-w-0 flex-1">
              <div class="text-sm font-medium">{{ card.front }}</div>
              <div class="text-xs text-muted-foreground mt-0.5">{{ card.back }}</div>
              <div class="flex gap-4 mt-1 text-[11px] text-muted-foreground">
                <span v-if="card.sourceFile">{{ card.sourceFile }}</span>
                <span>stability: {{ formatStability(card.stability) }}</span>
                <span>due: {{ formatDue(card.dueAt) }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Modified since snapshot -->
      <div v-if="diff.modified.length > 0">
        <div class="flex items-center gap-2 mb-3">
          <Badge class="text-[10px] bg-amber-500/15 text-amber-500 border-amber-500/25">Modified</Badge>
          <span class="text-xs text-muted-foreground">{{ diff.modified.length }} card{{ diff.modified.length !== 1 ? 's' : '' }} changed since snapshot</span>
        </div>
        <div class="space-y-2">
          <div
            v-for="card in diff.modified"
            :key="card.cardId"
            class="flex items-start gap-3 rounded-lg border border-border px-4 py-3"
          >
            <Checkbox
              :checked="selectedModified.includes(card.cardId)"
              class="mt-0.5"
              @update:checked="toggleModified(card.cardId, $event as boolean)"
            />
            <div class="min-w-0 flex-1">
              <div v-if="card.hasContentChanges" class="text-sm space-y-1">
                <div v-if="card.front !== card.currentFront">
                  <span class="text-xs text-muted-foreground">front: </span>
                  <span class="line-through text-muted-foreground">{{ card.front }}</span>
                  <span class="ml-1.5">{{ card.currentFront }}</span>
                </div>
                <div v-if="card.back !== card.currentBack">
                  <span class="text-xs text-muted-foreground">back: </span>
                  <span class="line-through text-muted-foreground">{{ card.back }}</span>
                  <span class="ml-1.5">{{ card.currentBack }}</span>
                </div>
              </div>
              <div v-else class="text-sm font-medium">{{ card.currentFront }}</div>
              <div v-if="card.hasFsrsChanges" class="flex gap-4 mt-1 text-[11px] text-muted-foreground">
                <span>stability: {{ formatStability(card.snapshotStability) }} → {{ formatStability(card.currentStability) }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Added since snapshot -->
      <div v-if="diff.added.length > 0">
        <div class="flex items-center gap-2 mb-3">
          <Badge class="text-[10px] bg-green-500/15 text-green-500 border-green-500/25">Added</Badge>
          <span class="text-xs text-muted-foreground">{{ diff.added.length }} card{{ diff.added.length !== 1 ? 's' : '' }} added since snapshot</span>
        </div>
        <div class="space-y-2">
          <div
            v-for="card in diff.added"
            :key="card.cardId"
            class="rounded-lg border border-border px-4 py-3"
          >
            <div class="text-sm font-medium">{{ card.front }}</div>
            <div class="text-xs text-muted-foreground mt-0.5">{{ card.back }}</div>
          </div>
        </div>
        <div class="text-[11px] text-muted-foreground mt-2">These cards are unaffected by restore.</div>
      </div>
    </div>

    <!-- Error on restore -->
    <div v-if="error && diff" class="text-xs text-destructive">{{ error }}</div>

    <!-- Sticky footer -->
    <div v-if="diff && !isEmpty" class="sticky bottom-0 bg-background border-t border-border -mx-4 px-4 py-3 flex items-center gap-3">
      <Button variant="ghost" size="sm" class="text-xs" @click="toggleAll">
        {{ allSelected ? 'Deselect all' : 'Select all' }}
      </Button>
      <div class="flex-1 text-xs text-muted-foreground">
        {{ selectedCount }} card{{ selectedCount !== 1 ? 's' : '' }} selected
      </div>
      <Button variant="outline" size="sm" @click="router.push(`/decks/${deckId}/snapshots`)">Cancel</Button>
      <Button size="sm" :disabled="selectedCount === 0 || restoring" @click="handleRestore">
        {{ restoring ? 'Restoring...' : 'Restore selected' }}
      </Button>
    </div>
  </div>
</template>
