<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import {
  Brain, BarChart3, FileText, Layers, FolderOpen, Bot, Settings, ShieldCheck,
} from 'lucide-vue-next'

const route = useRoute()
const auth = useAuthStore()

const tabs = computed(() => {
  const items = [
    { name: 'Study', path: '/study', Icon: Brain },
    { name: 'Progress', path: '/progress', Icon: BarChart3 },
    { name: 'Cards', path: '/cards', Icon: FileText },
    { name: 'Decks', path: '/decks', Icon: Layers },
    { name: 'Sources', path: '/sources', Icon: FolderOpen },
    { name: 'MCP', path: '/mcp-setup', Icon: Bot },
    { name: 'Settings', path: '/settings', Icon: Settings },
  ]
  if (auth.isAdmin) items.push({ name: 'Admin', path: '/admin', Icon: ShieldCheck })
  return items
})

function isActive(path: string) { return route.path === path || route.path.startsWith(path + '/') }
</script>

<template>
  <nav class="bottom-nav">
    <div class="bottom-nav-scroll">
      <RouterLink
        v-for="tab in tabs"
        :key="tab.path"
        :to="tab.path"
        class="bnav-tab"
        :class="{ 'is-active': isActive(tab.path) }"
      >
        <component :is="tab.Icon" :size="18" />
        <span class="bnav-label">{{ tab.name }}</span>
      </RouterLink>
    </div>
  </nav>
</template>

<style scoped>
.bottom-nav {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  border-top: 1px solid var(--rule-1);
  background: color-mix(in oklch, var(--paper-0) 92%, transparent);
  backdrop-filter: blur(8px);
  padding: 6px 4px 8px;
  z-index: 40;
}
@media (min-width: 640px) {
  .bottom-nav { display: none; }
}
.bottom-nav-scroll {
  display: flex;
  align-items: stretch;
  justify-content: space-between;
  gap: 2px;
  overflow-x: auto;
  scrollbar-width: none;
}
.bottom-nav-scroll::-webkit-scrollbar { display: none; }
.bnav-tab {
  flex: 1 0 auto;
  min-width: 56px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 3px;
  padding: 4px 6px;
  color: var(--ink-2);
  text-decoration: none;
  border-radius: 6px;
  position: relative;
  transition: color .12s;
}
.bnav-tab:hover { color: var(--ink-0); }
.bnav-tab.is-active { color: var(--accent); }
.bnav-tab.is-active::before {
  content: '';
  position: absolute;
  top: -7px;
  left: 50%;
  transform: translateX(-50%);
  width: 24px;
  height: 2px;
  background: var(--accent);
  border-radius: 0 0 2px 2px;
}
.bnav-label {
  font-size: 10px;
  letter-spacing: -0.005em;
  font-weight: 500;
}
</style>
