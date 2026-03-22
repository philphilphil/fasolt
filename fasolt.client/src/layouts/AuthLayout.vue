<script setup lang="ts">
import { useDarkMode } from '@/composables/useDarkMode'
useDarkMode()
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-background px-4">
    <div class="w-full max-w-sm">
      <div class="mb-8 flex flex-col items-center gap-2">
        <img src="/logo.png" alt="fasolt" class="h-14 w-14 object-contain" />
        <span class="font-mono text-lg font-bold text-foreground tracking-tight">fasolt</span>
      </div>
      <slot />
    </div>
  </div>
</template>
