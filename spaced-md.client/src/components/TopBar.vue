<script setup lang="ts">
import { computed } from 'vue'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import KbdHint from '@/components/KbdHint.vue'
import { useDarkMode } from '@/composables/useDarkMode'

const { theme, toggle } = useDarkMode()

const themeIcon = computed(() => {
  if (theme.value === 'dark') return '☾'
  if (theme.value === 'light') return '☀'
  return '◑'
})

const themeLabel = computed(() => {
  if (theme.value === 'dark') return 'Dark'
  if (theme.value === 'light') return 'Light'
  return 'System'
})
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
    <div class="flex items-center gap-2">
      <Button
        variant="ghost"
        size="sm"
        class="h-8 gap-1.5 text-xs text-muted-foreground"
        :aria-label="`Theme: ${themeLabel}. Click to change.`"
        @click="toggle"
      >
        <span class="text-sm">{{ themeIcon }}</span>
        <span class="hidden sm:inline">{{ themeLabel }}</span>
      </Button>
      <div class="h-8 w-8 rounded-full bg-secondary" />
    </div>
  </header>
</template>
