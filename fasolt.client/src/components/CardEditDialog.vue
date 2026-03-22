<script setup lang="ts">
import { ref, watch } from 'vue'
import { useCardsStore } from '@/stores/cards'
import { useMarkdown } from '@/composables/useMarkdown'
import type { Card } from '@/types'
import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const props = defineProps<{
  open: boolean
  card: Card | null
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  updated: []
}>()

const cards = useCardsStore()
const { render } = useMarkdown()

const front = ref('')
const back = ref('')
const saving = ref(false)
const error = ref('')
const showPreview = ref(false)

watch(() => props.open, (isOpen) => {
  if (isOpen && props.card) {
    front.value = props.card.front
    back.value = props.card.back
    error.value = ''
    showPreview.value = false
  }
})

async function save() {
  if (!props.card || !front.value.trim() || !back.value.trim()) {
    error.value = 'Front and back are required.'
    return
  }
  saving.value = true
  error.value = ''
  try {
    await cards.updateCard(props.card.id, { front: front.value, back: back.value })
    emit('update:open', false)
    emit('updated')
  } catch {
    error.value = 'Failed to update card.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="max-w-2xl max-h-[85vh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>Edit card</DialogTitle>
      </DialogHeader>

      <div class="space-y-4">
        <div class="space-y-1">
          <label class="text-xs font-medium text-muted-foreground">Front (question)</label>
          <textarea
            v-if="!showPreview"
            v-model="front"
            class="w-full rounded-md border border-border bg-transparent px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            rows="2"
          />
          <div v-else class="prose prose-sm dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="render(front)" />
        </div>

        <div class="space-y-1">
          <label class="text-xs font-medium text-muted-foreground">Back (answer)</label>
          <textarea
            v-if="!showPreview"
            v-model="back"
            class="w-full rounded-md border border-border bg-transparent px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            rows="8"
          />
          <div v-else class="prose prose-sm dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="render(back)" />
        </div>

        <div v-if="error" class="text-xs text-destructive">{{ error }}</div>
      </div>

      <DialogFooter class="gap-2">
        <Button variant="outline" size="sm" @click="showPreview = !showPreview">
          {{ showPreview ? 'Edit' : 'Preview' }}
        </Button>
        <Button variant="outline" size="sm" @click="emit('update:open', false)">Cancel</Button>
        <Button size="sm" :disabled="saving" @click="save">
          {{ saving ? 'Saving...' : 'Save' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
