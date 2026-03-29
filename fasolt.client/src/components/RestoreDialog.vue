<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useSnapshotsStore } from '@/stores/snapshots'
import type { SnapshotDiff } from '@/types'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog'

const props = defineProps<{
  snapshotId: string
  deckId: string
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void
  (e: 'restored'): void
}>()

const snapshotsStore = useSnapshotsStore()

const diff = ref<SnapshotDiff | null>(null)
const loadingDiff = ref(false)
const restoring = ref(false)
const error = ref('')

const selectedDeleted = ref<Set<string>>(new Set())
const selectedModified = ref<Set<string>>(new Set())

watch(() => props.open, async (isOpen) => {
  if (!isOpen) return
  error.value = ''
  diff.value = null
  loadingDiff.value = true
  selectedDeleted.value = new Set()
  selectedModified.value = new Set()
  try {
    diff.value = await snapshotsStore.getDiff(props.snapshotId)
    // Deleted cards are checked by default
    selectedDeleted.value = new Set(diff.value.deleted.map(c => c.cardId))
  } catch {
    error.value = 'Failed to load snapshot diff.'
  } finally {
    loadingDiff.value = false
  }
}, { immediate: true })

const selectedCount = computed(() => selectedDeleted.value.size + selectedModified.value.size)

const isEmpty = computed(() => {
  if (!diff.value) return true
  return diff.value.deleted.length === 0 && diff.value.modified.length === 0 && diff.value.added.length === 0
})

function toggleDeleted(cardId: string, checked: boolean) {
  const s = new Set(selectedDeleted.value)
  if (checked) s.add(cardId); else s.delete(cardId)
  selectedDeleted.value = s
}

function toggleModified(cardId: string, checked: boolean) {
  const s = new Set(selectedModified.value)
  if (checked) s.add(cardId); else s.delete(cardId)
  selectedModified.value = s
}

async function handleRestore() {
  restoring.value = true
  error.value = ''
  try {
    await snapshotsStore.restore(
      props.snapshotId,
      [...selectedDeleted.value],
      [...selectedModified.value],
    )
    emit('restored')
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
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="max-w-2xl max-h-[80vh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>Restore snapshot</DialogTitle>
        <DialogDescription>
          Select which changes to revert. This will restore selected cards to their snapshot state.
        </DialogDescription>
      </DialogHeader>

      <!-- Loading -->
      <div v-if="loadingDiff" class="py-8 text-center text-xs text-muted-foreground">
        Loading diff...
      </div>

      <!-- Error -->
      <div v-else-if="error && !diff" class="py-8 text-center text-xs text-destructive">
        {{ error }}
      </div>

      <!-- Empty diff -->
      <div v-else-if="isEmpty" class="py-8 text-center text-xs text-muted-foreground">
        No differences found. The deck matches this snapshot.
      </div>

      <!-- Diff sections -->
      <div v-else-if="diff" class="space-y-5">
        <!-- Deleted since snapshot -->
        <div v-if="diff.deleted.length > 0">
          <div class="flex items-center gap-2 mb-2">
            <Badge variant="destructive" class="text-[10px]">Deleted</Badge>
            <span class="text-[11px] text-muted-foreground">{{ diff.deleted.length }} cards removed since snapshot</span>
          </div>
          <div class="space-y-1.5">
            <div
              v-for="card in diff.deleted"
              :key="card.cardId"
              class="flex items-start gap-2.5 rounded border border-border px-3 py-2"
            >
              <Checkbox
                :checked="selectedDeleted.has(card.cardId)"
                class="mt-0.5"
                @update:checked="toggleDeleted(card.cardId, $event as boolean)"
              />
              <div class="min-w-0 flex-1">
                <div class="text-xs font-medium truncate">{{ card.front }}</div>
                <div class="text-[11px] text-muted-foreground truncate">{{ card.back }}</div>
                <div class="flex gap-3 mt-0.5 text-[10px] text-muted-foreground">
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
          <div class="flex items-center gap-2 mb-2">
            <Badge class="text-[10px] bg-amber-500/15 text-amber-500 border-amber-500/25">Modified</Badge>
            <span class="text-[11px] text-muted-foreground">{{ diff.modified.length }} cards changed since snapshot</span>
          </div>
          <div class="space-y-1.5">
            <div
              v-for="card in diff.modified"
              :key="card.cardId"
              class="flex items-start gap-2.5 rounded border border-border px-3 py-2"
            >
              <Checkbox
                :checked="selectedModified.has(card.cardId)"
                class="mt-0.5"
                @update:checked="toggleModified(card.cardId, $event as boolean)"
              />
              <div class="min-w-0 flex-1">
                <div v-if="card.hasContentChanges" class="text-xs space-y-0.5">
                  <div>
                    <span class="text-muted-foreground">front: </span>
                    <span class="line-through text-muted-foreground">{{ card.front }}</span>
                    <span class="ml-1">{{ card.currentFront }}</span>
                  </div>
                  <div>
                    <span class="text-muted-foreground">back: </span>
                    <span class="line-through text-muted-foreground">{{ card.back }}</span>
                    <span class="ml-1">{{ card.currentBack }}</span>
                  </div>
                </div>
                <div v-else class="text-xs font-medium truncate">{{ card.currentFront }}</div>
                <div v-if="card.hasFsrsChanges" class="flex gap-3 mt-0.5 text-[10px] text-muted-foreground">
                  <span>stability: {{ formatStability(card.snapshotStability) }} -> {{ formatStability(card.currentStability) }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Added since snapshot -->
        <div v-if="diff.added.length > 0">
          <div class="flex items-center gap-2 mb-2">
            <Badge class="text-[10px] bg-green-500/15 text-green-500 border-green-500/25">Added</Badge>
            <span class="text-[11px] text-muted-foreground">{{ diff.added.length }} cards added since snapshot</span>
          </div>
          <div class="space-y-1.5">
            <div
              v-for="card in diff.added"
              :key="card.cardId"
              class="rounded border border-border px-3 py-2"
            >
              <div class="text-xs font-medium truncate">{{ card.front }}</div>
              <div class="text-[11px] text-muted-foreground truncate">{{ card.back }}</div>
            </div>
          </div>
          <div class="text-[10px] text-muted-foreground mt-1.5">These cards are unaffected by restore.</div>
        </div>
      </div>

      <!-- Error on restore -->
      <div v-if="error && diff" class="text-xs text-destructive">{{ error }}</div>

      <!-- Footer -->
      <DialogFooter v-if="diff && !isEmpty" class="gap-2">
        <div class="flex-1 text-xs text-muted-foreground">
          {{ selectedCount }} card{{ selectedCount !== 1 ? 's' : '' }} selected
        </div>
        <Button variant="outline" size="sm" @click="emit('update:open', false)">Cancel</Button>
        <Button size="sm" :disabled="selectedCount === 0 || restoring" @click="handleRestore">
          {{ restoring ? 'Restoring...' : 'Restore selected' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
