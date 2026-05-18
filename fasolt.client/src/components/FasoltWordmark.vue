<script setup lang="ts">
import FasoltMark from './FasoltMark.vue'
withDefaults(defineProps<{ size?: number }>(), { size: 20 })
</script>

<template>
  <span class="fasolt-wordmark">
    <FasoltMark :size="size" />
    <span class="wordmark-text">fasolt</span>
    <span class="wordmark-dot" aria-hidden="true" />
  </span>
</template>

<style scoped>
.fasolt-wordmark {
  display: inline-flex;
  align-items: center;
  gap: 9px;
  color: var(--ink-0);
}
.wordmark-text {
  font-family: 'Geist', system-ui, sans-serif;
  font-size: 17px;
  font-weight: 600;
  letter-spacing: -0.045em;
  line-height: 1;
}
.wordmark-dot {
  width: 4px;
  height: 4px;
  border-radius: 1px;
  background: var(--accent);
  margin-left: 4px;
  margin-bottom: 1px;
  align-self: flex-end;
}
</style>
