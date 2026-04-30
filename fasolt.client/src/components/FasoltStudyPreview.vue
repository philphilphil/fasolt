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

onMounted(startCycle)
onBeforeUnmount(clearTimers)
</script>

<template>
  <div class="flex flex-col overflow-hidden rounded-xl border border-border/60 bg-card/60 shadow-2xl glow-accent-lg lg:h-[420px]">
    <!-- Window chrome -->
    <div class="flex items-center gap-2 px-4 py-3 border-b border-border/60 bg-card/80">
      <span class="h-2.5 w-2.5 rounded-full bg-[#ff5f57]/80"></span>
      <span class="h-2.5 w-2.5 rounded-full bg-[#febc2e]/80"></span>
      <span class="h-2.5 w-2.5 rounded-full bg-[#28c840]/80"></span>
      <span class="ml-3 text-[11px] text-muted-foreground/70">fasolt — Study</span>
    </div>

    <!-- Body -->
    <div class="flex-1 px-4 py-4 sm:px-5 sm:py-5">
      <!-- Context bar -->
      <div class="mb-3 flex items-center justify-between text-[10px] text-muted-foreground">
        <div class="flex items-center gap-2">
          <span class="text-accent uppercase tracking-[0.15em]">Review</span>
          <span class="text-border">|</span>
          <span>{{ currentIndex + 1 }} of {{ totalInDeck }}</span>
        </div>
        <span class="hidden sm:flex items-center gap-1">
          <span class="rounded border border-border/60 px-1.5 py-0.5 text-[9px] font-mono">space</span>
          flip
        </span>
      </div>

      <!-- Progress meter -->
      <div class="mb-5 h-1 w-full overflow-hidden rounded bg-border/60">
        <div class="h-full bg-accent transition-all duration-500" :style="{ width: progressPct + '%' }"></div>
      </div>

      <!-- Card -->
      <div
        class="relative rounded border border-border/60 bg-background/60 p-5 transition-all duration-300"
        :class="isExiting ? 'opacity-0 -translate-y-1' : 'opacity-100 translate-y-0'"
        style="min-height: 140px;"
      >
        <div class="text-[10px] uppercase tracking-[0.2em] mb-3 text-accent/70">
          {{ isFlipped ? 'Answer' : 'Question' }}
        </div>
        <div
          class="text-[13.5px] leading-relaxed transition-colors duration-300"
          :class="isFlipped ? 'text-muted-foreground' : 'text-foreground'"
        >
          {{ card.front }}
        </div>
        <div
          v-if="isFlipped"
          class="mt-3 text-[13.5px] leading-relaxed text-foreground"
          style="animation: fade-in 350ms ease-out;"
        >
          {{ card.back }}
        </div>
      </div>

      <!-- Below card -->
      <div class="mt-4 transition-opacity duration-300" :class="isExiting ? 'opacity-0' : 'opacity-100'">
        <div v-if="!showRating" class="text-center text-[11px] text-muted-foreground">
          Click the card or press
          <span class="rounded border border-border/60 px-1.5 py-0.5 text-[9px] font-mono">space</span>
          to reveal the answer
        </div>
        <div v-else class="flex flex-wrap justify-center gap-2" style="animation: fade-in 250ms ease-out;">
          <button
            v-for="(r, i) in [
              { label: 'Again', cls: 'border-destructive/50 text-destructive', activeCls: 'bg-destructive/10' },
              { label: 'Hard', cls: 'border-warning/50 text-warning', activeCls: 'bg-warning/10' },
              { label: 'Good', cls: 'border-success/50 text-success', activeCls: 'bg-success/10' },
              { label: 'Easy', cls: 'border-accent/50 text-accent', activeCls: 'bg-accent/10' },
            ]"
            :key="r.label"
            class="flex-1 min-w-[70px] rounded border px-3 py-2 text-[11px] font-medium transition-all"
            :class="[r.cls, highlightedRating === i ? r.activeCls : 'bg-transparent']"
            type="button"
            tabindex="-1"
          >
            {{ r.label }}
            <span class="ml-1 text-[9px] opacity-50">{{ i + 1 }}</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@keyframes fade-in {
  from { opacity: 0; transform: translateY(4px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>
