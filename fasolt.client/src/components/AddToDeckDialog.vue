<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useDecksStore } from '@/stores/decks'
import { deckColor } from '@/lib/utils'

const props = defineProps<{
  open: boolean
  cardIds: string[]
}>()
const emit = defineEmits<{
  'update:open': [value: boolean]
  added: [info: { deckId: string; count: number }]
}>()

const decks = useDecksStore()
const query = ref('')
const submitting = ref(false)
const errorMsg = ref('')

watch(() => props.open, (next) => {
  if (next) {
    query.value = ''
    errorMsg.value = ''
    if (decks.decks.length === 0) decks.fetchDecks()
  }
})

const filteredDecks = computed(() => {
  const q = query.value.trim().toLowerCase()
  const all = [...decks.decks].sort((a, b) => a.name.localeCompare(b.name))
  if (!q) return all
  return all.filter(d => d.name.toLowerCase().includes(q))
})

async function moveTo(deckId: string) {
  if (submitting.value) return
  submitting.value = true
  errorMsg.value = ''
  try {
    await decks.addCards(deckId, props.cardIds)
    emit('added', { deckId, count: props.cardIds.length })
    emit('update:open', false)
  } catch {
    errorMsg.value = 'Could not add cards to deck. Please try again.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="(v: boolean) => $emit('update:open', v)">
    <DialogContent>
      <DialogHeader>
        <DialogTitle>Add to deck</DialogTitle>
        <DialogDescription>
          Add <strong>{{ cardIds.length }} card{{ cardIds.length === 1 ? '' : 's' }}</strong> to a deck.
          Cards keep their existing deck assignments.
        </DialogDescription>
      </DialogHeader>

      <div class="move-search">
        <Input v-model="query" placeholder="Search decks…" autofocus />
      </div>

      <div class="move-list">
        <button
          v-for="d in filteredDecks"
          :key="d.id"
          class="move-row"
          :disabled="submitting"
          @click="moveTo(d.id)"
        >
          <span class="fa-tag" :style="{ background: deckColor(d.id) }" />
          <div class="move-row-text">
            <div class="move-row-name">{{ d.name }}</div>
            <div class="fa-mono move-row-sub">
              {{ d.cardCount }} card{{ d.cardCount === 1 ? '' : 's' }}<template v-if="d.isSuspended"> · suspended</template>
            </div>
          </div>
          <span class="move-row-cta fa-mono">add ›</span>
        </button>

        <div v-if="filteredDecks.length === 0" class="move-empty">
          No decks match "{{ query }}".
        </div>
      </div>

      <div v-if="errorMsg" class="move-error">{{ errorMsg }}</div>

      <DialogFooter>
        <Button variant="outline" :disabled="submitting" @click="$emit('update:open', false)">Cancel</Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>

<style scoped>
.move-search { margin-bottom: 2px; }
.move-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-height: 320px;
  overflow-y: auto;
  margin: 4px -4px;
  padding: 4px;
}
.move-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  border: 1px solid var(--rule-1);
  background: var(--paper-1);
  border-radius: 8px;
  text-align: left;
  cursor: pointer;
  font: inherit;
  color: inherit;
  transition: border-color .12s, background .12s;
}
.move-row:hover:not(:disabled) {
  border-color: var(--accent);
  background: var(--paper-2);
}
.move-row:disabled { opacity: 0.5; cursor: not-allowed; }
.move-row-text { flex: 1; min-width: 0; }
.move-row-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--ink-0);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.move-row-sub {
  font-size: 11px;
  color: var(--ink-2);
  margin-top: 2px;
}
.move-row-cta {
  font-size: 11px;
  color: var(--ink-3);
  transition: color .12s;
}
.move-row:hover:not(:disabled) .move-row-cta { color: var(--accent); }

.move-empty {
  padding: 18px 8px;
  text-align: center;
  font-size: 13px;
  color: var(--ink-2);
}
.move-error {
  font-size: 12px;
  color: var(--c-again);
  padding: 6px 0;
}
</style>
