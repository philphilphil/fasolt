<script setup lang="ts">
defineProps<{ total: number; current: number }>()
</script>

<template>
  <div class="flex gap-0.5" role="progressbar" :aria-valuenow="current" :aria-valuemax="total">
    <div
      v-for="i in total"
      :key="i"
      class="h-[3px] flex-1 rounded-sm transition-colors"
      :class="{
        'bg-accent': i <= current,
        'bg-accent/40': i === current + 1,
        'bg-border': i > current + 1,
      }"
    />
  </div>
</template>
