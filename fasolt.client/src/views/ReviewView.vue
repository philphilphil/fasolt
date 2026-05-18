<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useReviewStore } from '@/stores/review'
import { useDecksStore } from '@/stores/decks'
import type { ReviewRating } from '@/types'
import { Button } from '@/components/ui/button'
import ProgressMeter from '@/components/ProgressMeter.vue'
import ReviewCard from '@/components/ReviewCard.vue'
import RatingButtons from '@/components/RatingButtons.vue'
import SessionComplete from '@/components/SessionComplete.vue'
import KbdHint from '@/components/KbdHint.vue'
import { useKeyboardShortcuts } from '@/composables/useKeyboardShortcuts'
import { deckColor } from '@/lib/utils'

const route = useRoute()
const router = useRouter()
const review = useReviewStore()
const decks = useDecksStore()

const { register, cleanup } = useKeyboardShortcuts()

onMounted(async () => {
  if (!review.isActive) {
    const deckId = route.query.deckId as string | undefined
    const mode = route.query.mode === 'cram' ? 'cram' : 'normal'
    await review.startSession(deckId, mode)
  }
  if (decks.decks.length === 0) decks.fetchDecks()
  register({
    ' ': () => {
      if (review.isComplete) return
      if (!review.isFlipped) review.flipCard()
      else if (review.mode === 'cram') review.advance()
    },
    'n': () => { if (review.mode === 'cram' && review.isFlipped) review.advance() },
    '1': () => { if (review.mode !== 'cram' && review.isFlipped) review.rate('again') },
    '2': () => { if (review.mode !== 'cram' && review.isFlipped) review.rate('hard') },
    '3': () => { if (review.mode !== 'cram' && review.isFlipped) review.rate('good') },
    '4': () => { if (review.mode !== 'cram' && review.isFlipped) review.rate('easy') },
    's': () => { if (!review.isComplete) review.skip() },
    'x': () => { if (!review.isComplete) review.suspend() },
    'Escape': () => { review.endSession(); router.push('/study') },
  })
})

onUnmounted(() => { cleanup() })

async function onRate(rating: ReviewRating) { await review.rate(rating) }

function onDone() {
  review.endSession()
  router.push('/study')
}

// Context bar: derive deck info from the route query (?deckId=…) so the chip
// appears even before the first card returns.
const activeDeckId = computed(() => (route.query.deckId as string) || null)
const activeDeck = computed(() => {
  if (!activeDeckId.value) return null
  return decks.decks.find(d => d.id === activeDeckId.value) || null
})
const activeDeckLabel = computed(() => activeDeck.value?.name || 'All decks')
const activeDeckColor = computed(() => deckColor(activeDeck.value?.id || 'all-decks'))

const modeLabel = computed(() => review.mode === 'cram' ? 'CRAM' : 'REVIEW')
</script>

<template>
  <div class="review-page">
    <template v-if="review.isActive && !review.isComplete && !review.noDueCards">
      <!-- Context bar -->
      <header class="context-bar">
        <div class="context-left">
          <span class="fa-cap context-mode">{{ modeLabel }}</span>
          <span class="context-vrule" />
          <div class="context-deck">
            <span class="fa-tag" :style="{ background: activeDeckColor }" />
            <span class="context-deck-name">{{ activeDeckLabel }}</span>
          </div>
          <span class="context-vrule" />
          <span class="fa-mono context-count">{{ review.sessionStats.reviewed }} / {{ review.queue.length }} reviewed</span>
        </div>
        <div class="context-right">
          <span class="kbd-hint"><KbdHint keys="space" /> flip</span>
          <span v-if="review.mode === 'cram'" class="kbd-hint"><KbdHint keys="n" /> next</span>
          <span v-else class="kbd-hint"><KbdHint keys="1-4" /> rate</span>
          <span class="kbd-hint"><KbdHint keys="s" /> skip</span>
          <span class="kbd-hint"><KbdHint keys="x" /> suspend</span>
          <span class="kbd-hint"><KbdHint keys="esc" /> exit</span>
        </div>
      </header>

      <!-- Progress meter -->
      <ProgressMeter :total="review.queue.length" :current="review.sessionStats.reviewed" class="meter" />

      <!-- Cram mode notice -->
      <div v-if="review.mode === 'cram'" class="cram-notice">
        Custom study — FSRS scheduling is not adjusted
      </div>

      <!-- Card -->
      <div class="card-frame">
        <ReviewCard
          v-if="review.currentCard"
          :card="review.currentCard"
          :is-flipped="review.isFlipped"
          @flip="review.flipCard()"
        />
      </div>

      <div v-if="review.error" class="error">{{ review.error }}</div>

      <!-- Rating bar -->
      <div v-if="review.isFlipped" class="action-row">
        <Button
          v-if="review.mode === 'cram'"
          class="w-full"
          data-testid="next-button"
          @click="review.advance()"
        >Next</Button>
        <RatingButtons v-else @rate="onRate" />
      </div>
      <div v-else class="flip-hint">
        Click the card or press <KbdHint keys="space" /> to reveal the answer
      </div>

      <div class="aux-row">
        <button class="aux-link" @click="review.skip()">Skip</button>
        <span class="aux-sep">·</span>
        <button class="aux-link" @click="review.suspend()">Suspend</button>
      </div>
    </template>

    <div v-else-if="review.loading" class="state">Loading cards…</div>

    <div v-else-if="review.noDueCards" class="state-block">
      <h2 class="fa-serif state-title">All caught up.</h2>
      <p class="state-sub">No cards are due for review right now.</p>
      <button class="fa-btn fa-btn-primary" @click="onDone">Back to study</button>
    </div>

    <SessionComplete
      v-else-if="review.isComplete"
      :total-cards="review.sessionStats.reviewed"
      :rating-counts="review.sessionStats"
      :skipped-count="review.sessionStats.skipped"
      :mode="review.mode"
      @done="onDone"
    />

    <div v-else-if="review.error" class="state-block">
      <p class="state-error">{{ review.error }}</p>
      <button class="fa-btn" @click="router.push('/study')">Back to study</button>
    </div>
  </div>
</template>

<style scoped>
.review-page {
  width: 100%;
  max-width: 880px;
  margin: 0 auto;
  padding: 24px 0 40px;
  display: flex;
  flex-direction: column;
  gap: 18px;
  min-height: calc(100vh - 120px);
}
.context-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
}
.context-left {
  display: flex;
  align-items: center;
  gap: 14px;
  flex-wrap: wrap;
}
.context-mode { color: var(--accent); }
.context-vrule { width: 1px; height: 12px; background: var(--rule-1); }
.context-deck {
  display: flex;
  align-items: center;
  gap: 6px;
}
.context-deck-name {
  font-size: 13px;
  color: var(--ink-1);
}
.context-count { font-size: 12px; color: var(--ink-2); }
.context-right {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  color: var(--ink-2);
  font-size: 11px;
}
@media (max-width: 640px) {
  .context-right { display: none; }
}
.kbd-hint {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}

.meter { margin-top: 2px; }

.cram-notice {
  text-align: center;
  font-size: 11px;
  color: var(--ink-2);
}
.card-frame {
  margin-top: 12px;
}

.action-row { margin-top: 4px; }
.flip-hint {
  display: flex;
  justify-content: center;
  gap: 8px;
  margin-top: 8px;
  color: var(--ink-2);
  font-size: 13px;
}

.aux-row {
  display: flex;
  justify-content: center;
  gap: 8px;
  margin-top: 2px;
  font-size: 12px;
  color: var(--ink-3);
}
.aux-link {
  background: none;
  border: none;
  font: inherit;
  color: var(--ink-3);
  cursor: pointer;
  transition: color .12s;
}
.aux-link:hover { color: var(--ink-0); }
.aux-sep { color: var(--ink-3); }

.error {
  text-align: center;
  font-size: 12px;
  color: var(--c-again);
}

.state {
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 1;
  color: var(--ink-2);
  font-size: 13px;
}
.state-block {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  flex: 1;
  gap: 14px;
  text-align: center;
  padding: 40px 0;
}
.state-title {
  font-size: 56px;
  letter-spacing: -0.03em;
  line-height: 1;
  margin: 0;
  color: var(--ink-0);
}
.state-sub {
  color: var(--ink-1);
  font-size: 15px;
  max-width: 420px;
}
.state-error { color: var(--c-again); font-size: 14px; }
</style>
