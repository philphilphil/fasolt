import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useCardsStore } from '@/stores/cards'

vi.mock('@/api/client', () => ({
  apiFetch: vi.fn(),
}))

import { apiFetch } from '@/api/client'
const mockApiFetch = vi.mocked(apiFetch)

describe('cards store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('fetchCards with no filter calls /cards', async () => {
    mockApiFetch.mockResolvedValueOnce({ items: [], hasMore: false, nextCursor: null })

    const store = useCardsStore()
    await store.fetchCards()

    expect(mockApiFetch).toHaveBeenCalledWith('/cards')
  })

  it('fetchCards with sourceFile filter calls /cards?sourceFile=xxx', async () => {
    mockApiFetch.mockResolvedValueOnce({ items: [], hasMore: false, nextCursor: null })

    const store = useCardsStore()
    await store.fetchCards('notes.md')

    expect(mockApiFetch).toHaveBeenCalledWith('/cards?sourceFile=notes.md')
  })

  it('fetchCards with sourceFile containing special chars encodes them', async () => {
    mockApiFetch.mockResolvedValueOnce({ items: [], hasMore: false, nextCursor: null })

    const store = useCardsStore()
    await store.fetchCards('my notes/cap theorem.md')

    const call = mockApiFetch.mock.calls[0][0] as string
    expect(call).toContain('/cards?sourceFile=')
    expect(call).not.toContain(' ')
  })

  it('createCard includes sourceFile in request body', async () => {
    const mockCard = {
      id: 'c1',
      sourceFile: 'notes.md',
      sourceHeading: null,
      front: 'Q',
      back: 'A',
      createdAt: '2024-01-01T00:00:00Z',
      easeFactor: 2.5,
      interval: 1,
      repetitions: 0,
      dueAt: null,
      state: 'new' as const,
      decks: [],
    }
    mockApiFetch.mockResolvedValueOnce(mockCard)

    const store = useCardsStore()
    const result = await store.createCard({
      sourceFile: 'notes.md',
      sourceHeading: '## CAP Theorem',
      front: 'Q',
      back: 'A',
    })

    expect(mockApiFetch).toHaveBeenCalledWith('/cards', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({
        sourceFile: 'notes.md',
        sourceHeading: '## CAP Theorem',
        front: 'Q',
        back: 'A',
      }),
    }))
    expect(result.sourceFile).toBe('notes.md')
  })

  it('createCard does NOT include fileId in request body', async () => {
    const mockCard = {
      id: 'c1', sourceFile: 'notes.md', sourceHeading: null,
      front: 'Q', back: 'A', createdAt: '2024-01-01T00:00:00Z',
      easeFactor: 2.5, interval: 1, repetitions: 0, dueAt: null,
      state: 'new' as const, decks: [],
    }
    mockApiFetch.mockResolvedValueOnce(mockCard)

    const store = useCardsStore()
    await store.createCard({ sourceFile: 'notes.md', front: 'Q', back: 'A' })

    const body = JSON.parse((mockApiFetch.mock.calls[0][1] as RequestInit).body as string)
    expect(body).not.toHaveProperty('fileId')
  })

  it('extractContent method does NOT exist on cards store', () => {
    const store = useCardsStore()
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    expect((store as any)['extractContent']).toBeUndefined()
  })
})
