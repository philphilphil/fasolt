<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { apiFetch } from '@/api/client'

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
        { label: 'Sign-ups · 7 days', value: s.registrationsLast7d, sub: `${s.registrationsLast30d} in 30 days`, tone: 'accent' },
      ],
    },
  ] as Array<{
    title: string
    cards: Array<{ label: string; value: number; sub?: string; tone?: 'warn' | 'accent' }>
  }>
})

function fmt(n: number) {
  return new Intl.NumberFormat().format(n)
}
</script>

<template>
  <div class="overview">
    <header class="ov-head">
      <div>
        <h2 class="ov-title">Overview</h2>
        <p class="ov-sub">A snapshot of users, content, and recent activity.</p>
      </div>
      <button class="fa-btn" :disabled="isLoading" @click="fetchStats">Refresh</button>
    </header>

    <div v-if="errorMessage" class="ov-error">{{ errorMessage }}</div>

    <div v-if="isLoading && !stats" class="ov-loading">Loading…</div>

    <div v-for="group in groups" :key="group.title" class="ov-group">
      <h3 class="fa-cap">{{ group.title }}</h3>
      <div class="ov-stats" :class="`ov-stats-${Math.min(group.cards.length, 3)}`">
        <div v-for="c in group.cards" :key="c.label" class="ov-stat">
          <div class="ov-stat-label">{{ c.label }}</div>
          <div
            class="ov-stat-value fa-num"
            :class="{ 'is-warn': c.tone === 'warn', 'is-accent': c.tone === 'accent' }"
          >
            {{ fmt(c.value) }}
          </div>
          <div v-if="c.sub" class="ov-stat-sub">{{ c.sub }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.overview { display: flex; flex-direction: column; gap: 28px; }

.ov-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}
.ov-title {
  font-size: 18px;
  font-weight: 600;
  letter-spacing: -0.01em;
  color: var(--ink-0);
  margin: 0;
}
.ov-sub { margin: 4px 0 0; font-size: 13px; color: var(--ink-2); }

.ov-error {
  border: 1px solid var(--c-again);
  background: transparent;
  border-radius: 9px;
  padding: 10px 14px;
  font-size: 13px;
  color: var(--c-again);
}
.ov-loading { font-size: 13px; color: var(--ink-2); }

.ov-group { display: flex; flex-direction: column; gap: 10px; }

.ov-stats {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1px;
  background: var(--rule-1);
  border: 1px solid var(--rule-1);
  border-radius: 12px;
  overflow: hidden;
}
@media (min-width: 640px) {
  .ov-stats-2 { grid-template-columns: 1fr 1fr; }
  .ov-stats-3 { grid-template-columns: 1fr 1fr; }
}
@media (min-width: 1024px) {
  .ov-stats-3 { grid-template-columns: 1fr 1fr 1fr; }
}

.ov-stat {
  background: var(--paper-1);
  padding: 16px 18px;
}
.ov-stat-label { font-size: 12.5px; color: var(--ink-2); }
.ov-stat-value {
  margin-top: 6px;
  font-size: 30px;
  font-weight: 600;
  letter-spacing: -0.02em;
  color: var(--ink-0);
}
.ov-stat-value.is-warn { color: var(--c-again); }
.ov-stat-value.is-accent { color: var(--accent-text); }
.ov-stat-sub { margin-top: 4px; font-size: 12px; color: var(--ink-2); }
</style>
