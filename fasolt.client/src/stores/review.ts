import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { DueCard, ReviewStats } from '@/types'
import { apiFetch } from '@/api/client'

export const useReviewStore = defineStore('review', () => {
  const queue = ref<DueCard[]>([])
  const currentIndex = ref(0)
  const isFlipped = ref(false)
  const isActive = ref(false)
  const loading = ref(false)

  const sessionStats = ref({
    reviewed: 0,
    again: 0,
    hard: 0,
    good: 0,
    easy: 0,
    startTime: 0,
  })

  const currentCard = computed(() =>
    currentIndex.value < queue.value.length ? queue.value[currentIndex.value] : null
  )

  const isComplete = computed(() => isActive.value && currentCard.value === null && sessionStats.value.reviewed > 0)
  const noDueCards = computed(() => isActive.value && queue.value.length === 0 && sessionStats.value.reviewed === 0)

  const progress = computed(() => {
    if (queue.value.length === 0) return 0
    return Math.round((currentIndex.value / queue.value.length) * 100)
  })

  const sessionTime = computed(() => {
    if (!sessionStats.value.startTime) return 0
    return Math.round((Date.now() - sessionStats.value.startTime) / 1000)
  })

  async function startSession(deckId?: string) {
    loading.value = true
    try {
      const params = deckId ? `?deckId=${deckId}` : ''
      const cards = await apiFetch<DueCard[]>(`/review/due${params}`)
      queue.value = cards
      currentIndex.value = 0
      isFlipped.value = false
      isActive.value = true
      sessionStats.value = { reviewed: 0, again: 0, hard: 0, good: 0, easy: 0, startTime: Date.now() }
    } finally {
      loading.value = false
    }
  }

  function flipCard() {
    isFlipped.value = true
  }

  async function rate(rating: 'again' | 'hard' | 'good' | 'easy') {
    const card = currentCard.value
    if (!card) return

    await apiFetch('/review/rate', {
      method: 'POST',
      body: JSON.stringify({ cardId: card.id, rating }),
    })

    sessionStats.value.reviewed++
    sessionStats.value[rating]++
    if (rating === 'again') {
      queue.value.push({ ...card })
    }

    currentIndex.value++
    isFlipped.value = false
  }

  function endSession() {
    isActive.value = false
    queue.value = []
    currentIndex.value = 0
    isFlipped.value = false
  }

  async function fetchStats(): Promise<ReviewStats> {
    return apiFetch<ReviewStats>('/review/stats')
  }

  return {
    queue, currentCard, isFlipped, isActive, isComplete, noDueCards, loading,
    progress, sessionStats, sessionTime,
    startSession, flipCard, rate, endSession, fetchStats,
  }
})
