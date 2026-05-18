<script setup lang="ts">
defineProps<{ keys: string }>()
</script>

<template>
  <kbd class="fa-kbd">{{ keys }}</kbd>
</template>
