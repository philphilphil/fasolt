<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, computed } from 'vue'

const cards = [
  {
    front: 'What does a tokenizer do and why is it needed?',
    back: 'Splits text into tokens (numeric IDs) that LLMs operate on, instead of raw characters.',
    rating: 2, // Good
  },
  {
    front: 'What is the context window of an LLM?',
    back: 'The maximum number of tokens the model can attend to at once when generating a response.',
    rating: 3, // Easy
  },
  {
    front: 'How does temperature affect text generation?',
    back: 'Higher = more random and creative. Lower = more focused and deterministic.',
    rating: 1, // Hard
  },
]
const totalInDeck = 7

const currentIndex = ref(0)
const isFlipped = ref(false)
const showRating = ref(false)
const highlightedRating = ref<number | null>(null)
const isExiting = ref(false)

const card = computed(() => cards[currentIndex.value])
const progressPct = computed(() => {
  const completed = currentIndex.value + (showRating.value ? 1 : 0)
  return Math.min(100, Math.round((completed / totalInDeck) * 100))
})

const timers: ReturnType<typeof setTimeout>[] = []
const reducedMotion = computed(() =>
  typeof window !== 'undefined' &&
  window.matchMedia &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches,
)

function clearTimers() {
  while (timers.length) clearTimeout(timers.shift()!)
}

function startCycle() {
  clearTimers()
  isExiting.value = false
  isFlipped.value = false
  showRating.value = false
  highlightedRating.value = null

  if (reducedMotion.value) {
    isFlipped.value = true
    showRating.value = true
    highlightedRating.value = card.value.rating
    return
  }

  timers.push(setTimeout(() => { isFlipped.value = true }, 2000))
  timers.push(setTimeout(() => { showRating.value = true }, 2300))
  timers.push(setTimeout(() => { highlightedRating.value = card.value.rating }, 3400))
  timers.push(setTimeout(() => { isExiting.value = true }, 4900))
  timers.push(setTimeout(() => {
    currentIndex.value = (currentIndex.value + 1) % cards.length
    startCycle()
  }, 5300))
}

const ratings = [
  { label: 'Again', color: 'var(--c-again)', tint: 'color-mix(in srgb, var(--c-again) 12%, transparent)' },
  { label: 'Hard', color: 'var(--c-hard)', tint: 'color-mix(in srgb, var(--c-hard) 14%, transparent)' },
  { label: 'Good', color: 'var(--c-good)', tint: 'color-mix(in srgb, var(--c-good) 12%, transparent)' },
  { label: 'Easy', color: 'var(--c-easy)', tint: 'color-mix(in srgb, var(--c-easy) 12%, transparent)' },
]

onMounted(startCycle)
onBeforeUnmount(clearTimers)
</script>

<template>
  <div class="preview">
    <!-- Window chrome -->
    <div class="chrome">
      <span class="dot"></span>
      <span class="dot"></span>
      <span class="dot"></span>
      <span class="chrome-title">fasolt — Study</span>
    </div>

    <!-- Body -->
    <div class="body">
      <!-- Today / progress header -->
      <div class="head">
        <div class="deck-label">
          <span class="deck-dot"></span>
          LLM Basics
        </div>
        <span class="counter fa-num">{{ currentIndex + 1 }} of {{ totalInDeck }}</span>
      </div>

      <!-- Progress meter -->
      <div class="meter">
        <div class="meter-fill" :style="{ width: progressPct + '%' }"></div>
      </div>

      <!-- Card -->
      <div class="card" :class="{ 'is-exiting': isExiting }">
        <div class="card-label">{{ isFlipped ? 'Answer' : 'Question' }}</div>
        <div class="card-front" :class="{ 'is-muted': isFlipped }">
          {{ card.front }}
        </div>
        <div v-if="isFlipped" class="card-back">
          {{ card.back }}
        </div>
      </div>

      <!-- Below card -->
      <div class="actions" :class="{ 'is-exiting': isExiting }">
        <div v-if="!showRating" class="hint">
          Click the card or press <kbd class="fa-kbd">space</kbd> to reveal the answer
        </div>
        <div v-else class="rating-row">
          <button
            v-for="(r, i) in ratings"
            :key="r.label"
            class="rating-btn"
            :class="{ 'is-active': highlightedRating === i }"
            :style="{
              '--rb-color': r.color,
              '--rb-tint': r.tint,
            }"
            type="button"
            tabindex="-1"
          >
            {{ r.label }}
            <span class="rating-key fa-num">{{ i + 1 }}</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.preview {
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-radius: 14px;
  border: 1px solid var(--rule-1);
  background: var(--paper-1);
  box-shadow: var(--sh-2);
}
@media (min-width: 1024px) {
  .preview { height: 420px; }
}

/* Window chrome */
.chrome {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 11px 16px;
  border-bottom: 1px solid var(--rule-1);
  background: var(--paper-1);
}
.dot {
  width: 9px;
  height: 9px;
  border-radius: 999px;
  background: var(--ink-3);
}
.chrome-title {
  margin-left: 8px;
  font-size: 11px;
  color: var(--ink-2);
}

/* Body */
.body {
  flex: 1;
  padding: 18px 18px 20px;
}
@media (min-width: 640px) {
  .body { padding: 20px; }
}

.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}
.deck-label {
  display: flex;
  align-items: center;
  gap: 7px;
  color: var(--ink-1);
  font-size: 13px;
  font-weight: 600;
}
.deck-dot {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  background: #2e7cf6;
}
.counter {
  font-size: 12px;
  color: var(--ink-2);
}

/* Progress meter */
.meter {
  height: 4px;
  width: 100%;
  overflow: hidden;
  border-radius: 999px;
  background: var(--paper-2);
  margin-bottom: 20px;
}
.meter-fill {
  height: 100%;
  background: var(--accent);
  border-radius: 999px;
  transition: width .5s ease;
}

/* Card */
.card {
  position: relative;
  border-radius: 12px;
  border: 1px solid var(--rule-1);
  background: var(--paper-1);
  padding: 18px;
  min-height: 140px;
  transition: opacity .3s, transform .3s;
}
.card.is-exiting {
  opacity: 0;
  transform: translateY(-4px);
}
.card-label {
  margin-bottom: 10px;
  font-size: 12px;
  font-weight: 600;
  color: var(--ink-2);
}
.card-front {
  font-size: 14px;
  line-height: 1.55;
  color: var(--ink-0);
  transition: color .3s;
}
.card-front.is-muted { color: var(--ink-2); }
.card-back {
  margin-top: 12px;
  font-size: 14px;
  line-height: 1.55;
  color: var(--ink-0);
  animation: fade-in 350ms ease-out;
}

/* Below card */
.actions {
  margin-top: 16px;
  transition: opacity .3s;
}
.actions.is-exiting { opacity: 0; }
.hint {
  text-align: center;
  font-size: 12px;
  color: var(--ink-2);
}
.rating-row {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 8px;
  animation: fade-in 250ms ease-out;
}
.rating-btn {
  flex: 1;
  min-width: 70px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 5px;
  border-radius: 9px;
  border: 1px solid var(--rule-1);
  background: var(--paper-1);
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 500;
  color: var(--ink-1);
  transition: background .12s, border-color .12s, color .12s;
}
.rating-btn.is-active {
  border-color: var(--rb-color);
  background: var(--rb-tint);
  color: var(--ink-0);
}
.rating-key {
  font-size: 10px;
  color: var(--ink-3);
}

@keyframes fade-in {
  from { opacity: 0; transform: translateY(4px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>
