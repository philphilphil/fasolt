<script setup lang="ts">
withDefaults(defineProps<{ size?: number }>(), { size: 20 })
</script>

<template>
  <svg
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.6"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <rect x="3" y="5" width="16" height="11" rx="2" />
    <rect x="6" y="8" width="16" height="11" rx="2" fill="var(--paper-0)" />
    <line x1="9" y1="13" x2="16" y2="13" :stroke="'var(--accent)'" stroke-width="2" />
  </svg>
</template>
