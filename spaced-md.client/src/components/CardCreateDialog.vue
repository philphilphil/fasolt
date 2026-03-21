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
  initialFronts?: string[]
  initialBack?: string
  cardType: 'file' | 'section' | 'custom'
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  created: []
}>()

const cards = useCardsStore()
const { render } = useMarkdown()

const fronts = ref<string[]>([])
const back = ref('')
const saving = ref(false)
const error = ref('')
const showPreview = ref(false)

watch(() => props.open, (isOpen) => {
  if (isOpen) {
    fronts.value = props.initialFronts?.length ? [...props.initialFronts] : ['']
    back.value = props.initialBack ?? ''
    error.value = ''
    showPreview.value = false
  }
})

const isSingle = computed(() => fronts.value.length === 1)
const isLong = computed(() => back.value.length > 2000)
const renderedBack = computed(() => render(back.value))
const cardCount = computed(() => fronts.value.filter(f => f.trim()).length)

function removeFront(idx: number) {
  fronts.value.splice(idx, 1)
}

async function save() {
  const validFronts = fronts.value.filter(f => f.trim())
  if (!validFronts.length || !back.value.trim()) {
    error.value = 'At least one front and a back are required.'
    return
  }
  saving.value = true
  error.value = ''
  try {
    for (const front of validFronts) {
      await cards.createCard({
        fileId: props.fileId,
        sourceHeading: props.sourceHeading,
        front,
        back: back.value,
        cardType: props.cardType,
      })
    }
    emit('update:open', false)
    emit('created')
  } catch {
    error.value = 'Failed to create card(s).'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="max-w-2xl max-h-[85vh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>{{ cardCount > 1 ? `Create ${cardCount} cards` : 'Create card' }}</DialogTitle>
      </DialogHeader>

      <div class="space-y-4">
        <!-- Single front -->
        <div v-if="isSingle" class="space-y-1">
          <label class="text-xs font-medium text-muted-foreground">Front (question)</label>
          <textarea
            v-if="!showPreview"
            v-model="fronts[0]"
            class="w-full rounded-md border border-border bg-transparent px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            rows="2"
          />
          <div v-else class="prose prose-sm dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="render(fronts[0])" />
        </div>

        <!-- Multiple fronts -->
        <div v-else class="space-y-2">
          <label class="text-xs font-medium text-muted-foreground">Fronts (one card per question)</label>
          <div v-for="(front, idx) in fronts" :key="idx" class="flex items-start gap-2">
            <textarea
              v-if="!showPreview"
              v-model="fronts[idx]"
              class="flex-1 rounded-md border border-border bg-transparent px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              rows="1"
            />
            <div v-else class="flex-1 prose prose-sm dark:prose-invert rounded-md border border-border p-2 text-sm" v-html="render(front)" />
            <Button
              variant="ghost"
              size="sm"
              class="h-7 w-7 p-0 text-muted-foreground hover:text-destructive shrink-0"
              @click="removeFront(idx)"
            >
              &times;
            </Button>
          </div>
        </div>

        <!-- Back -->
        <div class="space-y-1">
          <label class="text-xs font-medium text-muted-foreground">Back (answer) {{ !isSingle ? '— shared by all cards' : '' }}</label>
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
          {{ saving ? 'Saving...' : cardCount > 1 ? `Create ${cardCount} cards` : 'Create card' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
