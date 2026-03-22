import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Stat, Deck } from '@/types'

export const useDashboardStore = defineStore('dashboard', () => {
  const stats = ref<Stat[]>([
    { label: 'Due', value: '12', delta: '↑ 3 from yesterday' },
    { label: 'Total', value: '84' },
    { label: 'Retention', value: '91%', delta: '↑ 3% this week' },
    { label: 'Streak', value: '7d' },
  ])

  const decks = ref<Deck[]>([
    { id: 'deck-1', name: 'Distributed Systems', description: null, cardCount: 24, dueCount: 5, createdAt: '2026-03-01T00:00:00Z' },
    { id: 'deck-2', name: 'Rust Ownership', description: null, cardCount: 18, dueCount: 4, createdAt: '2026-03-05T00:00:00Z' },
    { id: 'deck-3', name: 'System Design Patterns', description: null, cardCount: 31, dueCount: 3, createdAt: '2026-03-10T00:00:00Z' },
    { id: 'deck-4', name: 'PostgreSQL Internals', description: null, cardCount: 11, dueCount: 0, createdAt: '2026-03-15T00:00:00Z' },
  ])

  return { stats, decks }
})
