<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useReviewStore } from '@/stores/review'
import type { Card, ReviewRating } from '@/types'
import ProgressMeter from '@/components/ProgressMeter.vue'
import ReviewCard from '@/components/ReviewCard.vue'
import RatingButtons from '@/components/RatingButtons.vue'
import SessionComplete from '@/components/SessionComplete.vue'
import KbdHint from '@/components/KbdHint.vue'

const route = useRoute()
const router = useRouter()
const review = useReviewStore()

// Mock cards for demo — will be replaced by API call
const mockCards: Card[] = [
  { id: 'c1', deckId: 'deck-1', question: 'What is the CAP theorem and its three guarantees?', answer: 'Consistency, Availability, Partition tolerance — you can only guarantee two of three in a distributed system.', sourceFile: 'distributed-systems.md', sourceSection: '## CAP Theorem', dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
  { id: 'c2', deckId: 'deck-1', question: 'What is Raft?', answer: 'A consensus algorithm designed to be more understandable than Paxos. Uses leader election and log replication.', sourceFile: 'distributed-systems.md', sourceSection: '## Consensus Algorithms', dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
  { id: 'c3', deckId: 'deck-1', question: 'What is the difference between synchronous and asynchronous replication?', answer: 'Synchronous: write is confirmed only after all replicas acknowledge. Asynchronous: write is confirmed immediately, replicas update eventually.', sourceFile: 'distributed-systems.md', sourceSection: '## Replication', dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
]

onMounted(() => {
  if (!review.isActive) {
    const deckId = route.params.deckId as string || 'deck-1'
    review.startSession(deckId, 'Distributed Systems', mockCards)
  }
})

function onRate(rating: ReviewRating) {
  review.rate(rating)
}

function onDone() {
  review.endSession()
  router.push('/')
}
</script>

<template>
  <div class="flex min-h-[calc(100vh-8rem)] flex-col">
    <template v-if="review.isActive && !review.isComplete">
      <!-- Context bar -->
      <div class="mb-3 flex items-center justify-between text-[11px] text-muted-foreground">
        <div class="flex items-center gap-2">
          <span class="text-foreground">{{ review.deckName }}</span>
          <span>·</span>
          <span>{{ review.progress }}</span>
        </div>
        <div class="hidden items-center gap-2 sm:flex">
          <KbdHint keys="space" /> flip
          <span>·</span>
          <KbdHint keys="1-4" /> rate
        </div>
      </div>

      <!-- Progress meter -->
      <ProgressMeter :total="review.cards.length" :current="review.currentIndex" class="mb-5" />

      <!-- Card -->
      <ReviewCard
        v-if="review.currentCard"
        :card="review.currentCard"
        :is-flipped="review.isFlipped"
        @flip="review.flip()"
      />

      <!-- Rating buttons (only when flipped) -->
      <div v-if="review.isFlipped" class="mt-4">
        <RatingButtons @rate="onRate" />
      </div>

      <div v-else class="mt-4 text-center text-[11px] text-muted-foreground">
        Click the card or press <KbdHint keys="space" /> to reveal the answer
      </div>
    </template>

    <!-- Session complete -->
    <SessionComplete
      v-else-if="review.isComplete"
      :total-cards="review.cards.length"
      :rating-counts="review.ratingCounts"
      @done="onDone"
    />
  </div>
</template>
