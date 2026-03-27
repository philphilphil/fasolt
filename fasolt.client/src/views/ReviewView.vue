<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useReviewStore } from '@/stores/review'
import type { ReviewRating } from '@/types'
import ProgressMeter from '@/components/ProgressMeter.vue'
import ReviewCard from '@/components/ReviewCard.vue'
import RatingButtons from '@/components/RatingButtons.vue'
import SessionComplete from '@/components/SessionComplete.vue'
import KbdHint from '@/components/KbdHint.vue'
import { useKeyboardShortcuts } from '@/composables/useKeyboardShortcuts'

const route = useRoute()
const router = useRouter()
const review = useReviewStore()

const { register, cleanup } = useKeyboardShortcuts()

onMounted(async () => {
  if (!review.isActive) {
    const deckId = route.query.deckId as string | undefined
    await review.startSession(deckId)
  }
  register({
    ' ': () => { if (!review.isFlipped && !review.isComplete) review.flipCard() },
    '1': () => { if (review.isFlipped) review.rate('again') },
    '2': () => { if (review.isFlipped) review.rate('hard') },
    '3': () => { if (review.isFlipped) review.rate('good') },
    '4': () => { if (review.isFlipped) review.rate('easy') },
    's': () => { if (!review.isComplete) review.skip() },
    'Escape': () => { review.endSession(); router.push('/study') },
  })
})

onUnmounted(() => {
  cleanup()
})

async function onRate(rating: ReviewRating) {
  await review.rate(rating)
}

function onDone() {
  review.endSession()
  router.push('/dashboard')
}
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col pb-16 sm:pb-0">
    <template v-if="review.isActive && !review.isComplete">
      <!-- Context bar -->
      <div class="mb-4 flex items-center justify-between text-xs text-muted-foreground">
        <div class="flex items-center gap-2">
          <span class="text-accent text-[10px] uppercase tracking-[0.15em]">Review</span>
          <span class="text-border">|</span>
          <span>{{ review.sessionStats.reviewed }} reviewed</span>
        </div>
        <div class="hidden items-center gap-3 sm:flex">
          <span class="flex items-center gap-1"><KbdHint keys="space" /> flip</span>
          <span class="flex items-center gap-1"><KbdHint keys="1-4" /> rate</span>
          <span class="flex items-center gap-1"><KbdHint keys="s" /> skip</span>
        </div>
      </div>

      <!-- Progress meter -->
      <ProgressMeter :total="100" :current="review.progress" class="mb-6" />

      <!-- Card wrapper -->
      <div class="flex flex-1 flex-col items-center justify-center">
        <ReviewCard
          v-if="review.currentCard"
          :card="review.currentCard"
          :is-flipped="review.isFlipped"
          class="w-full"
          @flip="review.flipCard()"
        />
      </div>

      <!-- Rating error -->
      <div v-if="review.error" class="mt-3 text-center text-xs text-destructive">{{ review.error }}</div>

      <!-- Rating buttons -->
      <div v-if="review.isFlipped" class="mt-5">
        <RatingButtons @rate="onRate" />
        <div class="mt-3 text-center">
          <button class="text-xs text-muted-foreground/50 hover:text-muted-foreground transition-colors" @click="review.skip()">Skip</button>
        </div>
      </div>

      <div v-else class="mt-5 text-center text-xs text-muted-foreground">
        Click the card or press <KbdHint keys="space" /> to reveal the answer
        <div class="mt-2">
          <button class="text-xs text-muted-foreground/50 hover:text-muted-foreground transition-colors" @click="review.skip()">Skip</button>
        </div>
      </div>
    </template>

    <!-- Loading state -->
    <div v-else-if="review.loading" class="flex flex-1 items-center justify-center text-sm text-muted-foreground">
      Loading cards...
    </div>

    <!-- No cards due -->
    <div v-else-if="review.noDueCards" class="flex flex-1 flex-col items-center justify-center gap-4 text-center">
      <div class="text-base text-foreground">All caught up!</div>
      <div class="text-xs text-muted-foreground">No cards are due for review right now.</div>
      <button class="text-xs text-accent hover:underline" @click="onDone">Back to dashboard</button>
    </div>

    <!-- Session complete -->
    <SessionComplete
      v-else-if="review.isComplete"
      :total-cards="review.sessionStats.reviewed"
      :rating-counts="review.sessionStats"
      :skipped-count="review.sessionStats.skipped"
      @done="onDone"
    />

    <!-- Error state -->
    <div v-else-if="review.error" class="flex flex-1 flex-col items-center justify-center gap-4 text-center">
      <div class="text-sm text-destructive">{{ review.error }}</div>
      <button class="text-xs text-accent hover:underline" @click="router.push('/dashboard')">Back to dashboard</button>
    </div>
  </div>
</template>
