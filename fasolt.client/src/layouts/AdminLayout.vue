<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, RouterLink, RouterView } from 'vue-router'

const route = useRoute()

const sections = [
  { label: 'Overview', to: '/admin/overview', icon: '▦' },
  { label: 'Users', to: '/admin/users', icon: '☲' },
  { label: 'Activity', to: '/admin/logs', icon: '◫' },
]

const activePath = computed(() => route.path)
</script>

<template>
  <div class="space-y-4">
    <div>
      <h1 class="text-2xl font-bold tracking-tight">Admin</h1>
      <p class="text-muted-foreground">Manage users and monitor usage.</p>
    </div>

    <div class="flex flex-col gap-6 md:flex-row">
      <aside class="md:w-56 md:shrink-0">
        <nav class="flex gap-1 overflow-x-auto md:flex-col md:gap-0.5">
          <RouterLink
            v-for="s in sections"
            :key="s.to"
            :to="s.to"
            :class="[
              'flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors whitespace-nowrap',
              activePath === s.to || activePath.startsWith(s.to + '/')
                ? 'bg-muted font-medium text-foreground'
                : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground',
            ]"
          >
            <span class="text-base leading-none" aria-hidden>{{ s.icon }}</span>
            <span>{{ s.label }}</span>
          </RouterLink>
        </nav>
      </aside>

      <section class="min-w-0 flex-1">
        <RouterView />
      </section>
    </div>
  </div>
</template>
