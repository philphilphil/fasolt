import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useSourcesStore } from '@/stores/sources'

vi.mock('@/api/client', () => ({
  apiFetch: vi.fn(),
}))

import { apiFetch } from '@/api/client'
const mockApiFetch = vi.mocked(apiFetch)

describe('sources store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('has empty initial state', () => {
    const store = useSourcesStore()
    expect(store.sources).toEqual([])
    expect(store.loading).toBe(false)
  })

  it('fetchSources calls /sources and populates sources', async () => {
    const mockItems = [
      { sourceFile: 'notes.md', cardCount: 5, dueCount: 2 },
      { sourceFile: 'cap.md', cardCount: 3, dueCount: 1 },
    ]
    mockApiFetch.mockResolvedValueOnce({ items: mockItems })

    const store = useSourcesStore()
    await store.fetchSources()

    expect(mockApiFetch).toHaveBeenCalledWith('/sources')
    expect(store.sources).toEqual(mockItems)
  })

  it('sets loading true during fetch and false after', async () => {
    let resolveFetch!: (value: unknown) => void
    const fetchPromise = new Promise((resolve) => { resolveFetch = resolve })
    mockApiFetch.mockReturnValueOnce(fetchPromise as ReturnType<typeof apiFetch>)

    const store = useSourcesStore()
    expect(store.loading).toBe(false)

    const fetchCall = store.fetchSources()
    expect(store.loading).toBe(true)

    resolveFetch({ items: [] })
    await fetchCall

    expect(store.loading).toBe(false)
  })

  it('sets loading false even when fetch throws', async () => {
    mockApiFetch.mockRejectedValueOnce(new Error('network error'))

    const store = useSourcesStore()
    await expect(store.fetchSources()).rejects.toThrow('network error')
    expect(store.loading).toBe(false)
  })
})
