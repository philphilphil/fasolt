<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useReviewStore } from '@/stores/review'
import { useDecksStore } from '@/stores/decks'
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
      <h1 class="text-xl font-bold tracking-tight">Dashboard</h1>
      <Button v-if="dueCount > 0" class="glow-accent" @click="studyNow">
        Study now · {{ dueCount }} due
      </Button>
    </div>

    <!-- Stat bar -->
    <div class="bg-muted/50 rounded-lg px-4 py-3 flex items-center gap-5">
      <div>
        <span class="text-lg font-bold text-warning">{{ stats[0].value }}</span>
        <span class="text-xs text-muted-foreground ml-1.5">due</span>
      </div>
      <div class="w-px h-5 bg-border" />
      <div>
        <span class="text-lg font-bold">{{ stats[1].value }}</span>
        <span class="text-xs text-muted-foreground ml-1.5">total cards</span>
      </div>
      <div class="w-px h-5 bg-border" />
      <div>
        <span class="text-lg font-bold">{{ stats[2].value }}</span>
        <span class="text-xs text-muted-foreground ml-1.5">studied today</span>
      </div>
    </div>

    <!-- Decks section -->
    <div>
      <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Your decks</div>
      <DeckTable :decks="decksStore.decks" @select-deck="onSelectDeck" />
    </div>
  </div>
</template>
