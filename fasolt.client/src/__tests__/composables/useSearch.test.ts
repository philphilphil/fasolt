import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useSearch } from '@/composables/useSearch'

// Mock vue-router
const mockPush = vi.fn()
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: mockPush }),
}))

// Mock api/client searchAll
vi.mock('@/api/client', () => ({
  searchAll: vi.fn(),
  apiFetch: vi.fn(),
}))

import { searchAll } from '@/api/client'
const mockSearchAll = vi.mocked(searchAll)

describe('useSearch', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('flatItems does not include a "file" type', async () => {
    mockSearchAll.mockResolvedValueOnce({
      cards: [{ id: 'c1', headline: 'CAP theorem', state: 'new' }],
      decks: [{ id: 'd1', headline: 'Distributed Systems', cardCount: 5 }],
    })

    const { flatItems, results } = useSearch()

    // Directly set results to simulate a search response
    results.value = {
      cards: [{ id: 'c1', headline: 'CAP theorem', state: 'new' }],
      decks: [{ id: 'd1', headline: 'Distributed Systems', cardCount: 5 }],
    }

    const types = flatItems.value.map((item) => item.type)
    expect(types).not.toContain('file')
    expect(types).toContain('card')
    expect(types).toContain('deck')
  })

  it('flatItems only contains card and deck types', () => {
    const { flatItems, results } = useSearch()

    results.value = {
      cards: [
        { id: 'c1', headline: 'Q1', state: 'new' },
        { id: 'c2', headline: 'Q2', state: 'review' },
      ],
      decks: [
        { id: 'd1', headline: 'Deck A', cardCount: 3 },
      ],
    }

    for (const item of flatItems.value) {
      expect(['card', 'deck']).toContain(item.type)
    }
  })

  it('navigateToResult for card pushes to /cards/:id', () => {
    const { navigateToResult } = useSearch()

    navigateToResult({ type: 'card', data: { id: 'abc123', headline: 'Q', state: 'new' } })

    expect(mockPush).toHaveBeenCalledWith('/cards/abc123')
  })

  it('navigateToResult for deck pushes to /decks/:id', () => {
    const { navigateToResult } = useSearch()

    navigateToResult({ type: 'deck', data: { id: 'deck-99', headline: 'My Deck', cardCount: 7 } })

    expect(mockPush).toHaveBeenCalledWith('/decks/deck-99')
  })

  it('navigateToResult closes and resets the search', () => {
    const { navigateToResult, isOpen, query } = useSearch()

    isOpen.value = true
    query.value = 'test'

    navigateToResult({ type: 'card', data: { id: 'c1', headline: 'Q', state: 'new' } })

    expect(isOpen.value).toBe(false)
    expect(query.value).toBe('')
  })
})
