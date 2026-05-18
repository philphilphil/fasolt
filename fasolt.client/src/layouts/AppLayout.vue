<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import TopBar from '@/components/TopBar.vue'
import BottomNav from '@/components/BottomNav.vue'
import AppFooter from '@/components/AppFooter.vue'
import { useDarkMode } from '@/composables/useDarkMode'
import { useAuthStore } from '@/stores/auth'

useDarkMode()

const route = useRoute()
const auth = useAuthStore()

type Tab = { label: string; value: string } | { separator: true }

const tabs = computed<Tab[]>(() => {
  const items: Tab[] = [
    { label: 'Study', value: '/study' },
    { label: 'Progress', value: '/progress' },
    { label: 'Cards', value: '/cards' },
    { label: 'Decks', value: '/decks' },
    { label: 'Sources', value: '/sources' },
    { separator: true },
    { label: 'MCP', value: '/mcp-setup' },
    { label: 'Settings', value: '/settings' },
  ]
  if (auth.isAdmin) items.push({ label: 'Admin', value: '/admin' })
  return items
})

const activeTab = computed(() => {
  const path = route.path
  const match = tabs.value.find(t => 'value' in t && (path === t.value || path.startsWith(t.value + '/')))
  return match && 'value' in match ? match.value : path
})

function isMuted(tab: Tab): boolean {
  if (!('value' in tab)) return false
  return ['/mcp-setup', '/settings', '/admin'].some(v => tab.value === v)
}
</script>

<template>
  <div class="app-shell">
    <TopBar />
    <nav class="primary-nav">
      <div class="primary-nav-inner">
        <template v-for="(tab, i) in tabs" :key="i">
          <div v-if="'separator' in tab" class="nav-sep" />
          <RouterLink
            v-else
            :to="tab.value"
            class="nav-tab"
            :class="{
              'is-active': activeTab === tab.value,
              'is-muted': isMuted(tab),
            }"
          >
            <span>{{ tab.label }}</span>
          </RouterLink>
        </template>
        <div class="nav-spacer" />
      </div>
    </nav>
    <main class="app-main">
      <div :class="route.meta.fullWidth ? 'main-full' : 'main-clamped'">
        <slot />
      </div>
    </main>
    <div class="hidden sm:block">
      <AppFooter />
    </div>
    <BottomNav />
  </div>
</template>

<style scoped>
.app-shell {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  background: var(--paper-0);
  color: var(--ink-0);
}
.primary-nav {
  display: none;
  border-bottom: 1px solid var(--rule-1);
  background: var(--paper-0);
}
@media (min-width: 640px) {
  .primary-nav { display: block; }
}
.primary-nav-inner {
  display: flex;
  align-items: stretch;
  padding: 0 20px;
  height: 40px;
  gap: 0;
}
.nav-tab {
  position: relative;
  display: flex;
  align-items: center;
  padding: 0 14px;
  font-size: 13px;
  font-weight: 500;
  color: var(--ink-1);
  text-decoration: none;
  transition: color .12s;
}
.nav-tab:hover { color: var(--ink-0); }
.nav-tab.is-muted { color: var(--ink-2); }
.nav-tab.is-muted:hover { color: var(--ink-0); }
.nav-tab.is-active {
  color: var(--ink-0);
  font-weight: 600;
}
.nav-tab.is-active::after {
  content: '';
  position: absolute;
  left: 14px;
  right: 14px;
  bottom: -1px;
  height: 2px;
  background: var(--accent);
  border-radius: 2px 2px 0 0;
}
.nav-sep {
  width: 1px;
  background: var(--rule-1);
  margin: 8px 12px;
}
.nav-spacer { flex: 1; }
.app-main {
  flex: 1;
  padding: 0 16px 80px;
}
@media (min-width: 640px) {
  .app-main { padding: 0 20px 24px; }
}
.main-clamped {
  margin: 0 auto;
  max-width: 1200px;
  width: 100%;
}
.main-full { width: 100%; }
</style>
