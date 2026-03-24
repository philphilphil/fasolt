<script setup lang="ts">
import { useRoute } from 'vue-router'

const route = useRoute()

const tabs = [
  { name: 'Study', path: '/study', icon: '◉' },
  { name: 'Cards', path: '/cards', icon: '▤' },
  { name: 'Decks', path: '/decks', icon: '⊞' },
  { name: 'Sources', path: '/sources', icon: '◫' },
  { name: 'MCP', path: '/mcp', icon: '⏚' },
  { name: 'Settings', path: '/settings', icon: '⚙' },
]

function isActive(path: string) {
  return route.path === path
}
</script>

<template>
  <nav class="fixed bottom-0 left-0 right-0 flex items-center justify-around border-t border-border bg-background/95 backdrop-blur-sm py-2 sm:hidden">
    <RouterLink
      v-for="tab in tabs"
      :key="tab.path"
      :to="tab.path"
      class="flex flex-col items-center gap-0.5 px-2 py-1"
      :class="isActive(tab.path) ? 'text-foreground' : 'text-muted-foreground'"
    >
      <span class="text-lg">{{ tab.icon }}</span>
      <span class="text-[10px]">{{ tab.name }}</span>
    </RouterLink>
  </nav>
</template>
