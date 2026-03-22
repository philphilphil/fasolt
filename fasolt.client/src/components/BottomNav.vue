<script setup lang="ts">
import { useRoute } from 'vue-router'

const route = useRoute()

const tabs = [
  { name: 'Dashboard', path: '/dashboard', icon: '▦' },
  { name: 'Sources', path: '/sources', icon: '◫' },
  { name: 'Cards', path: '/cards', icon: '▤' },
  { name: 'Decks', path: '/decks', icon: '⊞' },
  { name: 'MCP', path: '/mcp', icon: '⏚' },
  { name: 'Settings', path: '/settings', icon: '⚙' },
]

function isActive(path: string) {
  return route.path === path
}
</script>

<template>
  <nav class="fixed bottom-0 left-0 right-0 flex items-center justify-around border-t border-border bg-background py-2.5 sm:hidden">
    <RouterLink
      v-for="tab in tabs"
      :key="tab.path"
      :to="tab.path"
      class="flex flex-col items-center gap-0.5 px-3 py-1 text-xs"
      :class="isActive(tab.path) ? 'text-accent' : 'text-muted-foreground'"
    >
      <span class="text-xl">{{ tab.icon }}</span>
      <span>{{ tab.name }}</span>
    </RouterLink>
  </nav>
</template>
