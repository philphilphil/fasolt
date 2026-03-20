<script setup lang="ts">
import { Input } from '@/components/ui/input'
import KbdHint from '@/components/KbdHint.vue'
</script>

<template>
  <header class="flex items-center justify-between border-b border-border px-5 py-3">
    <span class="font-mono text-[13px] font-bold text-foreground tracking-tight">
      spaced-md
    </span>
    <div class="relative hidden sm:block">
      <Input
        type="text"
        placeholder="Search cards, files…"
        class="h-8 w-[200px] bg-secondary pl-8 text-xs"
        readonly
      />
      <div class="absolute left-2 top-1/2 -translate-y-1/2">
        <KbdHint keys="⌘K" />
      </div>
    </div>
    <div class="h-8 w-8 rounded-full bg-secondary" />
  </header>
</template>
