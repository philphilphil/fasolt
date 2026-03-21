<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useCardsStore } from '@/stores/cards'
import { useMarkdown } from '@/composables/useMarkdown'
import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const props = defineProps<{
  open: boolean
  fileId?: string
  sourceHeading?: string
  initialFront?: string
  initialBack?: string
  cardType: 'file' | 'section' | 'custom'
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  created: []
}>()

const cards = useCardsStore()
const { render } = useMarkdown()

const front = ref('')
const back = ref('')
const saving = ref(false)
const error = ref('')
const showPreview = ref(false)

watch(() => props.open, (isOpen) => {
  if (isOpen) {
    front.value = props.initialFront ?? ''
    back.value = props.initialBack ?? ''
    error.value = ''
    showPreview.value = false
  }
})

const isLong = computed(() => back.value.length > 2000)
const renderedFront = computed(() => render(front.value))
const renderedBack = computed(() => render(back.value))

async function save() {
  if (!front.value.trim() || !back.value.trim()) {
    error.value = 'Front and back are required.'
    return
  }
  saving.value = true
  error.value = ''
  try {
    await cards.createCard({
      fileId: props.fileId,
      sourceHeading: props.sourceHeading,
      front: front.value,
      back: back.value,
      cardType: props.cardType,
    })
    emit('update:open', false)
    emit('created')
  } catch {
    error.value = 'Failed to create card.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="max-w-2xl max-h-[85vh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>Create card</DialogTitle>
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
          <div v-else class="prose prose-sm dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="renderedFront" />
        </div>

        <div class="space-y-1">
          <label class="text-xs font-medium text-muted-foreground">Back (answer)</label>
          <textarea
            v-if="!showPreview"
            v-model="back"
            class="w-full rounded-md border border-border bg-transparent px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            rows="8"
          />
          <div v-else class="prose prose-sm dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="renderedBack" />
        </div>

        <div v-if="isLong" class="rounded-md border border-yellow-500/30 bg-yellow-500/10 px-3 py-2 text-xs text-yellow-600 dark:text-yellow-400">
          This card is quite long ({{ back.length.toLocaleString() }} chars). Consider creating cards from specific sections instead.
        </div>

        <div v-if="error" class="text-xs text-destructive">{{ error }}</div>
      </div>

      <DialogFooter class="gap-2">
        <Button variant="outline" size="sm" @click="showPreview = !showPreview">
          {{ showPreview ? 'Edit' : 'Preview' }}
        </Button>
        <Button variant="outline" size="sm" @click="emit('update:open', false)">Cancel</Button>
        <Button size="sm" :disabled="saving" @click="save">
          {{ saving ? 'Saving...' : 'Create card' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
