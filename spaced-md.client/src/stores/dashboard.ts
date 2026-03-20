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
    { id: 'deck-1', name: 'Distributed Systems', fileName: 'distributed-systems.md', cardCount: 24, dueCount: 5, nextReview: 'now' },
    { id: 'deck-2', name: 'Rust Ownership', fileName: 'rust-ownership.md', cardCount: 18, dueCount: 4, nextReview: 'now' },
    { id: 'deck-3', name: 'System Design Patterns', fileName: 'system-design.md', cardCount: 31, dueCount: 3, nextReview: '2h' },
    { id: 'deck-4', name: 'PostgreSQL Internals', fileName: 'postgresql-internals.md', cardCount: 11, dueCount: 0, nextReview: 'tomorrow' },
  ])

  return { stats, decks }
})
