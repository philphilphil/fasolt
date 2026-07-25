import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Deck, LibraryDeck, LibraryDeckDetail, LibraryListResponse, LibrarySort } from '@/types'
import { apiFetch } from '@/api/client'

export interface LibraryQuery {
  q?: string
  sort?: LibrarySort
  page?: number
}

export const useLibraryStore = defineStore('library', () => {
  const decks = ref<LibraryDeck[]>([])
  const totalCount = ref(0)
  const page = ref(1)
  const pageSize = ref(24)
  const loading = ref(false)

  async function fetchDecks({ q, sort, page: requestedPage }: LibraryQuery = {}) {
    loading.value = true
    try {
      const params = new URLSearchParams()
      if (q) params.set('q', q)
      if (sort) params.set('sort', sort)
      if (requestedPage && requestedPage > 1) params.set('page', String(requestedPage))
      const qs = params.toString()
      const result = await apiFetch<LibraryListResponse>(`/library${qs ? `?${qs}` : ''}`)
      decks.value = result.items
      totalCount.value = result.totalCount
      page.value = result.page
      pageSize.value = result.pageSize
    } finally {
      loading.value = false
    }
  }

  function getDeck(publicId: string): Promise<LibraryDeckDetail> {
    return apiFetch<LibraryDeckDetail>(`/library/decks/${publicId}`)
  }

  /** Clones a public/unlisted deck into the caller's account. Returns the new deck. */
  function copyDeck(publicId: string): Promise<Deck> {
    return apiFetch<Deck>(`/library/decks/${publicId}/copy`, { method: 'POST' })
  }

  return { decks, totalCount, page, pageSize, loading, fetchDecks, getDeck, copyDeck }
})
