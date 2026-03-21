<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useReviewStore } from '@/stores/review'
import type { ReviewRating } from '@/types'
import ProgressMeter from '@/components/ProgressMeter.vue'
import ReviewCard from '@/components/ReviewCard.vue'
import RatingButtons from '@/components/RatingButtons.vue'
import SessionComplete from '@/components/SessionComplete.vue'
import KbdHint from '@/components/KbdHint.vue'
import { useKeyboardShortcuts } from '@/composables/useKeyboardShortcuts'

const router = useRouter()
const review = useReviewStore()

const ratingToQuality: Record<ReviewRating, number> = {
  again: 0,
  hard: 2,
  good: 4,
  easy: 5,
}

const { register, cleanup } = useKeyboardShortcuts()

onMounted(async () => {
  if (!review.isActive) {
    await review.startSession()
  }
  register({
    ' ': () => { if (!review.isFlipped && !review.isComplete) review.flipCard() },
    '1': () => { if (review.isFlipped) review.rate(0) },
    '2': () => { if (review.isFlipped) review.rate(2) },
    '3': () => { if (review.isFlipped) review.rate(4) },
    '4': () => { if (review.isFlipped) review.rate(5) },
    'Escape': () => { review.endSession(); router.push('/dashboard') },
  })
})

onUnmounted(() => {
  cleanup()
})

async function onRate(rating: ReviewRating) {
  await review.rate(ratingToQuality[rating])
}

function onDone() {
  review.endSession()
  router.push('/dashboard')
}
</script>

<template>
  <div class="flex min-h-[calc(100vh-8rem)] flex-col">
    <template v-if="review.isActive && !review.isComplete">
      <!-- Context bar -->
      <div class="mb-3 flex items-center justify-between text-[11px] text-muted-foreground">
        <div class="flex items-center gap-2">
          <span class="text-foreground">Review session</span>
          <span>·</span>
          <span>{{ review.sessionStats.reviewed }} reviewed</span>
        </div>
        <div class="hidden items-center gap-2 sm:flex">
          <KbdHint keys="space" /> flip
          <span>·</span>
          <KbdHint keys="1-4" /> rate
        </div>
      </div>

      <!-- Progress meter -->
      <ProgressMeter :total="100" :current="review.progress" class="mb-5" />

      <!-- Card -->
      <ReviewCard
        v-if="review.currentCard"
        :card="review.currentCard"
        :is-flipped="review.isFlipped"
        @flip="review.flipCard()"
      />

      <!-- Rating buttons (only when flipped) -->
      <div v-if="review.isFlipped" class="mt-4">
        <RatingButtons @rate="onRate" />
      </div>

      <div v-else class="mt-4 text-center text-[11px] text-muted-foreground">
        Click the card or press <KbdHint keys="space" /> to reveal the answer
      </div>
    </template>

    <!-- Loading state -->
    <div v-else-if="review.loading" class="flex flex-1 items-center justify-center text-muted-foreground">
      Loading cards...
    </div>

    <!-- Session complete -->
    <SessionComplete
      v-else-if="review.isComplete"
      :total-cards="review.sessionStats.reviewed"
      :rating-counts="review.sessionStats"
      @done="onDone"
    />
  </div>
</template>
