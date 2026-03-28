import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useDecksStore } from '@/stores/decks'

vi.mock('@/api/client', () => ({
  apiFetch: vi.fn(),
}))

import { apiFetch } from '@/api/client'
const mockApiFetch = vi.mocked(apiFetch)

function makeDeck(overrides: Partial<{ id: string; name: string; description: string | null; cardCount: number; dueCount: number }> = {}) {
  return {
    id: 'd1',
    name: 'Test Deck',
    description: null,
    cardCount: 0,
    dueCount: 0,
    createdAt: '2024-01-01T00:00:00Z',
    ...overrides,
  }
}

async function seedStore(store: ReturnType<typeof useDecksStore>, decks: ReturnType<typeof makeDeck>[]) {
  mockApiFetch.mockResolvedValueOnce(decks)
  await store.fetchDecks()
  vi.clearAllMocks()
}

describe('decks store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('has empty initial state', () => {
    const store = useDecksStore()
    expect(store.decks).toEqual([])
  })

  it('fetchDecks calls /decks and populates store', async () => {
    const mockDecks = [makeDeck({ id: 'd1', name: 'Biology' })]
    mockApiFetch.mockResolvedValueOnce(mockDecks)

    const store = useDecksStore()
    await store.fetchDecks()

    expect(mockApiFetch).toHaveBeenCalledWith('/decks')
    expect(store.decks).toEqual(mockDecks)
  })

  it('createDeck sends POST and adds deck to store', async () => {
    const newDeck = makeDeck({ id: 'd1', name: 'Biology', description: 'Bio concepts' })
    mockApiFetch.mockResolvedValueOnce(newDeck)

    const store = useDecksStore()
    const result = await store.createDeck('Biology', 'Bio concepts')

    expect(mockApiFetch).toHaveBeenCalledWith('/decks', {
      method: 'POST',
      body: JSON.stringify({ name: 'Biology', description: 'Bio concepts' }),
    })
    expect(result.name).toBe('Biology')
    expect(store.decks).toHaveLength(1)
    expect(store.decks[0].id).toBe('d1')
  })

  it('updateDeck sends PUT and updates deck in store', async () => {
    const store = useDecksStore()
    await seedStore(store, [makeDeck({ id: 'd1', name: 'Old Name' })])

    const updated = makeDeck({ id: 'd1', name: 'New Name' })
    mockApiFetch.mockResolvedValueOnce(updated)
    await store.updateDeck('d1', 'New Name')

    expect(mockApiFetch).toHaveBeenCalledWith('/decks/d1', {
      method: 'PUT',
      body: JSON.stringify({ name: 'New Name', description: undefined }),
    })
    expect(store.decks[0].name).toBe('New Name')
  })

  it('deleteDeck sends DELETE and removes deck from store', async () => {
    const store = useDecksStore()
    await seedStore(store, [makeDeck({ id: 'd1' }), makeDeck({ id: 'd2', name: 'Other' })])

    mockApiFetch.mockResolvedValueOnce(undefined)
    await store.deleteDeck('d1')

    expect(mockApiFetch).toHaveBeenCalledWith('/decks/d1', { method: 'DELETE' })
    expect(store.decks).toHaveLength(1)
    expect(store.decks[0].id).toBe('d2')
  })

  it('deleteDeck with deleteCards passes query param', async () => {
    const store = useDecksStore()
    await seedStore(store, [makeDeck({ id: 'd1' })])

    mockApiFetch.mockResolvedValueOnce(undefined)
    await store.deleteDeck('d1', true)

    expect(mockApiFetch).toHaveBeenCalledWith('/decks/d1?deleteCards=true', { method: 'DELETE' })
  })
})
