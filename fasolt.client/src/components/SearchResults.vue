<script setup lang="ts">
import type { SearchResponse } from '@/api/client'
import type { SearchItem } from '@/composables/useSearch'

const props = defineProps<{
  results: SearchResponse
  query: string
  isLoading: boolean
  activeIndex: number
  flatItems: SearchItem[]
  hasResults: boolean
  error: string | null
}>()

function highlightParts(text: string): { text: string; match: boolean }[] {
  const q = props.query.trim()
  if (!q) return [{ text, match: false }]
  const idx = text.toLowerCase().indexOf(q.toLowerCase())
  if (idx === -1) return [{ text, match: false }]
  return [
    ...(idx > 0 ? [{ text: text.slice(0, idx), match: false }] : []),
    { text: text.slice(idx, idx + q.length), match: true },
    ...(idx + q.length < text.length ? [{ text: text.slice(idx + q.length), match: false }] : []),
  ]
}

const emit = defineEmits<{
  select: [item: SearchItem]
  'update:activeIndex': [index: number]
}>()

function globalIndex(flatItems: SearchItem[], type: string, localIndex: number): number {
  let count = 0
  for (let i = 0; i < flatItems.length; i++) {
    if (flatItems[i].type === type) {
      if (count === localIndex) return i
      count++
    }
  }
  return -1
}
</script>

<template>
  <div class="sr-panel">
    <div v-if="isLoading" class="sr-status">
      Searching…
    </div>

    <div v-else-if="error" class="sr-status sr-status-error">
      {{ error }}
    </div>

    <div v-else-if="!hasResults" class="sr-status">
      No results found
    </div>

    <div v-else>
      <!-- Decks -->
      <div v-if="results.decks.length > 0">
        <div class="sr-group-label fa-cap">
          Decks ({{ results.decks.length }})
        </div>
        <button
          v-for="(deck, i) in results.decks"
          :key="deck.id"
          class="sr-row"
          :class="{ 'is-active': activeIndex === globalIndex(flatItems, 'deck', i) }"
          @click="emit('select', { type: 'deck', data: deck })"
          @mouseenter="emit('update:activeIndex', globalIndex(flatItems, 'deck', i))"
        >
          <span class="sr-marker">#</span>
          <span class="sr-text"><template v-for="(part, pi) in highlightParts(deck.headline)" :key="pi"><mark v-if="part.match">{{ part.text }}</mark><template v-else>{{ part.text }}</template></template></span>
          <span class="sr-meta">
            {{ deck.cardCount }} cards
          </span>
        </button>
      </div>

      <!-- Cards -->
      <div v-if="results.cards.length > 0">
        <div class="sr-group-label fa-cap" :class="{ 'sr-group-label-divided': results.decks.length > 0 }">
          Cards ({{ results.cards.length }})
        </div>
        <button
          v-for="(card, i) in results.cards"
          :key="card.id"
          class="sr-row"
          :class="{ 'is-active': activeIndex === globalIndex(flatItems, 'card', i) }"
          @click="emit('select', { type: 'card', data: card })"
          @mouseenter="emit('update:activeIndex', globalIndex(flatItems, 'card', i))"
        >
          <span class="sr-marker">›</span>
          <span class="sr-text"><template v-for="(part, pi) in highlightParts(card.headline)" :key="pi"><mark v-if="part.match">{{ part.text }}</mark><template v-else>{{ part.text }}</template></template></span>
          <span class="sr-state">
            {{ card.state }}
          </span>
        </button>
      </div>

      <!-- Footer -->
      <div class="sr-footer">
        <span>↑↓ navigate</span>
        <span>↵ open</span>
        <span>esc close</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.sr-panel {
  position: absolute;
  left: 50%;
  top: 100%;
  transform: translateX(-50%);
  margin-top: 6px;
  z-index: 50;
  width: 500px;
  max-height: 70vh;
  overflow-y: auto;
  border: 1px solid var(--rule-1);
  border-radius: 12px;
  background: var(--paper-1);
  box-shadow: var(--sh-2);
}

.sr-status {
  padding: 24px 12px;
  text-align: center;
  font-size: 12.5px;
  color: var(--ink-2);
}
.sr-status-error { color: var(--c-again); }

.sr-group-label {
  padding: 8px 12px 5px;
}
.sr-group-label-divided {
  border-top: 1px solid var(--rule-1);
  margin-top: 2px;
}

.sr-row {
  display: flex;
  width: 100%;
  align-items: center;
  gap: 9px;
  padding: 8px 12px;
  text-align: left;
  font-size: 13px;
  color: var(--ink-0);
  background: transparent;
  border: none;
  cursor: pointer;
  font: inherit;
  transition: background .12s, color .12s;
}
.sr-row:hover { background: var(--paper-2); }
.sr-row.is-active {
  background: var(--accent);
  color: var(--accent-on);
}

.sr-marker {
  flex-shrink: 0;
  font-size: 11px;
  color: var(--ink-3);
}
.sr-row.is-active .sr-marker { color: var(--accent-on); }

.sr-text {
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.sr-meta {
  margin-left: auto;
  flex-shrink: 0;
  font-size: 11px;
  color: var(--ink-2);
}
.sr-row.is-active .sr-meta { color: var(--accent-on); opacity: .82; }

.sr-state {
  margin-left: auto;
  flex-shrink: 0;
  padding: 1px 7px;
  font-size: 10.5px;
  color: var(--ink-1);
  background: var(--paper-2);
  border: 1px solid var(--rule-1);
  border-radius: 999px;
}
.sr-row.is-active .sr-state {
  color: var(--accent-on);
  background: transparent;
  border-color: rgba(255, 255, 255, .35);
}

/* Highlighted match: keep readable when the row is the active accent fill */
.sr-row.is-active mark {
  background: rgba(255, 255, 255, .25);
  color: var(--accent-on);
}

.sr-footer {
  display: flex;
  gap: 12px;
  padding: 7px 12px;
  border-top: 1px solid var(--rule-1);
  font-size: 11px;
  color: var(--ink-2);
}
</style>
