<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useDashboardStore } from '@/stores/dashboard'
import { useReviewStore } from '@/stores/review'
import StatGrid from '@/components/StatGrid.vue'
import DeckTable from '@/components/DeckTable.vue'
import { Button } from '@/components/ui/button'
import type { Deck, Stat } from '@/types'

const router = useRouter()
const dashboard = useDashboardStore()
const reviewStore = useReviewStore()

const dueCount = ref(0)
const totalCards = ref(0)

const stats = ref<Stat[]>([
  { label: 'Due', value: '…' },
  { label: 'Total', value: '…' },
  { label: 'Retention', value: '91%', delta: '↑ 3% this week' },
  { label: 'Streak', value: '7d' },
])

onMounted(async () => {
  try {
    const reviewStats = await reviewStore.fetchStats()
    dueCount.value = reviewStats.dueCount
    totalCards.value = reviewStats.totalCards
    stats.value[0] = { label: 'Due', value: String(reviewStats.dueCount) }
    stats.value[1] = { label: 'Total', value: String(reviewStats.totalCards) }
  } catch {
    stats.value[0] = { label: 'Due', value: '—' }
    stats.value[1] = { label: 'Total', value: '—' }
  }
})

function onSelectDeck(deck: Deck) {
  if (deck.dueCount > 0) {
    router.push({ name: 'review' })
  }
}

function studyNow() {
  router.push({ name: 'review' })
}
</script>

<template>
  <div class="space-y-5">
    <div class="flex items-center justify-between">
      <h1 class="text-lg font-semibold text-foreground">Dashboard</h1>
      <Button v-if="dueCount > 0" @click="studyNow">
        Study now · {{ dueCount }} due
      </Button>
    </div>
    <StatGrid :stats="stats" />
    <DeckTable :decks="dashboard.decks" @select-deck="onSelectDeck" />
  </div>
</template>
