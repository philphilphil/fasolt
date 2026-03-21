<script setup lang="ts">
import type { SearchResponse } from '@/api/client'
import type { SearchItem } from '@/composables/useSearch'

defineProps<{
  results: SearchResponse
  isLoading: boolean
  activeIndex: number
  flatItems: SearchItem[]
  hasResults: boolean
}>()

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
    class="absolute left-0 right-0 top-full mt-1 z-50 max-h-[400px] overflow-y-auto rounded-md border border-border bg-popover shadow-lg"
  >
    <div v-if="isLoading" class="px-3 py-6 text-center text-xs text-muted-foreground">
      Searching...
    </div>

    <div v-else-if="!hasResults" class="px-3 py-6 text-center text-xs text-muted-foreground">
      No results found
    </div>

    <div v-else>
      <!-- Cards -->
      <div v-if="results.cards.length > 0">
        <div class="px-3 py-1.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
          Cards ({{ results.cards.length }})
        </div>
        <button
          v-for="(card, i) in results.cards"
          :key="card.id"
          class="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-accent"
          :class="{ 'bg-accent': activeIndex === globalIndex(flatItems, 'card', i) }"
          @click="emit('select', { type: 'card', data: card })"
          @mouseenter="emit('update:activeIndex', globalIndex(flatItems, 'card', i))"
        >
          <span class="shrink-0 text-xs text-muted-foreground">
            {{ card.cardType === 'file' ? '📄' : card.cardType === 'section' ? '§' : '✏️' }}
          </span>
          <span class="truncate" v-html="card.headline" />
          <span class="ml-auto shrink-0 rounded bg-secondary px-1.5 py-0.5 text-[10px] text-muted-foreground">
            {{ card.state }}
          </span>
        </button>
      </div>

      <!-- Decks -->
      <div v-if="results.decks.length > 0">
        <div class="px-3 py-1.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground"
             :class="{ 'border-t border-border': results.cards.length > 0 }">
          Decks ({{ results.decks.length }})
        </div>
        <button
          v-for="(deck, i) in results.decks"
          :key="deck.id"
          class="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-accent"
          :class="{ 'bg-accent': activeIndex === globalIndex(flatItems, 'deck', i) }"
          @click="emit('select', { type: 'deck', data: deck })"
          @mouseenter="emit('update:activeIndex', globalIndex(flatItems, 'deck', i))"
        >
          <span class="shrink-0 text-xs text-muted-foreground">📚</span>
          <span class="truncate" v-html="deck.headline" />
          <span class="ml-auto shrink-0 text-xs text-muted-foreground">
            {{ deck.cardCount }} cards
          </span>
        </button>
      </div>

      <!-- Files -->
      <div v-if="results.files.length > 0">
        <div class="px-3 py-1.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground"
             :class="{ 'border-t border-border': results.cards.length > 0 || results.decks.length > 0 }">
          Files ({{ results.files.length }})
        </div>
        <button
          v-for="(file, i) in results.files"
          :key="file.id"
          class="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-accent"
          :class="{ 'bg-accent': activeIndex === globalIndex(flatItems, 'file', i) }"
          @click="emit('select', { type: 'file', data: file })"
          @mouseenter="emit('update:activeIndex', globalIndex(flatItems, 'file', i))"
        >
          <span class="shrink-0 text-xs text-muted-foreground">📝</span>
          <span class="truncate" v-html="file.headline" />
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
