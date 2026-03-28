import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useCardsStore } from '@/stores/cards'

vi.mock('@/api/client', () => ({
  apiFetch: vi.fn(),
}))

import { apiFetch } from '@/api/client'
const mockApiFetch = vi.mocked(apiFetch)

function makeCard(overrides: Partial<{
  id: string; front: string; back: string; isSuspended: boolean;
  state: 'new' | 'learning' | 'review' | 'relearning';
  stability: number | null; difficulty: number | null;
  step: number | null; dueAt: string | null; lastReviewedAt: string | null;
}> = {}) {
  return {
    id: 'c1', sourceFile: null, sourceHeading: null,
    front: 'Q', back: 'A', frontSvg: null, backSvg: null,
    createdAt: '2024-01-01T00:00:00Z',
    stability: null, difficulty: null, step: null, dueAt: null,
    state: 'new' as const, lastReviewedAt: null,
    isSuspended: false, decks: [],
    ...overrides,
  }
}

async function seedStore(store: ReturnType<typeof useCardsStore>, cards: ReturnType<typeof makeCard>[]) {
  mockApiFetch.mockResolvedValueOnce({ items: cards, hasMore: false, nextCursor: null })
  await store.fetchCards()
  vi.clearAllMocks() // Clear fetchCards call so subsequent assertions are clean
}

describe('cards store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  // --- fetchCards ---

  it('fetchCards calls /cards?limit=200 with no filter', async () => {
    mockApiFetch.mockResolvedValueOnce({ items: [], hasMore: false, nextCursor: null })
    const store = useCardsStore()
    await store.fetchCards()
    expect(mockApiFetch).toHaveBeenCalledWith('/cards?limit=200')
  })

  it('fetchCards includes sourceFile as query param', async () => {
    mockApiFetch.mockResolvedValueOnce({ items: [], hasMore: false, nextCursor: null })
    const store = useCardsStore()
    await store.fetchCards('notes.md')
    expect(mockApiFetch).toHaveBeenCalledWith('/cards?limit=200&sourceFile=notes.md')
  })

  it('fetchCards URL-encodes sourceFile with special characters', async () => {
    mockApiFetch.mockResolvedValueOnce({ items: [], hasMore: false, nextCursor: null })
    const store = useCardsStore()
    await store.fetchCards('my notes/cap theorem.md')
    const call = mockApiFetch.mock.calls[0][0] as string
    expect(call).toContain('/cards?limit=200&sourceFile=')
    expect(call).not.toContain(' ')
  })

  it('fetchCards populates the store with returned cards', async () => {
    const cards = [makeCard({ id: 'c1' }), makeCard({ id: 'c2', front: 'Q2' })]
    mockApiFetch.mockResolvedValueOnce({ items: cards, hasMore: false, nextCursor: null })

    const store = useCardsStore()
    expect(store.cards).toHaveLength(0)
    await store.fetchCards()
    expect(store.cards).toHaveLength(2)
    expect(store.cards[0].id).toBe('c1')
    expect(store.cards[1].id).toBe('c2')
  })

  it('fetchCards sets loading=true during fetch and false after', async () => {
    let resolveFetch!: (value: unknown) => void
    mockApiFetch.mockReturnValueOnce(new Promise(r => { resolveFetch = r }))

    const store = useCardsStore()
    const promise = store.fetchCards()
    expect(store.loading).toBe(true)

    resolveFetch({ items: [], hasMore: false, nextCursor: null })
    await promise
    expect(store.loading).toBe(false)
  })

  it('fetchCards sets loading=false even on error', async () => {
    mockApiFetch.mockRejectedValueOnce(new Error('Network error'))

    const store = useCardsStore()
    await expect(store.fetchCards()).rejects.toThrow('Network error')
    expect(store.loading).toBe(false)
  })

  // --- createCard ---

  it('createCard sends correct API request and returns result', async () => {
    const mockCard = makeCard({ id: 'c1', sourceFile: 'notes.md' })
    mockApiFetch.mockResolvedValueOnce(mockCard)

    const store = useCardsStore()
    const result = await store.createCard({
      sourceFile: 'notes.md',
      sourceHeading: '## CAP Theorem',
      front: 'Q',
      back: 'A',
    })

    expect(mockApiFetch).toHaveBeenCalledWith('/cards', {
      method: 'POST',
      body: JSON.stringify({
        sourceFile: 'notes.md',
        sourceHeading: '## CAP Theorem',
        front: 'Q',
        back: 'A',
      }),
    })
    expect(result.sourceFile).toBe('notes.md')
  })

  // --- updateCard ---

  it('updateCard calls PUT with correct endpoint and body', async () => {
    const store = useCardsStore()
    await seedStore(store, [makeCard({ id: 'c1' }), makeCard({ id: 'c2' })])

    const updated = makeCard({ id: 'c1', front: 'Updated' })
    mockApiFetch.mockResolvedValueOnce(updated)
    await store.updateCard('c1', { front: 'Updated', back: 'A' })

    expect(mockApiFetch).toHaveBeenCalledWith('/cards/c1', {
      method: 'PUT',
      body: JSON.stringify({ front: 'Updated', back: 'A' }),
    })
  })

  it('updateCard updates only the target card, leaves others unchanged', async () => {
    const card1 = makeCard({ id: 'c1', front: 'Q1' })
    const card2 = makeCard({ id: 'c2', front: 'Q2' })

    const store = useCardsStore()
    await seedStore(store, [card1, card2])

    const updatedCard1 = makeCard({ id: 'c1', front: 'Updated Q1' })
    mockApiFetch.mockResolvedValueOnce(updatedCard1)
    await store.updateCard('c1', { front: 'Updated Q1', back: 'A' })

    expect(store.cards).toHaveLength(2)
    expect(store.cards[0].front).toBe('Updated Q1')
    expect(store.cards[1].front).toBe('Q2') // c2 untouched
  })

  // --- deleteCard ---

  it('deleteCard calls DELETE with correct endpoint', async () => {
    const store = useCardsStore()
    await seedStore(store, [makeCard({ id: 'c1' })])

    mockApiFetch.mockResolvedValueOnce(undefined)
    await store.deleteCard('c1')

    expect(mockApiFetch).toHaveBeenCalledWith('/cards/c1', { method: 'DELETE' })
  })

  it('deleteCard removes only the target card, leaves others unchanged', async () => {
    const card1 = makeCard({ id: 'c1', front: 'Q1' })
    const card2 = makeCard({ id: 'c2', front: 'Q2' })

    const store = useCardsStore()
    await seedStore(store, [card1, card2])

    mockApiFetch.mockResolvedValueOnce(undefined)
    await store.deleteCard('c1')

    expect(store.cards).toHaveLength(1)
    expect(store.cards[0].id).toBe('c2')
  })

  // --- resetProgress ---

  it('resetProgress calls POST with correct endpoint', async () => {
    const store = useCardsStore()
    await seedStore(store, [makeCard({ id: 'c1', state: 'review' })])

    const resetCard = makeCard({ id: 'c1', state: 'new' })
    mockApiFetch.mockResolvedValueOnce(resetCard)
    await store.resetProgress('c1')

    expect(mockApiFetch).toHaveBeenCalledWith('/cards/c1/reset', { method: 'POST' })
  })

  it('resetProgress updates only the target card, leaves others unchanged', async () => {
    const card1 = makeCard({ id: 'c1', state: 'review', stability: 5.0 })
    const card2 = makeCard({ id: 'c2', state: 'learning', stability: 3.0 })

    const store = useCardsStore()
    await seedStore(store, [card1, card2])

    const resetCard = makeCard({ id: 'c1', state: 'new', stability: null })
    mockApiFetch.mockResolvedValueOnce(resetCard)
    await store.resetProgress('c1')

    expect(store.cards).toHaveLength(2)
    expect(store.cards[0].state).toBe('new')
    expect(store.cards[0].stability).toBeNull()
    expect(store.cards[1].state).toBe('learning') // c2 untouched
    expect(store.cards[1].stability).toBe(3.0)
  })

  // --- setSuspended ---

  it('setSuspended calls PUT with correct endpoint and body', async () => {
    const store = useCardsStore()
    await seedStore(store, [makeCard({ id: 'c1' })])

    mockApiFetch.mockResolvedValueOnce(makeCard({ id: 'c1', isSuspended: true }))
    await store.setSuspended('c1', true)

    expect(mockApiFetch).toHaveBeenCalledWith('/cards/c1/suspended', {
      method: 'PUT',
      body: JSON.stringify({ isSuspended: true }),
    })
  })

  it('setSuspended updates only the target card, leaves others unchanged', async () => {
    const card1 = makeCard({ id: 'c1', isSuspended: false })
    const card2 = makeCard({ id: 'c2', isSuspended: false })

    const store = useCardsStore()
    await seedStore(store, [card1, card2])

    mockApiFetch.mockResolvedValueOnce(makeCard({ id: 'c1', isSuspended: true }))
    await store.setSuspended('c1', true)

    expect(store.cards).toHaveLength(2)
    expect(store.cards[0].isSuspended).toBe(true)
    expect(store.cards[1].isSuspended).toBe(false) // c2 untouched
  })

  it('setSuspended round-trips: suspend then unsuspend', async () => {
    const store = useCardsStore()
    await seedStore(store, [makeCard({ id: 'c1', isSuspended: false })])

    mockApiFetch.mockResolvedValueOnce(makeCard({ id: 'c1', isSuspended: true }))
    await store.setSuspended('c1', true)
    expect(store.cards[0].isSuspended).toBe(true)

    mockApiFetch.mockResolvedValueOnce(makeCard({ id: 'c1', isSuspended: false }))
    await store.setSuspended('c1', false)
    expect(store.cards[0].isSuspended).toBe(false)
  })

  // --- Array reference replacement (reactivity) ---

  it('mutation methods replace the array reference (not in-place)', async () => {
    const store = useCardsStore()
    await seedStore(store, [makeCard({ id: 'c1' }), makeCard({ id: 'c2' })])

    // updateCard
    let before = store.cards
    mockApiFetch.mockResolvedValueOnce(makeCard({ id: 'c1', front: 'new' }))
    await store.updateCard('c1', { front: 'new', back: 'A' })
    expect(store.cards).not.toBe(before)

    // resetProgress
    before = store.cards
    mockApiFetch.mockResolvedValueOnce(makeCard({ id: 'c1' }))
    await store.resetProgress('c1')
    expect(store.cards).not.toBe(before)

    // setSuspended
    before = store.cards
    mockApiFetch.mockResolvedValueOnce(makeCard({ id: 'c1', isSuspended: true }))
    await store.setSuspended('c1', true)
    expect(store.cards).not.toBe(before)

    // deleteCard
    before = store.cards
    mockApiFetch.mockResolvedValueOnce(undefined)
    await store.deleteCard('c1')
    expect(store.cards).not.toBe(before)
  })
})
