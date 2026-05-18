<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(defineProps<{
  totalCards: number
  ratingCounts: { again: number; hard: number; good: number; easy: number }
  skippedCount?: number
  mode?: 'normal' | 'cram'
}>(), { mode: 'normal' })

defineEmits<{ done: [] }>()

const breakdown = computed(() => [
  { label: 'Again', n: props.ratingCounts.again, cssColor: 'var(--c-again)' },
  { label: 'Hard',  n: props.ratingCounts.hard,  cssColor: 'var(--c-hard)' },
  { label: 'Good',  n: props.ratingCounts.good,  cssColor: 'var(--c-good)' },
  { label: 'Easy',  n: props.ratingCounts.easy,  cssColor: 'var(--c-easy)' },
])

const total = computed(() => breakdown.value.reduce((a, b) => a + b.n, 0) || 1)
</script>

<template>
  <div class="session-complete">
    <section class="hero">
      <span class="fa-cap accent-label">{{ mode === 'cram' ? 'Cram complete' : 'Session complete' }}</span>
      <h1 class="hero-title">Well done.</h1>
      <p class="hero-sub">
        You reviewed <strong>{{ totalCards }} {{ totalCards === 1 ? 'card' : 'cards' }}</strong>{{ mode === 'cram' ? ' in cram mode' : '' }}.
        <template v-if="skippedCount"> {{ skippedCount }} skipped.</template>
      </p>
    </section>

    <section v-if="mode !== 'cram'" class="fa-surface breakdown">
      <div class="breakdown-head">
        <span class="fa-cap">Breakdown</span>
        <span class="fa-mono breakdown-meta">{{ totalCards }} total</span>
      </div>
      <div class="breakdown-bar">
        <div
          v-for="b in breakdown"
          :key="b.label"
          class="breakdown-bar-seg"
          :style="{ flex: b.n || 0.0001, background: b.cssColor, opacity: b.n ? 1 : 0.15 }"
          :title="`${b.label}: ${b.n}`"
        />
      </div>
      <div class="breakdown-grid">
        <div v-for="b in breakdown" :key="b.label" class="breakdown-cell">
          <div class="breakdown-row">
            <span class="breakdown-dot" :style="{ background: b.cssColor }" />
            <span class="fa-cap" :style="{ color: b.cssColor }">{{ b.label }}</span>
          </div>
          <div class="breakdown-value-row">
            <span class="fa-num breakdown-num">{{ b.n }}</span>
            <span class="fa-mono breakdown-pct">{{ Math.round((b.n / total) * 100) }}%</span>
          </div>
        </div>
      </div>
    </section>

    <div class="session-actions">
      <button class="fa-btn fa-btn-primary" @click="$emit('done')">
        {{ mode === 'cram' ? 'Done' : 'Back to Study' }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.session-complete {
  width: 100%;
  max-width: 720px;
  margin: 40px auto;
  display: flex;
  flex-direction: column;
  gap: 28px;
}
.hero { display: flex; flex-direction: column; }
.accent-label { color: var(--accent); }
.hero-title {
  font-family: 'Instrument Serif', serif;
  font-size: 72px;
  line-height: 1;
  letter-spacing: -0.03em;
  margin: 12px 0 0;
  color: var(--ink-0);
}
@media (max-width: 600px) { .hero-title { font-size: 52px; } }
.hero-sub {
  margin-top: 14px;
  font-size: 16px;
  line-height: 1.5;
  color: var(--ink-1);
  max-width: 540px;
}
.hero-sub strong { color: var(--ink-0); font-weight: 600; }
.breakdown { padding: 20px 22px; }
.breakdown-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
}
.breakdown-meta { font-size: 11px; color: var(--ink-2); }
.breakdown-bar {
  display: flex;
  height: 8px;
  margin-top: 14px;
  border-radius: 4px;
  overflow: hidden;
  background: var(--paper-2);
}
.breakdown-bar-seg { min-width: 0; }
.breakdown-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
  margin-top: 18px;
}
@media (max-width: 500px) {
  .breakdown-grid { grid-template-columns: repeat(2, 1fr); }
}
.breakdown-cell { display: flex; flex-direction: column; gap: 4px; }
.breakdown-row {
  display: flex;
  align-items: center;
  gap: 6px;
}
.breakdown-dot {
  width: 6px;
  height: 6px;
  border-radius: 2px;
}
.breakdown-cell .fa-cap { font-size: 9px; }
.breakdown-value-row {
  display: flex;
  align-items: baseline;
  gap: 6px;
  margin-top: 4px;
}
.breakdown-num {
  font-size: 26px;
  line-height: 1;
  font-weight: 500;
}
.breakdown-pct {
  font-size: 11px;
  color: var(--ink-2);
}
.session-actions {
  display: flex;
  gap: 10px;
}
</style>
