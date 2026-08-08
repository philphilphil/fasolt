import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useLibraryStore } from '@/stores/library'

vi.mock('@/api/client', () => ({
  apiFetch: vi.fn(),
}))

import { apiFetch } from '@/api/client'
const mockApiFetch = vi.mocked(apiFetch)

function makeListResponse(overrides: Record<string, unknown> = {}) {
  return {
    items: [
      {
        id: 'V1StGXR8Z5',
        name: 'Spanish Vocabulary',
        description: null,
        authorHandle: 'phil',
        cardCount: 42,
        copyCount: 3,
        publishedAt: '2026-07-25T20:10:00+00:00',
      },
    ],
    totalCount: 1,
    page: 1,
    pageSize: 24,
    ...overrides,
  }
}

describe('library store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('has empty initial state', () => {
    const store = useLibraryStore()
    expect(store.decks).toEqual([])
    expect(store.totalCount).toBe(0)
  })

  it('fetchDecks without arguments hits the bare endpoint', async () => {
    mockApiFetch.mockResolvedValueOnce(makeListResponse())

    const store = useLibraryStore()
    await store.fetchDecks()

    expect(mockApiFetch).toHaveBeenCalledWith('/library')
    expect(store.decks).toHaveLength(1)
    expect(store.totalCount).toBe(1)
    expect(store.pageSize).toBe(24)
  })

  it('fetchDecks encodes query, sort and page', async () => {
    mockApiFetch.mockResolvedValueOnce(makeListResponse({ page: 2 }))

    const store = useLibraryStore()
    await store.fetchDecks({ q: 'spanish verbs', sort: 'recent', page: 2 })

    expect(mockApiFetch).toHaveBeenCalledWith('/library?q=spanish+verbs&sort=recent&page=2')
  })

  it('fetchDecks omits page 1 from the query string', async () => {
    mockApiFetch.mockResolvedValueOnce(makeListResponse())

    const store = useLibraryStore()
    await store.fetchDecks({ q: 'bio', page: 1 })

    expect(mockApiFetch).toHaveBeenCalledWith('/library?q=bio')
  })

  it('getDeck fetches a single public deck', async () => {
    mockApiFetch.mockResolvedValueOnce({ id: 'V1StGXR8Z5', sampleCards: [] })

    const store = useLibraryStore()
    await store.getDeck('V1StGXR8Z5')

    expect(mockApiFetch).toHaveBeenCalledWith('/library/decks/V1StGXR8Z5')
  })

  it('copyDeck posts to the copy endpoint', async () => {
    mockApiFetch.mockResolvedValueOnce({ id: 'newdeck', name: 'Spanish Vocabulary' })

    const store = useLibraryStore()
    const result = await store.copyDeck('V1StGXR8Z5')

    expect(mockApiFetch).toHaveBeenCalledWith('/library/decks/V1StGXR8Z5/copy', { method: 'POST' })
    expect(result.id).toBe('newdeck')
  })

  it('subscribeDeck posts to the subscribe endpoint', async () => {
    mockApiFetch.mockResolvedValueOnce({ id: 'V1StGXR8Z5', name: 'Spanish Vocabulary', isLinked: true })

    const store = useLibraryStore()
    const result = await store.subscribeDeck('V1StGXR8Z5')

    expect(mockApiFetch).toHaveBeenCalledWith('/library/decks/V1StGXR8Z5/subscribe', { method: 'POST' })
    // A linked deck keeps the author's public id — that's what the deck route uses.
    expect(result.id).toBe('V1StGXR8Z5')
  })

  it('unsubscribeDeck deletes the subscription', async () => {
    mockApiFetch.mockResolvedValueOnce(undefined)

    const store = useLibraryStore()
    await store.unsubscribeDeck('V1StGXR8Z5')

    expect(mockApiFetch).toHaveBeenCalledWith('/library/decks/V1StGXR8Z5/subscribe', { method: 'DELETE' })
  })
})
