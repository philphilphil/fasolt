<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import TopBar from '@/components/TopBar.vue'
import BottomNav from '@/components/BottomNav.vue'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useDarkMode } from '@/composables/useDarkMode'
import { useAuthStore } from '@/stores/auth'

useDarkMode()

const route = useRoute()
const auth = useAuthStore()
const version = __APP_VERSION__

const tabs = computed(() => {
  const items = [
    { label: 'Study', value: '/study' },
    { label: 'Cards', value: '/cards' },
    { label: 'Decks', value: '/decks' },
    { label: 'Sources', value: '/sources' },
    { label: 'MCP', value: '/mcp-setup' },
    { label: 'Settings', value: '/settings' },
  ]
  if (auth.isAdmin) {
    items.push({ label: 'Admin', value: '/admin' })
  }
  return items
})

const activeTab = computed(() => {
  const path = route.path
  const match = tabs.value.find(t => path === t.value || path.startsWith(t.value + '/'))
  return match?.value ?? path
})
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <TopBar />
    <nav class="hidden border-b border-border px-5 sm:block">
      <Tabs :model-value="activeTab">
        <TabsList class="h-auto gap-0 rounded-none bg-transparent p-0">
          <TabsTrigger
            v-for="tab in tabs"
            :key="tab.value"
            :value="tab.value"
            as-child
          >
            <RouterLink
              :to="tab.value"
              class="relative rounded-none border-b-2 border-transparent px-3.5 py-2.5 text-xs text-muted-foreground transition-colors hover:text-foreground data-[state=active]:border-foreground data-[state=active]:text-foreground data-[state=active]:font-semibold"
            >
              {{ tab.label }}
            </RouterLink>
          </TabsTrigger>
        </TabsList>
      </Tabs>
    </nav>
    <main class="flex-1 px-4 pb-20 pt-6 sm:px-5 sm:pb-6">
      <div class="mx-auto max-w-[1200px]">
        <slot />
      </div>
    </main>
    <footer class="hidden border-t border-border/40 px-5 py-2 text-[10px] text-muted-foreground/50 sm:flex sm:items-center sm:justify-between">
      <span>fasolt v{{ version }}</span>
      <div class="flex items-center gap-3">
        <a href="https://github.com/philphilphil/fasolt" target="_blank" rel="noopener noreferrer" class="hover:text-muted-foreground transition-colors" aria-label="GitHub">
          <svg class="size-3.5" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z"/></svg>
        </a>
        <RouterLink to="/privacy" class="hover:text-muted-foreground">Privacy Policy</RouterLink>
      </div>
    </footer>
    <BottomNav />
  </div>
</template>
