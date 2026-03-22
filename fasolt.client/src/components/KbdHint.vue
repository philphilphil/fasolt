<script setup lang="ts">
defineProps<{ keys: string }>()
</script>

<template>
  <kbd class="inline-flex items-center justify-center rounded border border-accent/20 bg-accent/5 px-1.5 py-0.5 text-[10px] text-accent/80">
    {{ keys }}
  </kbd>
</template>
