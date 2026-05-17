<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { apiFetch } from '@/api/client'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

interface AdminStats {
  totalUsers: number
  lockedUsers: number
  usersWithPush: number
  totalCards: number
  totalDecks: number
  dueCards: number
  registrationsLast7d: number
  registrationsLast30d: number
}

const stats = ref<AdminStats | null>(null)
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)

async function fetchStats() {
  isLoading.value = true
  errorMessage.value = null
  try {
    stats.value = await apiFetch<AdminStats>('/admin/stats')
  } catch (e: any) {
    errorMessage.value = e.message ?? 'Failed to load stats'
  } finally {
    isLoading.value = false
  }
}

onMounted(fetchStats)

const groups = computed(() => {
  if (!stats.value) return []
  const s = stats.value
  return [
    {
      title: 'Users',
      cards: [
        { label: 'Total users', value: s.totalUsers },
        { label: 'With push enabled', value: s.usersWithPush },
        { label: 'Locked out', value: s.lockedUsers, tone: s.lockedUsers > 0 ? 'warn' : undefined },
      ],
    },
    {
      title: 'Content',
      cards: [
        { label: 'Total cards', value: s.totalCards },
        { label: 'Total decks', value: s.totalDecks },
        { label: 'Cards due now', value: s.dueCards },
      ],
    },
    {
      title: 'Activity',
      cards: [
        { label: 'Sign-ups · 7 days', value: s.registrationsLast7d, sub: `${s.registrationsLast30d} in 30 days` },
      ],
    },
  ] as Array<{
    title: string
    cards: Array<{ label: string; value: number; sub?: string; tone?: 'warn' }>
  }>
})

function fmt(n: number) {
  return new Intl.NumberFormat().format(n)
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <div>
        <h2 class="text-lg font-semibold tracking-tight">Overview</h2>
        <p class="text-sm text-muted-foreground">A snapshot of users, content, and recent activity.</p>
      </div>
      <Button variant="outline" size="sm" :disabled="isLoading" @click="fetchStats">Refresh</Button>
    </div>

    <div v-if="errorMessage" class="rounded-md border border-destructive bg-destructive/10 px-4 py-3 text-sm text-destructive">
      {{ errorMessage }}
    </div>

    <div v-if="isLoading && !stats" class="text-sm text-muted-foreground">Loading…</div>

    <div v-for="group in groups" :key="group.title" class="space-y-2">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{{ group.title }}</h3>
      <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <Card v-for="c in group.cards" :key="c.label">
          <CardHeader class="pb-2">
            <CardTitle class="text-xs font-medium text-muted-foreground">{{ c.label }}</CardTitle>
          </CardHeader>
          <CardContent>
            <div
              :class="[
                'text-3xl font-semibold tabular-nums',
                c.tone === 'warn' ? 'text-destructive' : 'text-foreground',
              ]"
            >
              {{ fmt(c.value) }}
            </div>
            <div v-if="c.sub" class="mt-1 text-xs text-muted-foreground">{{ c.sub }}</div>
          </CardContent>
        </Card>
      </div>
    </div>
  </div>
</template>
