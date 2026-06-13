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
  sourceFile?: string
  initialFronts?: string[]
  initialBack?: string
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  created: []
}>()

const cards = useCardsStore()
const { render } = useMarkdown()

const fronts = ref<string[]>([])
const back = ref('')
const sourceFile = ref('')
const saving = ref(false)
const error = ref('')
const showPreview = ref(false)

watch(() => props.open, (isOpen) => {
  if (isOpen) {
    fronts.value = props.initialFronts?.length ? [...props.initialFronts] : ['']
    back.value = props.initialBack ?? ''
    sourceFile.value = props.sourceFile ?? ''
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
        sourceFile: sourceFile.value.trim() || undefined,
        front,
        back: back.value,
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
        <!-- Source metadata -->
        <div class="space-y-1.5">
          <label class="fa-cap block">Source file</label>
          <input
            v-model="sourceFile"
            type="text"
            placeholder="e.g. distributed-systems.md"
            class="fa-field"
          />
        </div>

        <!-- Single front -->
        <div v-if="isSingle" class="space-y-1.5">
          <label class="fa-cap block">Front (question)</label>
          <textarea
            v-if="!showPreview"
            v-model="fronts[0]"
            class="fa-field"
            rows="2"
          />
          <div v-else class="fa-preview fa-prose" v-html="render(fronts[0])" />
        </div>

        <!-- Multiple fronts -->
        <div v-else class="space-y-2">
          <label class="fa-cap block">Fronts (one card per question)</label>
          <div v-for="(front, idx) in fronts" :key="idx" class="flex items-start gap-2">
            <textarea
              v-if="!showPreview"
              v-model="fronts[idx]"
              class="fa-field flex-1"
              rows="1"
            />
            <div v-else class="fa-preview fa-prose flex-1" v-html="render(front)" />
            <Button
              variant="ghost"
              size="sm"
              class="h-7 w-7 p-0 text-ink-2 hover:text-destructive shrink-0"
              @click="removeFront(idx)"
            >
              &times;
            </Button>
          </div>
        </div>

        <!-- Back -->
        <div class="space-y-1.5">
          <label class="fa-cap block">Back (answer) {{ !isSingle ? '— shared by all cards' : '' }}</label>
          <textarea
            v-if="!showPreview"
            v-model="back"
            class="fa-field"
            rows="8"
          />
          <div v-else class="fa-preview fa-prose" v-html="renderedBack" />
        </div>

        <div v-if="isLong" class="fa-note">
          This card is quite long ({{ back.length.toLocaleString() }} chars). Consider creating cards from specific sections instead.
        </div>

        <div v-if="error" class="fa-error">{{ error }}</div>
      </div>

      <DialogFooter class="gap-2">
        <button class="fa-btn fa-btn-ghost" @click="showPreview = !showPreview">
          {{ showPreview ? 'Edit' : 'Preview' }}
        </button>
        <button class="fa-btn" @click="emit('update:open', false)">Cancel</button>
        <button class="fa-btn fa-btn-primary" :disabled="saving" @click="save">
          {{ saving ? 'Saving…' : cardCount > 1 ? `Create ${cardCount} cards` : 'Create card' }}
        </button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>

<style scoped>
/* Text inputs / textareas — flat, hairline, accent ring on focus */
.fa-field {
  width: 100%;
  border-radius: 9px;
  border: 1px solid var(--rule-1);
  background: var(--paper-1);
  padding: 8px 11px;
  font-size: 13px;
  font-family: inherit;
  color: var(--ink-0);
  transition: border-color .12s;
}
.fa-field::placeholder { color: var(--ink-3); }
.fa-field:focus { outline: none; border-color: var(--accent); }
textarea.fa-field { resize: vertical; }

/* Rendered markdown preview surface */
.fa-preview {
  border: 1px solid var(--rule-1);
  border-radius: 9px;
  padding: 10px 12px;
  background: var(--paper-2);
}

/* Soft advisory note (long-card warning) — quiet, no glow */
.fa-note {
  border: 1px solid var(--rule-1);
  border-radius: 9px;
  padding: 8px 11px;
  font-size: 12px;
  color: var(--ink-1);
  background: var(--paper-2);
}

.fa-error {
  font-size: 12.5px;
  color: var(--c-again);
}
</style>
