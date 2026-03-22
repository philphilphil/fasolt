<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useReviewStore } from '@/stores/review'
import { useDecksStore } from '@/stores/decks'
import StatGrid from '@/components/StatGrid.vue'
import DeckTable from '@/components/DeckTable.vue'
import { Button } from '@/components/ui/button'
import type { Deck, Stat } from '@/types'

const router = useRouter()
const reviewStore = useReviewStore()
const decksStore = useDecksStore()

const dueCount = ref(0)
const totalCards = ref(0)

const stats = ref<Stat[]>([
  { label: 'Due', value: '...' },
  { label: 'Total', value: '...' },
  { label: 'Studied today', value: '...' },
])

onMounted(async () => {
  try {
    const reviewStats = await reviewStore.fetchStats()
    dueCount.value = reviewStats.dueCount
    totalCards.value = reviewStats.totalCards
    stats.value[0] = { label: 'Due', value: String(reviewStats.dueCount) }
    stats.value[1] = { label: 'Total', value: String(reviewStats.totalCards) }
    stats.value[2] = { label: 'Studied today', value: String(reviewStats.studiedToday) }
  } catch {
    stats.value[0] = { label: 'Due', value: '—' }
    stats.value[1] = { label: 'Total', value: '—' }
    stats.value[2] = { label: 'Studied today', value: '—' }
  }
  decksStore.fetchDecks()
})

function onSelectDeck(deck: Deck) {
  router.push(`/decks/${deck.id}`)
}

function studyNow() {
  router.push({ name: 'review' })
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <h1 class="text-base font-semibold text-foreground">Dashboard</h1>
      <Button v-if="dueCount > 0" class="glow-accent" @click="studyNow">
        Study now · {{ dueCount }} due
      </Button>
    </div>
    <StatGrid :stats="stats" />
    <DeckTable :decks="decksStore.decks" @select-deck="onSelectDeck" />
  </div>
</template>
