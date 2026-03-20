<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useDashboardStore } from '@/stores/dashboard'
import StatGrid from '@/components/StatGrid.vue'
import DeckTable from '@/components/DeckTable.vue'
import type { Deck } from '@/types'

const router = useRouter()
const dashboard = useDashboardStore()

function onSelectDeck(deck: Deck) {
  if (deck.dueCount > 0) {
    router.push({ name: 'review', params: { deckId: deck.id } })
  }
}
</script>

<template>
  <div class="space-y-5">
    <StatGrid :stats="dashboard.stats" />
    <DeckTable :decks="dashboard.decks" @select-deck="onSelectDeck" />
  </div>
</template>
