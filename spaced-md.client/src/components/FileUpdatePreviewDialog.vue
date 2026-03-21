<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { FileUpdatePreview } from '@/types'
import { useFilesStore } from '@/stores/files'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const props = defineProps<{
  open: boolean
  preview: FileUpdatePreview | null
  file: File | null
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  confirmed: []
}>()

const files = useFilesStore()
const saving = ref(false)
const error = ref('')

const deleteSet = ref<Set<string>>(new Set())

watch(() => props.preview, () => {
  deleteSet.value = new Set()
  error.value = ''
})

function toggleDelete(cardId: string) {
  if (deleteSet.value.has(cardId)) {
    deleteSet.value.delete(cardId)
  } else {
    deleteSet.value.add(cardId)
  }
  deleteSet.value = new Set(deleteSet.value)
}

const hasChanges = computed(() => {
  if (!props.preview) return false
  return props.preview.updatedCards.length > 0
    || props.preview.orphanedCards.length > 0
    || props.preview.newSections.length > 0
})

async function confirm() {
  if (!props.preview || !props.file) return
  saving.value = true
  error.value = ''
  try {
    await files.confirmUpdate(
      props.preview.fileId,
      props.file,
      Array.from(deleteSet.value),
    )
    emit('update:open', false)
    emit('confirmed')
  } catch {
    error.value = 'Failed to update file.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="max-w-lg max-h-[85vh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>Updating {{ preview?.fileName }}</DialogTitle>
      </DialogHeader>

      <div v-if="preview" class="space-y-4 text-sm">
        <!-- No changes -->
        <div v-if="!hasChanges" class="text-muted-foreground">
          No card changes detected. File content will be replaced.
        </div>

        <!-- Updated cards -->
        <div v-if="preview.updatedCards.length > 0">
          <div class="text-xs font-medium text-muted-foreground mb-2">
            Cards updated ({{ preview.updatedCards.length }})
          </div>
          <div class="space-y-1">
            <div v-for="card in preview.updatedCards" :key="card.cardId" class="flex items-center gap-2 rounded border border-border px-3 py-2 text-xs">
              <Badge variant="secondary" class="text-[10px] shrink-0">changed</Badge>
              <span class="truncate">{{ card.front }}</span>
            </div>
          </div>
        </div>

        <!-- Orphaned cards -->
        <div v-if="preview.orphanedCards.length > 0">
          <div class="text-xs font-medium text-muted-foreground mb-2">
            Section removed ({{ preview.orphanedCards.length }})
          </div>
          <div class="space-y-1">
            <div v-for="card in preview.orphanedCards" :key="card.cardId" class="flex items-center justify-between rounded border border-border px-3 py-2 text-xs">
              <div class="flex items-center gap-2 min-w-0">
                <Badge variant="secondary" class="text-[10px] shrink-0">orphan</Badge>
                <span class="truncate">{{ card.front }}</span>
              </div>
              <Button
                :variant="deleteSet.has(card.cardId) ? 'destructive' : 'outline'"
                size="sm"
                class="h-6 text-[10px] shrink-0"
                @click="toggleDelete(card.cardId)"
              >
                {{ deleteSet.has(card.cardId) ? 'Delete' : 'Keep' }}
              </Button>
            </div>
          </div>
        </div>

        <!-- Unchanged -->
        <div v-if="preview.unchangedCount > 0" class="text-xs text-muted-foreground">
          {{ preview.unchangedCount }} card{{ preview.unchangedCount === 1 ? '' : 's' }} unchanged
        </div>

        <!-- New sections -->
        <div v-if="preview.newSections.length > 0">
          <div class="text-xs font-medium text-muted-foreground mb-2">
            New sections ({{ preview.newSections.length }})
          </div>
          <div class="space-y-1">
            <div v-for="section in preview.newSections" :key="section.heading" class="flex items-center gap-2 rounded border border-border px-3 py-2 text-xs text-muted-foreground">
              <Badge variant="secondary" class="text-[10px]">new</Badge>
              <span>{{ section.heading }}</span>
              <span v-if="section.hasMarkers" class="text-[10px]">(has questions)</span>
            </div>
          </div>
          <div class="text-[10px] text-muted-foreground mt-1">
            Create cards from new sections after updating.
          </div>
        </div>

        <div v-if="error" class="text-xs text-destructive">{{ error }}</div>
      </div>

      <DialogFooter>
        <Button variant="outline" size="sm" @click="emit('update:open', false)">Cancel</Button>
        <Button size="sm" :disabled="saving" @click="confirm">
          {{ saving ? 'Updating...' : 'Confirm update' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
