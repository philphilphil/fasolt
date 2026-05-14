<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { apiFetch } from '@/api/client'
import type { Progress, DailyActivity } from '@/types'
import { Card, CardContent } from '@/components/ui/card'

const progress = ref<Progress | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

onMounted(async () => {
  try {
    progress.value = await apiFetch<Progress>('/review/progress?days=30')
  } catch {
    error.value = 'Could not load progress.'
  } finally {
    loading.value = false
  }
})

const maxCount = computed(() => {
  if (!progress.value) return 0
  return Math.max(1, ...progress.value.dailyActivity.map(d => d.count))
})

function dayClass(d: DailyActivity, isLast: boolean): string {
  if (isLast) return 'bg-blue-500'
  if (d.count > 0) return 'bg-emerald-500'
  if (d.hadDue) return 'bg-rose-500/30'
  return 'bg-muted'
}

function dayHeight(count: number): string {
  if (count === 0) return '6px'
  const min = 22
  const max = 64
  const pct = count / maxCount.value
  return `${Math.round(min + pct * (max - min))}px`
}

function dayLabel(d: DailyActivity, isLast: boolean): string {
  const dateStr = new Date(d.date).toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' })
  if (isLast) return `Today — ${dateStr}: ${d.count} answered`
  if (d.count > 0) return `${dateStr}: ${d.count} answered`
  if (d.hadDue) return `${dateStr}: missed (had due cards)`
  return `${dateStr}: rest day`
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-lg font-bold tracking-tight">Progress</h1>
      <p class="text-sm text-muted-foreground mt-1">Your study momentum and recent activity.</p>
    </div>

    <div v-if="loading" class="py-12 text-center text-xs text-muted-foreground">Loading...</div>
    <div v-else-if="error" class="py-12 text-center text-xs text-destructive">{{ error }}</div>

    <template v-else-if="progress">
      <!-- Top stat cards -->
      <div class="grid gap-2.5 grid-cols-2 sm:grid-cols-4">
        <Card class="border-border/60">
          <CardContent class="p-4">
            <div class="text-[11px] uppercase tracking-wide text-muted-foreground">Current streak</div>
            <div class="mt-1 text-2xl font-bold tracking-tight">
              <span v-if="progress.currentStreak > 0">🔥</span>
              {{ progress.currentStreak }}
              <span class="ml-0.5 text-xs font-normal text-muted-foreground">days</span>
            </div>
          </CardContent>
        </Card>
        <Card class="border-border/60">
          <CardContent class="p-4">
            <div class="text-[11px] uppercase tracking-wide text-muted-foreground">Best streak</div>
            <div class="mt-1 text-2xl font-bold tracking-tight">
              {{ progress.bestStreak }}
              <span class="ml-0.5 text-xs font-normal text-muted-foreground">days</span>
            </div>
          </CardContent>
        </Card>
        <Card class="border-border/60">
          <CardContent class="p-4">
            <div class="text-[11px] uppercase tracking-wide text-muted-foreground">Total answered</div>
            <div class="mt-1 text-2xl font-bold tracking-tight">{{ progress.totalAnswered }}</div>
          </CardContent>
        </Card>
        <Card class="border-border/60">
          <CardContent class="p-4">
            <div class="text-[11px] uppercase tracking-wide text-muted-foreground">Today</div>
            <div class="mt-1 text-2xl font-bold tracking-tight">{{ progress.answeredToday }}</div>
          </CardContent>
        </Card>
      </div>

      <!-- Period stats -->
      <div class="grid gap-2.5 grid-cols-2">
        <Card class="border-border/60">
          <CardContent class="p-4">
            <div class="text-[11px] uppercase tracking-wide text-muted-foreground">This week</div>
            <div class="mt-1 text-xl font-semibold tracking-tight">{{ progress.answeredThisWeek }}</div>
          </CardContent>
        </Card>
        <Card class="border-border/60">
          <CardContent class="p-4">
            <div class="text-[11px] uppercase tracking-wide text-muted-foreground">This month</div>
            <div class="mt-1 text-xl font-semibold tracking-tight">{{ progress.answeredThisMonth }}</div>
          </CardContent>
        </Card>
      </div>

      <!-- Activity chart -->
      <Card class="border-border/60">
        <CardContent class="p-4">
          <div class="text-[11px] uppercase tracking-wide text-muted-foreground mb-3">Last 30 days</div>
          <div class="flex h-[72px] items-end gap-[3px]">
            <div
              v-for="(d, i) in progress.dailyActivity"
              :key="d.date"
              class="relative flex-1 rounded-sm transition-colors flex items-center justify-center"
              :class="dayClass(d, i === progress.dailyActivity.length - 1)"
              :style="{ height: dayHeight(d.count) }"
              :title="dayLabel(d, i === progress.dailyActivity.length - 1)"
            >
              <span
                v-if="d.count > 0"
                class="text-[10px] font-semibold leading-none text-white tabular-nums"
              >{{ d.count }}</span>
            </div>
          </div>
          <div class="mt-3 flex flex-wrap gap-3 text-[11px] text-muted-foreground">
            <span class="flex items-center gap-1.5"><span class="inline-block h-2 w-2 rounded-sm bg-emerald-500" /> Studied</span>
            <span class="flex items-center gap-1.5"><span class="inline-block h-2 w-2 rounded-sm bg-rose-500/30" /> Missed</span>
            <span class="flex items-center gap-1.5"><span class="inline-block h-2 w-2 rounded-sm bg-muted" /> Rest day</span>
            <span class="flex items-center gap-1.5"><span class="inline-block h-2 w-2 rounded-sm bg-blue-500" /> Today</span>
          </div>
        </CardContent>
      </Card>

      <p v-if="progress.totalAnswered === 0" class="py-4 text-center text-xs text-muted-foreground">
        No reviews yet. Once you start studying, your activity shows up here.
      </p>
    </template>
  </div>
</template>
