<script setup lang="ts">
import { useRoute } from 'vue-router'
import TopBar from '@/components/TopBar.vue'
import BottomNav from '@/components/BottomNav.vue'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useDarkMode } from '@/composables/useDarkMode'

useDarkMode()

const route = useRoute()

const tabs = [
  { label: 'Dashboard', value: '/' },
  { label: 'Files', value: '/files' },
  { label: 'Groups', value: '/groups' },
  { label: 'Settings', value: '/settings' },
]
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <TopBar />
    <nav class="hidden border-b border-border px-5 sm:block">
      <Tabs :model-value="route.path">
        <TabsList class="h-auto gap-0 rounded-none bg-transparent p-0">
          <TabsTrigger
            v-for="tab in tabs"
            :key="tab.value"
            :value="tab.value"
            as-child
          >
            <RouterLink
              :to="tab.value"
              class="relative rounded-none border-b-2 border-transparent px-3.5 py-2 text-xs text-muted-foreground transition-colors hover:text-foreground data-[state=active]:border-accent data-[state=active]:text-foreground"
            >
              {{ tab.label }}
            </RouterLink>
          </TabsTrigger>
        </TabsList>
      </Tabs>
    </nav>
    <main class="flex-1 px-4 pb-16 pt-5 sm:px-5 sm:pb-5">
      <div class="mx-auto max-w-[1200px]">
        <slot />
      </div>
    </main>
    <BottomNav />
  </div>
</template>
