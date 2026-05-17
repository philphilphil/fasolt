import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { DueCard, ReviewStats, StudyStats } from '@/types'
import { apiFetch } from '@/api/client'

export type ReviewMode = 'normal' | 'cram'

export const useReviewStore = defineStore('review', () => {
  const queue = ref<DueCard[]>([])
  const currentIndex = ref(0)
  const isFlipped = ref(false)
  const isActive = ref(false)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const mode = ref<ReviewMode>('normal')

  const sessionStats = ref({
    reviewed: 0,
    again: 0,
    hard: 0,
    good: 0,
    easy: 0,
    skipped: 0,
    suspended: 0,
    startTime: 0,
  })

  const currentCard = computed(() =>
    currentIndex.value < queue.value.length ? queue.value[currentIndex.value] : null
  )

  const isComplete = computed(() => isActive.value && currentCard.value === null && (sessionStats.value.reviewed > 0 || sessionStats.value.skipped > 0))
  const noDueCards = computed(() => isActive.value && queue.value.length === 0 && sessionStats.value.reviewed === 0 && sessionStats.value.skipped === 0)

  const progress = computed(() => {
    if (queue.value.length === 0) return 0
    return Math.round((currentIndex.value / queue.value.length) * 100)
  })

  const sessionTime = computed(() => {
    if (!sessionStats.value.startTime) return 0
    return Math.round((Date.now() - sessionStats.value.startTime) / 1000)
  })

  async function startSession(deckId?: string, sessionMode: ReviewMode = 'normal') {
    loading.value = true
    error.value = null
    try {
      let endpoint: string
      if (sessionMode === 'cram') {
        if (!deckId) {
          error.value = 'Custom study requires a deck.'
          return
        }
        endpoint = `/review/custom?deckId=${deckId}`
      } else {
        const params = deckId ? `?deckId=${deckId}` : ''
        endpoint = `/review/due${params}`
      }
      const cards = await apiFetch<DueCard[]>(endpoint)
      queue.value = cards
      currentIndex.value = 0
      isFlipped.value = false
      isActive.value = true
      mode.value = sessionMode
      sessionStats.value = { reviewed: 0, again: 0, hard: 0, good: 0, easy: 0, skipped: 0, suspended: 0, startTime: Date.now() }
    } catch {
      error.value = 'Failed to load review session. Please try again.'
    } finally {
      loading.value = false
    }
  }

  function flipCard() {
    isFlipped.value = true
  }

  function skip() {
    if (!currentCard.value) return
    sessionStats.value.skipped++
    currentIndex.value++
    isFlipped.value = false
  }

  async function rate(rating: 'again' | 'hard' | 'good' | 'easy') {
    const card = currentCard.value
    if (!card) return

    error.value = null
    try {
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
    } catch {
      error.value = 'Failed to submit rating. Please try again.'
    }
  }

  async function suspend() {
    const card = currentCard.value
    if (!card) return
    try {
      await apiFetch(`/cards/${card.id}/suspended`, {
        method: 'PUT',
        body: JSON.stringify({ isSuspended: true }),
      })
    } catch {
      // best-effort — card still skipped even if API fails
    }
    sessionStats.value.suspended++
    currentIndex.value++
    isFlipped.value = false
  }

  function advance() {
    if (!currentCard.value) return
    sessionStats.value.reviewed++
    currentIndex.value++
    isFlipped.value = false
  }

  function endSession() {
    isActive.value = false
    queue.value = []
    currentIndex.value = 0
    isFlipped.value = false
    mode.value = 'normal'
  }

  async function fetchStats(): Promise<ReviewStats> {
    return apiFetch<ReviewStats>('/review/stats')
  }

  async function fetchStudyStats(): Promise<StudyStats> {
    return apiFetch<StudyStats>('/review/study-stats')
  }

  return {
    queue, currentCard, isFlipped, isActive, isComplete, noDueCards, loading, error, mode,
    progress, sessionStats, sessionTime,
    startSession, flipCard, skip, suspend, rate, advance, endSession, fetchStats, fetchStudyStats,
  }
})
