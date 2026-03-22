<script setup lang="ts">
defineProps<{ total: number; current: number }>()
</script>

<template>
  <div class="flex gap-0.5" role="progressbar" :aria-valuenow="current" :aria-valuemax="total">
    <div
      v-for="i in total"
      :key="i"
      class="h-[3px] flex-1 rounded-sm transition-all duration-300"
      :class="{
        'bg-accent shadow-[0_0_4px_hsl(var(--accent)/0.4)]': i <= current,
        'bg-accent/30': i === current + 1,
        'bg-border': i > current + 1,
      }"
    />
  </div>
</template>
