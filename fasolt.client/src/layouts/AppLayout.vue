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

const tabs = computed(() => {
  const items = [
    { label: 'Study', value: '/study' },
    { label: 'Cards', value: '/cards' },
    { label: 'Decks', value: '/decks' },
    { label: 'Sources', value: '/sources' },
    { label: 'MCP', value: '/mcp' },
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
    <BottomNav />
  </div>
</template>
