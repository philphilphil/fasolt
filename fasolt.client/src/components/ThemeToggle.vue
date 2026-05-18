<script setup lang="ts">
import { useDarkMode } from '@/composables/useDarkMode'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Sun, Moon, Monitor, Check } from 'lucide-vue-next'

const { mode, setMode } = useDarkMode()

const labels = { light: 'Light', dark: 'Dark', auto: 'Auto' } as const
</script>

<template>
  <DropdownMenu>
    <DropdownMenuTrigger as-child>
      <button
        class="theme-btn"
        :aria-label="`Theme: ${labels[mode]} (click to change)`"
      >
        <Sun v-if="mode === 'light'" :size="15" />
        <Moon v-else-if="mode === 'dark'" :size="15" />
        <Monitor v-else :size="15" />
      </button>
    </DropdownMenuTrigger>
    <DropdownMenuContent align="end" class="w-36">
      <DropdownMenuItem class="theme-item" @click="setMode('light')">
        <Sun :size="14" />
        <span>Light</span>
        <Check v-if="mode === 'light'" :size="13" class="theme-check" />
      </DropdownMenuItem>
      <DropdownMenuItem class="theme-item" @click="setMode('dark')">
        <Moon :size="14" />
        <span>Dark</span>
        <Check v-if="mode === 'dark'" :size="13" class="theme-check" />
      </DropdownMenuItem>
      <DropdownMenuItem class="theme-item" @click="setMode('auto')">
        <Monitor :size="14" />
        <span>Auto</span>
        <Check v-if="mode === 'auto'" :size="13" class="theme-check" />
      </DropdownMenuItem>
    </DropdownMenuContent>
  </DropdownMenu>
</template>

<style scoped>
.theme-btn {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  background: transparent;
  border: 1px solid transparent;
  border-radius: 6px;
  color: var(--ink-1);
  cursor: pointer;
  transition: background .12s, border-color .12s, color .12s;
}
.theme-btn:hover {
  background: var(--paper-1);
  border-color: var(--rule-1);
  color: var(--ink-0);
}
.theme-item {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}
.theme-check {
  margin-left: auto;
  color: var(--accent);
}
</style>
