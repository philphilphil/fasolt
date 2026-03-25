<script setup lang="ts">
import { useDarkMode } from '@/composables/useDarkMode'
useDarkMode()
</script>

<template>
  <div class="relative flex min-h-screen items-center justify-center bg-background px-4">
    <div class="pointer-events-none fixed inset-0 bg-grid bg-grid-fade opacity-30" />
    <div class="relative w-full max-w-sm">
      <div class="mb-8 flex flex-col items-center gap-2">
        <img src="/logo.svg" alt="fasolt" class="h-12 w-12 object-contain" />
        <span class="text-base font-bold text-foreground tracking-tight">fasolt</span>
      </div>
      <slot />
    </div>
  </div>
</template>
