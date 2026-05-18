<script setup lang="ts">
import FasoltMark from './FasoltMark.vue'
const props = withDefaults(defineProps<{ size?: number }>(), { size: 20 })
</script>

<template>
  <span class="fasolt-wordmark" :style="{ '--mark-size': props.size + 'px' }">
    <FasoltMark :size="props.size" />
    <span class="wordmark-text">fasolt</span>
    <span class="wordmark-dot" aria-hidden="true" />
  </span>
</template>

<style scoped>
.fasolt-wordmark {
  --mark-size: 20px;
  display: inline-flex;
  align-items: center;
  gap: calc(var(--mark-size) * 0.45);
  color: var(--ink-0);
}
.wordmark-text {
  font-family: 'Geist', system-ui, sans-serif;
  font-size: calc(var(--mark-size) * 0.85);
  font-weight: 600;
  letter-spacing: -0.045em;
  line-height: 1;
}
.wordmark-dot {
  width: calc(var(--mark-size) * 0.2);
  height: calc(var(--mark-size) * 0.2);
  border-radius: calc(var(--mark-size) * 0.05);
  background: var(--accent);
  margin-left: calc(var(--mark-size) * 0.2);
  align-self: flex-end;
}
</style>
