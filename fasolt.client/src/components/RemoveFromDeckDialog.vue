<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { useDecksStore } from '@/stores/decks'
import { deckColor } from '@/lib/utils'
import type { Card } from '@/types'

const props = defineProps<{
  open: boolean
  cards: Card[]
}>()
const emit = defineEmits<{
  'update:open': [value: boolean]
  removed: [info: { deckId: string; count: number }]
}>()

const decks = useDecksStore()
const submitting = ref(false)
const errorMsg = ref('')

watch(() => props.open, (next) => {
  if (next) {
    errorMsg.value = ''
    if (decks.decks.length === 0) decks.fetchDecks()
  }
})

// Decks that any selected card is in, with the count of selected cards in each.
const deckRows = computed(() => {
  const counts = new Map<string, { id: string; name: string; selectedCount: number; cardIds: string[] }>()
  for (const card of props.cards) {
    for (const d of card.decks) {
      const existing = counts.get(d.id)
      if (existing) {
        existing.selectedCount++
        existing.cardIds.push(card.id)
      } else {
        counts.set(d.id, { id: d.id, name: d.name, selectedCount: 1, cardIds: [card.id] })
      }
    }
  }
  return [...counts.values()].sort((a, b) => b.selectedCount - a.selectedCount || a.name.localeCompare(b.name))
})

async function removeFrom(deckId: string, cardIds: string[]) {
  if (submitting.value) return
  submitting.value = true
  errorMsg.value = ''
  try {
    const removed = await decks.removeCards(deckId, cardIds)
    emit('removed', { deckId, count: removed })
    emit('update:open', false)
  } catch {
    errorMsg.value = 'Could not remove cards from deck. Please try again.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="(v: boolean) => $emit('update:open', v)">
    <DialogContent>
      <DialogHeader>
        <DialogTitle>Remove from deck</DialogTitle>
        <DialogDescription>
          Pick which deck to remove the selected card{{ cards.length === 1 ? '' : 's' }} from.
          The card{{ cards.length === 1 ? '' : 's' }} stay{{ cards.length === 1 ? 's' : '' }} in your library.
        </DialogDescription>
      </DialogHeader>

      <div class="remove-list">
        <button
          v-for="row in deckRows"
          :key="row.id"
          class="remove-row"
          :disabled="submitting"
          @click="removeFrom(row.id, row.cardIds)"
        >
          <span class="fa-tag" :style="{ background: deckColor(row.id) }" />
          <div class="remove-row-text">
            <div class="remove-row-name">{{ row.name }}</div>
            <div class="fa-mono remove-row-sub">
              {{ row.selectedCount }} of selection {{ row.selectedCount === 1 ? 'is' : 'are' }} in this deck
            </div>
          </div>
          <span class="remove-row-cta fa-mono">remove ›</span>
        </button>

        <div v-if="deckRows.length === 0" class="remove-empty">
          None of the selected cards are in any deck.
        </div>
      </div>

      <div v-if="errorMsg" class="remove-error">{{ errorMsg }}</div>

      <DialogFooter>
        <Button variant="outline" :disabled="submitting" @click="$emit('update:open', false)">Cancel</Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>

<style scoped>
.remove-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-height: 320px;
  overflow-y: auto;
  margin: 4px -4px;
  padding: 4px;
}
.remove-row {
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
.remove-row:hover:not(:disabled) {
  border-color: var(--c-again);
  background: var(--paper-2);
}
.remove-row:disabled { opacity: 0.5; cursor: not-allowed; }
.remove-row-text { flex: 1; min-width: 0; }
.remove-row-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--ink-0);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.remove-row-sub {
  font-size: 11px;
  color: var(--ink-2);
  margin-top: 2px;
}
.remove-row-cta {
  font-size: 11px;
  color: var(--ink-3);
  transition: color .12s;
}
.remove-row:hover:not(:disabled) .remove-row-cta { color: var(--c-again); }

.remove-empty {
  padding: 18px 8px;
  text-align: center;
  font-size: 13px;
  color: var(--ink-2);
}
.remove-error {
  font-size: 12px;
  color: var(--c-again);
  padding: 6px 0;
}
</style>
