<script setup lang="ts">
defineProps<{ keys: string }>()
</script>

<template>
  <kbd class="inline-flex items-center justify-center rounded bg-secondary px-1.5 py-0.5 font-mono text-[10px] text-muted-foreground">
    {{ keys }}
  </kbd>
</template>
