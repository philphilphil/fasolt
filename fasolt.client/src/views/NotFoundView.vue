<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-paper-0">
    <div class="text-center">
      <p class="fa-num text-5xl font-semibold text-ink-3">404</p>
      <p class="mt-3 text-xs text-ink-2">Page not found</p>
      <RouterLink to="/dashboard" class="fa-btn mt-6 inline-flex">Back to dashboard</RouterLink>
    </div>
  </div>
</template>
