<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useReviewStore } from '@/stores/review'
import { useDecksStore } from '@/stores/decks'
import { Button } from '@/components/ui/button'

const router = useRouter()
const reviewStore = useReviewStore()
const decksStore = useDecksStore()

const activeDecks = computed(() => decksStore.decks.filter(d => !d.isSuspended))

const dueCount = ref(0)
const totalCards = ref(0)
const studiedToday = ref(0)

onMounted(async () => {
  try {
    const stats = await reviewStore.fetchStats()
    dueCount.value = stats.dueCount
    totalCards.value = stats.totalCards
    studiedToday.value = stats.studiedToday
  } catch {
    // leave as 0
  }
  decksStore.fetchDecks()
})
</script>

<template>
  <div class="mx-auto max-w-[480px] space-y-6 py-8">
    <!-- Due count -->
    <div class="text-center">
      <div class="text-[56px] font-extrabold leading-none tracking-tighter">{{ dueCount }}</div>
      <div class="mt-2 text-sm text-muted-foreground">cards due for review</div>
    </div>

    <!-- CTA -->
    <div class="text-center">
      <Button v-if="dueCount > 0" size="lg" class="px-10" @click="router.push('/review')">
        Start reviewing
      </Button>
      <p v-else class="text-sm text-muted-foreground">All caught up</p>
    </div>

    <!-- Stats -->
    <div class="flex justify-center gap-7 text-sm text-muted-foreground">
      <div><span class="font-bold text-foreground">{{ totalCards }}</span> total</div>
      <div><span class="font-bold text-foreground">{{ studiedToday }}</span> today</div>
    </div>

    <div class="border-t border-border" />

    <!-- Study by deck -->
    <div>
      <div class="text-[11px] font-semibold uppercase tracking-[1.2px] text-muted-foreground mb-3">Study by deck</div>
      <div class="flex flex-col gap-2">
        <div
          v-for="deck in activeDecks"
          :key="deck.id"
          class="flex cursor-pointer items-center justify-between rounded-lg border border-border bg-card px-4 py-3 transition-colors hover:border-foreground/20"
          @click="router.push(`/review?deckId=${deck.id}`)"
        >
          <div>
            <div class="text-[13px] font-semibold">{{ deck.name }}</div>
            <div class="text-[11px] text-muted-foreground">{{ deck.cardCount }} cards</div>
          </div>
          <span
            v-if="deck.dueCount > 0"
            class="rounded-full bg-warning/10 px-2.5 py-0.5 text-[10px] font-medium text-warning"
          >
            {{ deck.dueCount }} due
          </span>
          <span
            v-else
            class="rounded-full border border-border px-2.5 py-0.5 text-[10px] text-muted-foreground"
          >
            0 due
          </span>
        </div>
      </div>
    </div>
  </div>
</template>
