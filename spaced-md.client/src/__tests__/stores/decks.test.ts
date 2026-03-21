import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useDecksStore } from '@/stores/decks'

vi.mock('@/api/client', () => ({
  apiFetch: vi.fn(),
}))

import { apiFetch } from '@/api/client'
const mockApiFetch = vi.mocked(apiFetch)

describe('decks store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('fetchDecks calls /decks and populates decks', async () => {
    const mockDecks = [
      { id: 'd1', name: 'Distributed Systems', description: null, cardCount: 10, dueCount: 3, createdAt: '2024-01-01T00:00:00Z' },
    ]
    mockApiFetch.mockResolvedValueOnce(mockDecks)

    const store = useDecksStore()
    await store.fetchDecks()

    expect(mockApiFetch).toHaveBeenCalledWith('/decks')
    expect(store.decks).toEqual(mockDecks)
  })

  it('fetchDecks starts empty', () => {
    const store = useDecksStore()
    expect(store.decks).toEqual([])
  })

  it('addFileCards method does NOT exist on decks store', () => {
    const store = useDecksStore()
    expect((store as Record<string, unknown>)['addFileCards']).toBeUndefined()
  })
})
