import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { Card, ReviewRating } from '@/types'

export const useReviewStore = defineStore('review', () => {
  const deckId = ref<string | null>(null)
  const deckName = ref('')
  const cards = ref<Card[]>([])
  const currentIndex = ref(0)
  const isFlipped = ref(false)
  const ratings = ref(new Map<string, ReviewRating>())

  const isActive = computed(() => deckId.value !== null)
  const isComplete = computed(() => currentIndex.value >= cards.value.length)
  const currentCard = computed(() => cards.value[currentIndex.value] ?? null)
  const progress = computed(() => `${currentIndex.value + 1} of ${cards.value.length}`)
  const progressFraction = computed(() =>
    cards.value.length === 0 ? 0 : currentIndex.value / cards.value.length
  )

  const ratingCounts = computed(() => {
    const counts = { again: 0, hard: 0, good: 0, easy: 0 }
    for (const r of ratings.value.values()) {
      counts[r]++
    }
    return counts
  })

  function startSession(id: string, name: string, sessionCards: Card[]) {
    deckId.value = id
    deckName.value = name
    cards.value = sessionCards
    currentIndex.value = 0
    isFlipped.value = false
    ratings.value = new Map()
  }

  function flip() {
    isFlipped.value = true
  }

  function rate(rating: ReviewRating) {
    const card = currentCard.value
    if (!card) return
    ratings.value.set(card.id, rating)
    currentIndex.value++
    isFlipped.value = false
  }

  function endSession() {
    deckId.value = null
    deckName.value = ''
    cards.value = []
    currentIndex.value = 0
    isFlipped.value = false
    ratings.value = new Map()
  }

  return {
    deckId, deckName, cards, currentIndex, isFlipped, ratings,
    isActive, isComplete, currentCard, progress, progressFraction, ratingCounts,
    startSession, flip, rate, endSession,
  }
})
