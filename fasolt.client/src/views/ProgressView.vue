<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { apiFetch } from '@/api/client'
import type { Progress, DailyActivity } from '@/types'

type Period = 'year' | '90d' | '30d' | '7d'

const progress = ref<Progress | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const period = ref<Period>('year')

const periodOptions: { value: Period; label: string }[] = [
  { value: 'year', label: 'Year' },
  { value: '90d',  label: '90d' },
  { value: '30d',  label: '30d' },
  { value: '7d',   label: '7d' },
]

onMounted(async () => {
  try {
    progress.value = await apiFetch<Progress>('/review/progress?days=364')
  } catch {
    error.value = 'Could not load progress.'
  } finally {
    loading.value = false
  }
})

const daysWindow = computed(() => period.value === 'year' ? 364 : period.value === '90d' ? 90 : period.value === '30d' ? 30 : 7)

const windowActivity = computed<DailyActivity[]>(() => {
  if (!progress.value) return []
  return progress.value.dailyActivity.slice(-daysWindow.value)
})

const totalAnswered = computed(() => progress.value?.totalAnswered ?? 0)
const currentStreak = computed(() => progress.value?.currentStreak ?? 0)
const bestStreak = computed(() => progress.value?.bestStreak ?? 0)
const answeredThisMonth = computed(() => progress.value?.answeredThisMonth ?? 0)

const windowAnswered = computed(() => windowActivity.value.reduce((a, d) => a + d.count, 0))
const windowActiveDays = computed(() => windowActivity.value.filter(d => d.count > 0).length)
const windowMax = computed(() => Math.max(1, ...windowActivity.value.map(d => d.count)))
const windowBestDay = computed(() => {
  let best: DailyActivity | null = null
  for (const d of windowActivity.value) if (!best || d.count > best.count) best = d
  return best
})
const windowAvgPerActive = computed(() => windowActiveDays.value > 0 ? Math.round(windowAnswered.value / windowActiveDays.value) : 0)
const windowRestDays = computed(() => windowActivity.value.filter(d => d.count === 0).length)

function bucket(count: number): number {
  if (!count) return 0
  const max = windowMax.value
  if (count >= max * 0.85) return 4
  if (count >= max * 0.55) return 3
  if (count >= max * 0.3) return 2
  return 1
}

function cellColor(v: number): string {
  if (v === 0) return 'var(--paper-2)'
  if (v === 1) return 'oklch(0.85 0.07 50)'
  if (v === 2) return 'oklch(0.75 0.12 48)'
  if (v === 3) return 'oklch(0.65 0.15 42)'
  return 'oklch(0.56 0.18 38)'
}

// Heatmap layout: 53 columns x 7 rows for "year", aligned to today.
// We right-pad the data so the *last* filled cell is today, then prepend
// null cells until the grid is full — produces a clean column shape and
// the today marker always lands on a real day.
const HEATMAP_WEEKS = 53
type Cell = { day: DailyActivity | null; isToday: boolean }
const heatmapCells = computed<Cell[][]>(() => {
  const data = windowActivity.value
  const totalCells = HEATMAP_WEEKS * 7
  const padding = Math.max(0, totalCells - data.length)
  const flat: Cell[] = []
  for (let i = 0; i < padding; i++) flat.push({ day: null, isToday: false })
  for (let i = 0; i < data.length; i++) {
    flat.push({ day: data[i], isToday: i === data.length - 1 })
  }
  // Group into 53 columns of 7
  const cells: Cell[][] = []
  for (let w = 0; w < HEATMAP_WEEKS; w++) {
    cells.push(flat.slice(w * 7, w * 7 + 7))
  }
  return cells
})

const dayLabels = ['Mon', '', 'Wed', '', 'Fri', '', '']

const monthLabels = computed(() => {
  if (period.value !== 'year') return []
  const today = new Date()
  const labels: string[] = []
  for (let i = 11; i >= 0; i--) {
    const d = new Date(today.getFullYear(), today.getMonth() - i, 1)
    labels.push(d.toLocaleString('en-US', { month: 'short' }))
  }
  return labels
})

function dayTooltip(d: DailyActivity | null): string {
  if (!d) return ''
  const [y, m, day] = d.date.split('-').map(Number)
  const date = new Date(y, m - 1, day)
  return `${date.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' })} · ${d.count} answered`
}

// Rating mix is the real breakdown from the backend over the *full* fetched
// window. (Backend currently returns one number per rating; we don't yet have
// per-period mix, so this is "year so far" rather than "in current period".)
const ratingMix = computed(() => {
  const mix = progress.value?.ratingMix
  if (!mix) return null
  const total = mix.again + mix.hard + mix.good + mix.easy
  if (total === 0) return null
  return [
    { label: 'Again', n: mix.again, pct: Math.round((mix.again / total) * 100), cssColor: 'var(--c-again)' },
    { label: 'Hard',  n: mix.hard,  pct: Math.round((mix.hard  / total) * 100), cssColor: 'var(--c-hard)' },
    { label: 'Good',  n: mix.good,  pct: Math.round((mix.good  / total) * 100), cssColor: 'var(--c-good)' },
    { label: 'Easy',  n: mix.easy,  pct: Math.round((mix.easy  / total) * 100), cssColor: 'var(--c-easy)' },
  ]
})
const ratingMixTotal = computed(() => {
  const mix = progress.value?.ratingMix
  if (!mix) return 0
  return mix.again + mix.hard + mix.good + mix.easy
})
</script>

<template>
  <div class="progress-page">
    <header class="progress-head">
      <div>
        <h1 class="page-title fa-serif">Progress</h1>
        <p class="progress-sub">
          <template v-if="progress">
            <strong>{{ windowActiveDays }} active days</strong>
            in the
            <template v-if="period === 'year'">last year</template>
            <template v-else-if="period === '90d'">last 90 days</template>
            <template v-else-if="period === '30d'">last 30 days</template>
            <template v-else>last 7 days</template>
            · longest streak <strong>{{ bestStreak }} days</strong>
          </template>
          <template v-else>Your study momentum, end to end.</template>
        </p>
      </div>
      <div class="period-switch">
        <button
          v-for="opt in periodOptions"
          :key="opt.value"
          class="fa-btn"
          :class="{ 'is-on': period === opt.value }"
          @click="period = opt.value"
        >{{ opt.label }}</button>
      </div>
    </header>

    <div v-if="loading" class="empty">Loading…</div>
    <div v-else-if="error" class="empty error">{{ error }}</div>

    <template v-else-if="progress">
      <!-- Top stats -->
      <div class="big-stats">
        <div class="big-stat">
          <div class="big-stat-head">
            <span class="fa-cap">Streak</span>
            <span v-if="currentStreak > 0" class="fa-pulse" />
          </div>
          <div class="big-stat-row">
            <span class="big-stat-num fa-num accent">{{ currentStreak }}</span>
            <span class="big-stat-unit fa-mono">days</span>
          </div>
          <p class="big-stat-sub">active · best {{ bestStreak }}</p>
        </div>
        <div class="big-stat">
          <span class="fa-cap">Cards answered</span>
          <div class="big-stat-row">
            <span class="big-stat-num fa-num">{{ totalAnswered.toLocaleString() }}</span>
          </div>
          <p class="big-stat-sub">+{{ answeredThisMonth }} this month</p>
        </div>
        <div class="big-stat">
          <span class="fa-cap">In window</span>
          <div class="big-stat-row">
            <span class="big-stat-num fa-num">{{ windowAnswered.toLocaleString() }}</span>
          </div>
          <p class="big-stat-sub">{{ windowActiveDays }} active days</p>
        </div>
        <div class="big-stat is-last">
          <span class="fa-cap">Rest days</span>
          <div class="big-stat-row">
            <span class="big-stat-num fa-num">{{ windowRestDays }}</span>
          </div>
          <p class="big-stat-sub">in this window</p>
        </div>
      </div>

      <!-- The heatmap -->
      <section class="fa-surface heatmap-surface">
        <div class="heatmap-head">
          <div>
            <span class="fa-cap">{{ period === 'year' ? 'Year in review' : 'Recent activity' }}</span>
            <h2 class="heatmap-title">
              {{ period === 'year' ? 'One year of practice' : 'Practice over time' }}
            </h2>
          </div>
          <div class="heatmap-legend">
            <span class="fa-mono">less</span>
            <span v-for="v in [0,1,2,3,4]" :key="v" class="legend-cell" :style="{ background: cellColor(v) }" />
            <span class="fa-mono">more</span>
          </div>
        </div>

        <div class="heatmap" v-if="period === 'year'">
          <div class="heatmap-months">
            <div v-for="(m, i) in monthLabels" :key="i" class="heatmap-month fa-mono">{{ m }}</div>
          </div>
          <div class="heatmap-grid-wrap">
            <div class="heatmap-day-col">
              <div v-for="(d, i) in dayLabels" :key="i" class="heatmap-day-label fa-mono">{{ d }}</div>
            </div>
            <div class="heatmap-grid">
              <div v-for="(col, w) in heatmapCells" :key="w" class="heatmap-col">
                <div
                  v-for="(cell, d) in col"
                  :key="d"
                  class="heatmap-cell"
                  :class="{ 'is-today': cell.isToday }"
                  :style="{ background: cell.day == null ? 'transparent' : cellColor(bucket(cell.day.count)) }"
                  :title="dayTooltip(cell.day)"
                />
              </div>
            </div>
          </div>
        </div>

        <div v-else class="heatmap-strip">
          <div
            v-for="(d, i) in windowActivity"
            :key="d.date"
            class="strip-cell"
            :class="{ 'is-today': i === windowActivity.length - 1 }"
            :style="{
              background: cellColor(bucket(d.count)),
              height: `${22 + bucket(d.count) * 10}px`,
            }"
            :title="dayTooltip(d)"
          />
        </div>

        <div class="heatmap-foot">
          <span v-if="windowBestDay">
            Best day · <span class="fa-mono accent-text">{{ windowBestDay.count }} cards</span>
          </span>
          <span>
            Avg per active day · <span class="fa-mono accent-text">{{ windowAvgPerActive }} cards</span>
          </span>
          <span>
            Rest days · <span class="fa-mono accent-text">{{ windowRestDays }}</span>
          </span>
        </div>
      </section>

      <!-- Lower split: rating mix + secondary stat surface -->
      <section v-if="ratingMix" class="lower-split">
        <div class="fa-surface rating-mix">
          <div class="rating-head">
            <div>
              <span class="fa-cap">Rating mix</span>
              <h3 class="rating-title">How you rate yourself</h3>
            </div>
            <span class="fa-mono rating-meta">last year · {{ ratingMixTotal.toLocaleString() }} ratings</span>
          </div>
          <div class="rating-bar">
            <div
              v-for="d in ratingMix"
              :key="d.label"
              class="rating-bar-seg"
              :style="{ flex: d.pct, background: d.cssColor }"
            />
          </div>
          <div class="rating-grid">
            <div v-for="d in ratingMix" :key="d.label" class="rating-cell">
              <div class="rating-cell-head">
                <span class="rating-dot" :style="{ background: d.cssColor }" />
                <span class="fa-cap" :style="{ color: d.cssColor }">{{ d.label }}</span>
              </div>
              <div class="rating-cell-row">
                <span class="fa-num rating-num">{{ d.n.toLocaleString() }}</span>
                <span class="fa-mono rating-pct">{{ d.pct }}%</span>
              </div>
            </div>
          </div>
          <div class="rating-foot">
            <span>Honest ratings help FSRS schedule better.</span>
          </div>
        </div>

        <div class="fa-surface period-mix">
          <span class="fa-cap">This week vs month</span>
          <div class="period-stat-row">
            <div class="period-stat-block">
              <span class="fa-num period-num">{{ progress.answeredThisWeek }}</span>
              <span class="fa-cap">answered this week</span>
            </div>
            <div class="period-stat-block">
              <span class="fa-num period-num">{{ progress.answeredThisMonth }}</span>
              <span class="fa-cap">answered this month</span>
            </div>
          </div>
          <div class="fa-rule" />
          <p class="period-note">
            Reviews are tracked in your local time zone. Streaks count days where you completed at least one review.
          </p>
        </div>
      </section>

      <p v-if="totalAnswered === 0" class="empty">
        No reviews yet. Once you start studying, your activity shows up here.
      </p>
    </template>
  </div>
</template>

<style scoped>
.progress-page {
  padding: 28px 0 40px;
  display: flex;
  flex-direction: column;
  gap: 22px;
}
.progress-head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 24px;
  flex-wrap: wrap;
}
.page-title {
  font-size: 34px;
  line-height: 1;
  letter-spacing: -0.02em;
  margin: 0;
  color: var(--ink-0);
}
.progress-sub {
  font-size: 14px;
  color: var(--ink-1);
  margin: 8px 0 0;
}
.progress-sub strong { color: var(--ink-0); font-weight: 600; }
.period-switch { display: flex; gap: 6px; }
.period-switch .fa-btn { height: 28px; padding: 0 12px; font-size: 12px; }
.period-switch .fa-btn.is-on {
  border-color: var(--accent);
  color: var(--accent);
  background: var(--accent-soft);
}

.big-stats {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  border: 1px solid var(--rule-1);
  border-radius: 12px;
  background: var(--paper-1);
}
@media (max-width: 800px) {
  .big-stats { grid-template-columns: repeat(2, 1fr); }
  .big-stat.is-last { border-bottom: none; }
}
.big-stat {
  padding: 22px 24px;
  border-right: 1px solid var(--rule-1);
}
.big-stat.is-last { border-right: none; }
@media (max-width: 800px) {
  .big-stat { border-right: none; border-bottom: 1px solid var(--rule-1); }
  .big-stat:nth-child(odd) { border-right: 1px solid var(--rule-1); }
}
.big-stat-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.big-stat-row {
  display: flex;
  align-items: baseline;
  gap: 6px;
  margin-top: 10px;
}
.big-stat-num {
  font-size: 42px;
  line-height: 1;
  font-weight: 500;
  color: var(--ink-0);
}
.big-stat-num.accent { color: var(--accent); }
.big-stat-unit { font-size: 12px; color: var(--ink-2); }
.big-stat-sub { margin-top: 6px; font-size: 12px; color: var(--ink-2); }

/* Heatmap */
.heatmap-surface { padding: 22px 24px; }
.heatmap-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  margin-bottom: 18px;
  flex-wrap: wrap;
  gap: 12px;
}
.heatmap-title {
  font-size: 18px;
  font-weight: 600;
  margin-top: 4px;
  letter-spacing: -0.01em;
  color: var(--ink-0);
}
.heatmap-legend {
  display: flex;
  align-items: center;
  gap: 14px;
  color: var(--ink-2);
  font-size: 11px;
}
.heatmap-legend > .fa-mono { font-size: 11px; }
.legend-cell {
  width: 11px;
  height: 11px;
  border-radius: 2px;
}
.heatmap {
  display: flex;
  flex-direction: column;
  gap: 6px;
  width: 100%;
}
.heatmap-months {
  display: flex;
  padding-left: 28px;
}
.heatmap-month {
  flex: 1;
  font-size: 10.5px;
  color: var(--ink-2);
}
.heatmap-grid-wrap {
  display: flex;
  gap: 4px;
}
.heatmap-day-col {
  display: flex;
  flex-direction: column;
  gap: 3px;
  padding-right: 4px;
}
.heatmap-day-label {
  height: 13px;
  font-size: 9px;
  color: var(--ink-3);
  display: flex;
  align-items: center;
}
.heatmap-grid {
  display: flex;
  gap: 3px;
  flex: 1;
}
.heatmap-col {
  display: flex;
  flex-direction: column;
  gap: 3px;
  flex: 1;
}
.heatmap-cell {
  aspect-ratio: 1 / 1;
  width: 100%;
  border-radius: 2.5px;
  transition: transform .1s;
}
.heatmap-cell.is-today {
  outline: 1.5px solid var(--ink-0);
  outline-offset: -1.5px;
}

.heatmap-strip {
  display: flex;
  align-items: flex-end;
  gap: 4px;
  margin-top: 6px;
}
.strip-cell {
  flex: 1;
  border-radius: 2px;
  min-height: 14px;
}
.strip-cell.is-today { outline: 1.5px solid var(--ink-0); outline-offset: -1.5px; }

.heatmap-foot {
  display: flex;
  justify-content: space-between;
  margin-top: 14px;
  color: var(--ink-2);
  font-size: 11.5px;
  flex-wrap: wrap;
  gap: 8px;
}
.accent-text { color: var(--ink-0); }

/* Lower split */
.lower-split {
  display: grid;
  grid-template-columns: 1.2fr 1fr;
  gap: 14px;
}
@media (max-width: 800px) { .lower-split { grid-template-columns: 1fr; } }

.rating-mix { padding: 20px 22px; }
.rating-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 14px;
  flex-wrap: wrap;
}
.rating-title {
  font-size: 17px;
  font-weight: 600;
  margin-top: 4px;
  letter-spacing: -0.005em;
  color: var(--ink-0);
}
.rating-meta { font-size: 11px; color: var(--ink-2); }
.rating-bar {
  display: flex;
  margin-top: 18px;
  height: 12px;
  border-radius: 4px;
  overflow: hidden;
}
.rating-bar-seg { min-width: 4px; }
.rating-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 10px;
  margin-top: 18px;
}
.rating-cell { display: flex; flex-direction: column; gap: 6px; }
.rating-cell-head {
  display: flex;
  align-items: center;
  gap: 6px;
}
.rating-cell-head .fa-cap { font-size: 9px; }
.rating-dot {
  width: 8px;
  height: 8px;
  border-radius: 2px;
}
.rating-cell-row {
  display: flex;
  align-items: baseline;
  gap: 4px;
}
.rating-num {
  font-size: 26px;
  line-height: 1;
  font-weight: 500;
}
.rating-pct { font-size: 11px; color: var(--ink-2); }
.rating-foot {
  margin-top: 18px;
  padding-top: 14px;
  border-top: 1px solid var(--rule-1);
  font-size: 12px;
  color: var(--ink-2);
  display: flex;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
}

.period-mix {
  padding: 20px 22px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}
.period-stat-row {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}
.period-stat-block {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.period-num {
  font-size: 32px;
  line-height: 1;
  font-weight: 500;
  color: var(--ink-0);
}
.period-mix .fa-cap { font-size: 10px; }
.period-note {
  margin: 0;
  font-size: 12px;
  color: var(--ink-2);
  line-height: 1.5;
}

.empty {
  padding: 40px;
  text-align: center;
  color: var(--ink-2);
  font-size: 13px;
}
.empty.error { color: var(--c-again); }
</style>
