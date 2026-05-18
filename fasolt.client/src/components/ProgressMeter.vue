<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ total: number; current: number }>()

const pct = computed(() => {
  if (props.total <= 0) return 0
  return Math.max(0, Math.min(100, Math.round((props.current / props.total) * 100)))
})

// Dotted ticks read great for medium queues but look chunky at the extremes —
// a 2-card queue becomes two huge slabs, a 200-card queue becomes a blur. Use
// dots only in the sweet spot; fall back to a continuous bar otherwise.
const useDots = computed(() => props.total >= 5 && props.total <= 60)
</script>

<template>
  <div class="progress-meter" role="progressbar" :aria-valuenow="current" :aria-valuemax="total">
    <div v-if="useDots" class="progress-track">
      <div
        v-for="i in total"
        :key="i"
        class="progress-tick"
        :class="{
          'is-done': i <= current,
          'is-next': i === current + 1,
        }"
      />
    </div>
    <div v-else class="progress-bar-track">
      <div class="progress-bar-fill" :style="{ width: `${pct}%` }" />
    </div>
    <span class="progress-pct fa-mono">{{ pct }}%</span>
  </div>
</template>

<style scoped>
.progress-meter {
  display: flex;
  align-items: center;
  gap: 10px;
}
.progress-track {
  display: flex;
  flex: 1;
  gap: 3px;
}
.progress-tick {
  flex: 1;
  height: 3px;
  border-radius: 2px;
  background: var(--rule-1);
  transition: background .2s;
}
.progress-tick.is-done { background: var(--accent); }
.progress-tick.is-next { background: var(--ink-3); opacity: .7; }
.progress-bar-track {
  flex: 1;
  height: 3px;
  border-radius: 2px;
  background: var(--rule-1);
  overflow: hidden;
}
.progress-bar-fill {
  height: 100%;
  background: var(--accent);
  border-radius: 2px;
  transition: width .25s cubic-bezier(.4,0,.2,1);
}
.progress-pct {
  font-size: 11px;
  color: var(--ink-2);
  min-width: 40px;
  text-align: right;
}
</style>
