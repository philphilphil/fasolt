import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useReviewStore } from '@/stores/review'

vi.mock('@/api/client', () => ({
  apiFetch: vi.fn(),
}))

import { apiFetch } from '@/api/client'
const mockApiFetch = vi.mocked(apiFetch)

describe('review store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('starts a session by fetching due cards', async () => {
    const mockCards = [
      { id: 'c1', front: 'What is CAP?', back: 'Consistency, Availability, Partition tolerance', sourceFile: 'cap.md', sourceHeading: '## Overview', state: 'learning', easeFactor: 2.5, interval: 1, repetitions: 0 },
    ]
    mockApiFetch.mockResolvedValueOnce(mockCards)

    const store = useReviewStore()
    await store.startSession()

    expect(store.isActive).toBe(true)
    expect(store.currentCard?.front).toBe('What is CAP?')
  })

  it('flips the current card', async () => {
    mockApiFetch.mockResolvedValueOnce([
      { id: 'c1', front: 'Q', back: 'A', sourceFile: null, sourceHeading: null, state: 'new', easeFactor: 2.5, interval: 0, repetitions: 0 },
    ])

    const store = useReviewStore()
    await store.startSession()

    expect(store.isFlipped).toBe(false)
    store.flipCard()
    expect(store.isFlipped).toBe(true)
  })

  it('rates a card and advances to next', async () => {
    mockApiFetch.mockResolvedValueOnce([
      { id: 'c1', front: 'Q1', back: 'A1', sourceFile: null, sourceHeading: null, state: 'new', easeFactor: 2.5, interval: 0, repetitions: 0 },
      { id: 'c2', front: 'Q2', back: 'A2', sourceFile: null, sourceHeading: null, state: 'new', easeFactor: 2.5, interval: 0, repetitions: 0 },
    ])
    mockApiFetch.mockResolvedValueOnce({ cardId: 'c1', easeFactor: 2.5, interval: 1, repetitions: 1, dueAt: null, state: 'learning' })

    const store = useReviewStore()
    await store.startSession()
    store.flipCard()
    await store.rate(4)

    expect(store.currentCard?.front).toBe('Q2')
    expect(store.isFlipped).toBe(false)
  })

  it('completes session after rating last card', async () => {
    mockApiFetch.mockResolvedValueOnce([
      { id: 'c1', front: 'Q1', back: 'A1', sourceFile: null, sourceHeading: null, state: 'new', easeFactor: 2.5, interval: 0, repetitions: 0 },
    ])
    mockApiFetch.mockResolvedValueOnce({ cardId: 'c1', easeFactor: 2.5, interval: 1, repetitions: 1, dueAt: null, state: 'learning' })

    const store = useReviewStore()
    await store.startSession()
    store.flipCard()
    await store.rate(4)

    expect(store.isComplete).toBe(true)
    expect(store.isActive).toBe(true)
  })
})
