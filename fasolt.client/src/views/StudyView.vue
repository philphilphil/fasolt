<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useReviewStore } from '@/stores/review'
import { useDecksStore } from '@/stores/decks'
import { apiFetch } from '@/api/client'
import type { Deck, DailyActivity, Progress, StudyStats } from '@/types'
import { deckColor } from '@/lib/utils'

const router = useRouter()
const reviewStore = useReviewStore()
const decksStore = useDecksStore()

const totalCards = ref(0)
const dueCount = ref(0)
const studiedToday = ref(0)
const creatingDemo = ref(false)
const studyStats = ref<StudyStats>({ currentStreak: 0, bestStreak: 0, totalAnswered: 0, answeredToday: 0 })
const streakActivity = ref<DailyActivity[]>([])

onMounted(async () => {
  try {
    const stats = await reviewStore.fetchStats()
    dueCount.value = stats.dueCount
    totalCards.value = stats.totalCards
    studiedToday.value = stats.studiedToday
  } catch { /* leave as 0 */ }
  try { studyStats.value = await reviewStore.fetchStudyStats() } catch { /* leave at zeros */ }
  try {
    const progress = await apiFetch<Progress>('/review/progress?days=14')
    streakActivity.value = progress.dailyActivity
  } catch { /* leave empty — strip falls back to flat zero state */ }
  decksStore.fetchDecks()
})

const sortedDecks = computed(() => {
  return [...decksStore.decks].sort((a, b) => {
    if (a.isSuspended !== b.isSuspended) return a.isSuspended ? 1 : -1
    if ((b.dueCount > 0) !== (a.dueCount > 0)) return b.dueCount - a.dueCount
    return b.dueCount - a.dueCount || a.name.localeCompare(b.name)
  })
})

const decksWithDue = computed(() => decksStore.decks.filter(d => !d.isSuspended && d.dueCount > 0).length)
const activeDeckCount = computed(() => decksStore.decks.filter(d => !d.isSuspended).length)

const today = computed(() => {
  return new Date().toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' })
})

const todayLabel = computed(() => `Today · ${today.value}`)

async function createDemoDeck() {
  creatingDemo.value = true
  try {
    const deck = await apiFetch<Deck>('/demo-deck', { method: 'POST' })
    router.push(`/decks/${deck.id}`)
  } catch { creatingDemo.value = false }
}

function startReview(deckId?: string) {
  if (deckId) router.push(`/review?deckId=${deckId}`)
  else router.push('/review')
}

// Streak strip — real per-day reviews from /review/progress?days=14, bucketed
// 0..4 relative to the window max. Empty array (pre-fetch or error) renders as
// a flat zero strip with today highlighted.
type StreakBar = { count: number; bucket: number; date: string | null; isToday: boolean }

const streakBars = computed<StreakBar[]>(() => {
  const data = streakActivity.value
  if (data.length === 0) {
    return Array.from({ length: 14 }, (_, i) => ({
      count: 0, bucket: 0, date: null, isToday: i === 13,
    }))
  }
  const max = Math.max(1, ...data.map(d => d.count))
  return data.map((d, i) => {
    const ratio = d.count / max
    let bucket = 0
    if (d.count > 0) {
      if (ratio >= 0.85) bucket = 4
      else if (ratio >= 0.55) bucket = 3
      else if (ratio >= 0.30) bucket = 2
      else bucket = 1
    }
    return { count: d.count, bucket, date: d.date, isToday: i === data.length - 1 }
  })
})

function streakBarTitle(b: StreakBar): string {
  if (!b.date) return b.isToday ? 'Today' : ''
  const [y, m, day] = b.date.split('-').map(Number)
  const label = new Date(y, m - 1, day).toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' })
  const verb = b.count === 1 ? 'review' : 'reviews'
  return `${b.isToday ? 'Today · ' : ''}${label} · ${b.count} ${verb}`
}
</script>

<template>
  <div class="study-page">
    <!-- Hero row: due number + streak panel -->
    <section class="hero">
      <div class="hero-due">
        <div class="hero-meta">
          <span class="fa-cap">{{ todayLabel }}</span>
        </div>
        <div class="hero-due-stack">
          <div class="hero-number-wrap">
            <span class="hero-number fa-num">{{ dueCount }}</span>
            <span class="hero-descriptor">
              <template v-if="dueCount > 0">
                cards due<br />
                <template v-if="decksWithDue > 0">
                  across <strong>{{ decksWithDue }} {{ decksWithDue === 1 ? 'deck' : 'decks' }}</strong>
                </template>
                <template v-else>
                  across <strong>your library</strong>
                </template>
              </template>
              <template v-else-if="totalCards > 0">
                cards due<br />
                <strong>all caught up</strong>
              </template>
              <template v-else>
                cards total<br />
                <strong>add your first cards</strong>
              </template>
            </span>
          </div>
          <div class="hero-actions">
            <button
              v-if="dueCount > 0"
              class="fa-btn fa-btn-primary hero-cta"
              @click="startReview()"
            >
              Start reviewing
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14M13 5l7 7-7 7"/></svg>
              <span class="cta-kbd">↵</span>
            </button>
            <button
              v-else-if="totalCards === 0"
              class="fa-btn fa-btn-primary hero-cta"
              :disabled="creatingDemo"
              @click="createDemoDeck"
            >
              {{ creatingDemo ? 'Creating…' : 'Create a demo deck' }}
            </button>
            <RouterLink to="/decks" class="fa-btn" style="height:40px;padding:0 14px;">Browse decks</RouterLink>
            <RouterLink to="/mcp-setup" class="fa-btn fa-btn-ghost" style="height:40px;color:var(--ink-2);">Connect your AI</RouterLink>
          </div>
        </div>
      </div>

      <aside class="hero-streak">
        <div class="streak-head">
          <span class="fa-cap">Streak</span>
          <span class="fa-mono streak-range">last 14 days</span>
        </div>
        <div class="streak-num-row">
          <span class="streak-num fa-num">{{ studyStats.currentStreak }}</span>
          <span class="streak-meta">
            days · best {{ studyStats.bestStreak }}
            <template v-if="studyStats.currentStreak > 0"> 🔥</template>
          </span>
        </div>
        <div class="streak-bars">
          <div
            v-for="(b, i) in streakBars"
            :key="i"
            class="streak-bar"
            :class="{ 'is-today': b.isToday, 'is-rest': b.bucket === 0 }"
            :style="{
              height: `${12 + b.bucket * 8}px`,
              background: b.isToday
                ? 'var(--accent)'
                : b.bucket === 0
                  ? 'var(--rule-1)'
                  : `oklch(0.55 0.13 155 / ${0.35 + b.bucket * 0.12})`,
            }"
            :title="streakBarTitle(b)"
          />
        </div>
        <div class="streak-axis">
          <span class="fa-mono">2w</span>
          <span class="fa-mono">today</span>
        </div>
      </aside>
    </section>

    <div class="fa-rule" />

    <!-- Decks list -->
    <section class="decks-section">
      <header class="section-head">
        <div class="section-head-text">
          <h2>Your decks</h2>
          <span class="fa-cap">
            {{ activeDeckCount }} total · {{ decksWithDue }} with cards due
          </span>
        </div>
      </header>
      <div v-if="decksStore.loading" class="empty">Loading decks…</div>
      <div v-else-if="sortedDecks.length === 0" class="empty">
        <span>No decks yet.</span>
        <RouterLink to="/decks" class="fa-link">Create your first deck</RouterLink>
      </div>
      <div v-else class="deck-list">
        <button
          v-for="deck in sortedDecks"
          :key="deck.id"
          class="deck-row"
          :class="{ 'is-suspended': deck.isSuspended }"
          @click="startReview(deck.id)"
        >
          <span class="fa-tag" :style="{ background: deckColor(deck.id) }" />
          <div class="deck-row-text">
            <div class="deck-row-name">{{ deck.name }}</div>
            <div class="fa-mono deck-row-sub">
              {{ deck.cardCount }} cards<template v-if="deck.isSuspended"> · suspended</template>
            </div>
          </div>
          <div class="deck-row-bar">
            <div
              class="deck-row-bar-fill"
              :style="{
                width: `${deck.cardCount ? Math.min(100, (deck.dueCount / deck.cardCount) * 100) : 0}%`,
                background: deck.dueCount > 0 ? 'var(--accent)' : 'var(--rule-1)',
              }"
            />
          </div>
          <div
            class="fa-mono deck-row-due"
            :class="{ 'has-due': deck.dueCount > 0 }"
          >
            {{ deck.dueCount > 0 ? `${deck.dueCount} due` : '—' }}
          </div>
          <div class="deck-row-cta">
            <span v-if="deck.dueCount > 0 && !deck.isSuspended" class="fa-btn fa-btn-accent deck-cta-btn">
              Review
              <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14M13 5l7 7-7 7"/></svg>
            </span>
            <span v-else class="fa-cap" style="font-size:9px;">
              {{ deck.isSuspended ? 'paused' : 'caught up' }}
            </span>
          </div>
          <svg class="deck-chevron" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="m9 6 6 6-6 6"/></svg>
        </button>
      </div>
    </section>

    <!-- Stats tiles -->
    <section class="stats-row">
      <div class="stat-tile">
        <div class="stat-head">
          <span class="fa-cap">Current streak</span>
          <span v-if="studyStats.currentStreak > 0" class="fa-pulse" />
        </div>
        <div class="stat-value-row">
          <span class="stat-value fa-num">{{ studyStats.currentStreak }}</span>
          <span class="stat-unit fa-mono">days</span>
        </div>
        <span class="stat-sub">
          {{ studyStats.currentStreak > 0 ? `🔥 don't break it tonight` : 'review today to start a streak' }}
        </span>
      </div>
      <div class="stat-tile">
        <span class="fa-cap">Best streak</span>
        <div class="stat-value-row">
          <span class="stat-value fa-num">{{ studyStats.bestStreak }}</span>
          <span class="stat-unit fa-mono">days</span>
        </div>
        <span class="stat-sub">all time</span>
      </div>
      <div class="stat-tile">
        <span class="fa-cap">Total answered</span>
        <div class="stat-value-row">
          <span class="stat-value fa-num">{{ studyStats.totalAnswered.toLocaleString() }}</span>
        </div>
        <span class="stat-sub">across {{ totalCards }} cards</span>
      </div>
      <div class="stat-tile is-last">
        <span class="fa-cap">Today</span>
        <div class="stat-value-row">
          <span class="stat-value fa-num">{{ studyStats.answeredToday }}</span>
          <span class="stat-unit fa-mono">answered</span>
        </div>
        <span class="stat-sub">{{ studiedToday > 0 ? `${studiedToday} unique cards` : 'no reviews yet' }}</span>
      </div>
    </section>
  </div>
</template>

<style scoped>
.study-page {
  display: flex;
  flex-direction: column;
  gap: 32px;
  padding: 36px 8px 40px;
}
.hero {
  display: grid;
  grid-template-columns: 1.35fr 1fr;
  gap: 36px;
  align-items: stretch;
}
@media (max-width: 900px) {
  .hero { grid-template-columns: 1fr; gap: 24px; }
}

/* Due number column */
.hero-due {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  min-height: 280px;
}
.hero-meta {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
}
.hero-meta .muted { font-size: 9.5px; }
.hero-due-stack { margin-top: 18px; }
.hero-number-wrap {
  display: flex;
  align-items: flex-end;
  gap: 18px;
}
.hero-number {
  font-size: 132px;
  line-height: 0.85;
  letter-spacing: -0.04em;
  color: var(--ink-0);
  font-weight: 500;
}
.hero-descriptor {
  padding-bottom: 12px;
  color: var(--ink-1);
  font-size: 15px;
  line-height: 1.4;
  max-width: 220px;
}
.hero-descriptor strong { color: var(--ink-0); font-weight: 600; }
.hero-actions {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 24px;
  flex-wrap: wrap;
}
.hero-cta {
  height: 40px;
  padding: 0 18px;
  font-size: 14px;
  font-weight: 600;
}
.cta-kbd {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin-left: 4px;
  width: 18px;
  height: 18px;
  font-family: 'Geist Mono', monospace;
  font-size: 11px;
  background: oklch(1 0 0 / .15);
  border: 1px solid oklch(1 0 0 / .25);
  border-radius: 4px;
  color: inherit;
}

@media (max-width: 600px) {
  .hero-number { font-size: 96px; }
}

/* Streak panel */
.hero-streak {
  display: flex;
  flex-direction: column;
  padding: 20px;
  border: 1px solid var(--rule-1);
  border-radius: 12px;
  background: var(--paper-1);
  justify-content: space-between;
  min-height: 280px;
}
.streak-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
}
.streak-range { font-size: 11px; color: var(--ink-2); }
.streak-num-row {
  display: flex;
  align-items: baseline;
  gap: 10px;
  margin-top: 10px;
}
.streak-num {
  font-size: 64px;
  line-height: 0.9;
  letter-spacing: -0.03em;
  font-weight: 500;
}
.streak-meta {
  font-size: 13px;
  color: var(--ink-1);
  padding-bottom: 6px;
}
.streak-bars {
  display: flex;
  align-items: flex-end;
  gap: 4px;
  height: 64px;
  margin-top: 12px;
}
.streak-bar {
  flex: 1;
  border-radius: 2px;
  min-height: 6px;
  transition: background .2s;
}
.streak-bar.is-today {
  box-shadow: 0 0 0 2px var(--accent-soft);
}
.streak-axis {
  display: flex;
  justify-content: space-between;
  margin-top: 6px;
  color: var(--ink-3);
  font-size: 10px;
}

/* Section heads */
.section-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  margin-bottom: 14px;
}
.section-head-text {
  display: flex;
  align-items: baseline;
  gap: 12px;
}
.section-head-text h2 {
  font-size: 20px;
  font-weight: 600;
  letter-spacing: -0.01em;
  margin: 0;
}

/* Deck list */
.deck-list {
  border: 1px solid var(--rule-1);
  border-radius: 12px;
  background: var(--paper-1);
  overflow: hidden;
}
.deck-row {
  display: grid;
  grid-template-columns: 16px minmax(160px, 1.4fr) 1fr 80px 100px 22px;
  align-items: center;
  gap: 16px;
  padding: 14px 18px;
  width: 100%;
  text-align: left;
  background: transparent;
  border: none;
  border-bottom: 1px solid var(--rule-1);
  cursor: pointer;
  color: inherit;
  font: inherit;
  transition: background .12s;
}
.deck-row:last-child { border-bottom: none; }
.deck-row:hover { background: var(--paper-2); }
.deck-row.is-suspended { opacity: 0.55; }
.deck-row-text { min-width: 0; }
.deck-row-name {
  font-weight: 500;
  font-size: 14px;
  color: var(--ink-0);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.deck-row-sub {
  font-size: 11px;
  color: var(--ink-2);
  margin-top: 2px;
}
.deck-row-bar {
  height: 4px;
  background: var(--paper-2);
  border-radius: 2px;
  overflow: hidden;
}
.deck-row-bar-fill {
  height: 100%;
  transition: width .2s;
}
.deck-row-due {
  font-size: 13px;
  color: var(--ink-3);
  text-align: right;
  font-weight: 400;
}
.deck-row-due.has-due {
  color: var(--ink-0);
  font-weight: 600;
}
.deck-row-cta { display: flex; justify-content: flex-end; }
.deck-cta-btn {
  height: 26px;
  padding: 0 10px;
  font-size: 11.5px;
  pointer-events: none;
}
.deck-chevron { color: var(--ink-3); justify-self: end; }

/* Stats row */
.stats-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  border: 1px solid var(--rule-1);
  border-radius: 12px;
  background: var(--paper-1);
}
@media (max-width: 800px) {
  .stats-row { grid-template-columns: repeat(2, 1fr); }
  .stat-tile.is-last { border-bottom: none; }
}
.stat-tile {
  padding: 20px 22px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  border-right: 1px solid var(--rule-1);
}
.stat-tile.is-last { border-right: none; }
@media (max-width: 800px) {
  .stat-tile { border-right: none; border-bottom: 1px solid var(--rule-1); }
  .stat-tile:nth-child(odd) { border-right: 1px solid var(--rule-1); }
}
.stat-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.stat-value-row {
  display: flex;
  align-items: baseline;
  gap: 6px;
  margin-top: 8px;
}
.stat-value {
  font-size: 32px;
  line-height: 1;
  font-weight: 500;
}
.stat-unit { font-size: 11px; color: var(--ink-2); }
.stat-sub { font-size: 11.5px; color: var(--ink-2); }

/* Empty state */
.empty {
  padding: 28px 0;
  color: var(--ink-2);
  font-size: 13px;
  text-align: center;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
</style>
