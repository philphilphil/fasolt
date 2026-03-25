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
  <div
    class="absolute left-0 right-0 top-full mt-1 z-50 max-h-[1200px] overflow-y-auto rounded border border-border/60 bg-popover shadow-lg"
  >
    <div v-if="isLoading" class="px-3 py-6 text-center text-[11px] text-muted-foreground">
      Searching...
    </div>

    <div v-else-if="error" class="px-3 py-6 text-center text-[11px] text-destructive">
      {{ error }}
    </div>

    <div v-else-if="!hasResults" class="px-3 py-6 text-center text-[11px] text-muted-foreground">
      No results found
    </div>

    <div v-else>
      <!-- Decks -->
      <div v-if="results.decks.length > 0">
        <div class="px-3 py-1.5 text-[10px] font-medium uppercase tracking-[0.15em] text-muted-foreground">
          Decks ({{ results.decks.length }})
        </div>
        <button
          v-for="(deck, i) in results.decks"
          :key="deck.id"
          class="flex w-full items-center gap-2 px-3 py-2 text-left text-xs hover:bg-accent/10"
          :class="{ 'bg-accent/10': activeIndex === globalIndex(flatItems, 'deck', i) }"
          @click="emit('select', { type: 'deck', data: deck })"
          @mouseenter="emit('update:activeIndex', globalIndex(flatItems, 'deck', i))"
        >
          <span class="shrink-0 text-[10px] text-accent/50">#</span>
          <span class="truncate"><template v-for="(part, pi) in highlightParts(deck.headline)" :key="pi"><mark v-if="part.match">{{ part.text }}</mark><template v-else>{{ part.text }}</template></template></span>
          <span class="ml-auto shrink-0 text-[10px] text-muted-foreground">
            {{ deck.cardCount }} cards
          </span>
        </button>
      </div>

      <!-- Cards -->
      <div v-if="results.cards.length > 0">
        <div class="px-3 py-1.5 text-[10px] font-medium uppercase tracking-[0.15em] text-muted-foreground"
             :class="{ 'border-t border-border': results.decks.length > 0 }">
          Cards ({{ results.cards.length }})
        </div>
        <button
          v-for="(card, i) in results.cards"
          :key="card.id"
          class="flex w-full items-center gap-2 px-3 py-2 text-left text-xs hover:bg-accent/10"
          :class="{ 'bg-accent/10': activeIndex === globalIndex(flatItems, 'card', i) }"
          @click="emit('select', { type: 'card', data: card })"
          @mouseenter="emit('update:activeIndex', globalIndex(flatItems, 'card', i))"
        >
          <span class="shrink-0 text-[10px] text-accent/50">></span>
          <span class="truncate"><template v-for="(part, pi) in highlightParts(card.headline)" :key="pi"><mark v-if="part.match">{{ part.text }}</mark><template v-else>{{ part.text }}</template></template></span>
          <span class="ml-auto shrink-0 rounded border border-border bg-secondary px-1.5 py-0.5 text-[10px] text-muted-foreground">
            {{ card.state }}
          </span>
        </button>
      </div>

      <!-- Footer -->
      <div class="flex gap-3 border-t border-border px-3 py-1.5 text-[10px] text-muted-foreground">
        <span>↑↓ navigate</span>
        <span>↵ open</span>
        <span>esc close</span>
      </div>
    </div>
  </div>
</template>
