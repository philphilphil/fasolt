<script setup lang="ts">
import type { ReviewRating } from '@/types'

defineEmits<{ rate: [rating: ReviewRating] }>()

const ratings: { key: string; label: string; rating: ReviewRating; cssColor: string; sub: string }[] = [
  { key: '1', label: 'Again', rating: 'again', cssColor: 'var(--c-again)', sub: '<10m' },
  { key: '2', label: 'Hard',  rating: 'hard',  cssColor: 'var(--c-hard)',  sub: '~1d' },
  { key: '3', label: 'Good',  rating: 'good',  cssColor: 'var(--c-good)',  sub: '2d' },
  { key: '4', label: 'Easy',  rating: 'easy',  cssColor: 'var(--c-easy)',  sub: '5d' },
]
</script>

<template>
  <div class="rating-bar">
    <button
      v-for="r in ratings"
      :key="r.rating"
      class="rating-btn"
      :style="{ '--rcolor': r.cssColor }"
      @click="$emit('rate', r.rating)"
    >
      <span class="rating-top">
        <span class="rating-kbd">{{ r.key }}</span>
        <span class="rating-label">{{ r.label }}</span>
      </span>
      <span class="rating-sub fa-mono">next · {{ r.sub }}</span>
    </button>
  </div>
</template>

<style scoped>
.rating-bar {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 10px;
}
@media (max-width: 640px) {
  .rating-bar { gap: 6px; }
}
.rating-btn {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 8px;
  padding: 14px 16px;
  background: var(--paper-1);
  border: 1px solid var(--rule-1);
  border-bottom: 3px solid var(--rcolor);
  border-radius: 8px;
  text-align: left;
  cursor: pointer;
  font: inherit;
  color: inherit;
  transition: background .12s, border-color .12s, transform .08s;
}
.rating-btn:hover {
  background: var(--paper-2);
  border-color: var(--rcolor);
  border-bottom-color: var(--rcolor);
}
.rating-btn:active { transform: translateY(1px); }
.rating-top {
  display: flex;
  align-items: center;
  gap: 10px;
}
.rating-kbd {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 22px;
  height: 22px;
  padding: 0 6px;
  font-family: 'Geist Mono', monospace;
  font-size: 11px;
  font-weight: 600;
  color: var(--rcolor);
  background: var(--paper-1);
  border: 1px solid var(--rcolor);
  border-bottom-width: 2px;
  border-radius: 4px;
}
.rating-label {
  font-size: 17px;
  font-weight: 600;
  color: var(--rcolor);
  letter-spacing: -0.01em;
}
.rating-sub {
  font-size: 11px;
  color: var(--ink-2);
}
@media (max-width: 640px) {
  .rating-btn { padding: 10px 10px; }
  .rating-label { font-size: 14px; }
}
</style>
