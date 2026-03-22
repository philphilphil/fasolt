import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useReviewStore } from '@/stores/review'

describe('review store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('starts a session with cards', () => {
    const store = useReviewStore()
    store.startSession('deck-1', 'Distributed Systems', [
      {
        id: 'c1', deckId: 'deck-1', question: 'What is CAP?',
        answer: 'Consistency, Availability, Partition tolerance',
        sourceFile: 'cap.md', sourceSection: '## Overview',
        dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0,
      },
    ])
    expect(store.isActive).toBe(true)
    expect(store.currentCard?.question).toBe('What is CAP?')
    expect(store.progress).toBe('1 of 1')
  })

  it('flips the current card', () => {
    const store = useReviewStore()
    store.startSession('deck-1', 'Test', [{
      id: 'c1', deckId: 'deck-1', question: 'Q', answer: 'A',
      sourceFile: 'f.md', sourceSection: null,
      dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0,
    }])
    expect(store.isFlipped).toBe(false)
    store.flip()
    expect(store.isFlipped).toBe(true)
  })

  it('rates a card and advances to next', () => {
    const store = useReviewStore()
    store.startSession('deck-1', 'Test', [
      { id: 'c1', deckId: 'deck-1', question: 'Q1', answer: 'A1', sourceFile: 'f.md', sourceSection: null, dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
      { id: 'c2', deckId: 'deck-1', question: 'Q2', answer: 'A2', sourceFile: 'f.md', sourceSection: null, dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
    ])
    store.flip()
    store.rate('good')
    expect(store.currentCard?.question).toBe('Q2')
    expect(store.isFlipped).toBe(false)
  })

  it('completes session after rating last card', () => {
    const store = useReviewStore()
    store.startSession('deck-1', 'Test', [
      { id: 'c1', deckId: 'deck-1', question: 'Q1', answer: 'A1', sourceFile: 'f.md', sourceSection: null, dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
    ])
    store.flip()
    store.rate('good')
    expect(store.isComplete).toBe(true)
    expect(store.isActive).toBe(true)
  })
})
