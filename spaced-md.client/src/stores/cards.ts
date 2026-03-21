import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Card, ExtractedContent } from '@/types'
import { apiFetch } from '@/api/client'

export const useCardsStore = defineStore('cards', () => {
  const cards = ref<Card[]>([])
  const loading = ref(false)

  async function fetchCards(fileId?: string) {
    loading.value = true
    try {
      const params = fileId ? `?fileId=${fileId}` : ''
      cards.value = await apiFetch<Card[]>(`/cards${params}`)
    } finally {
      loading.value = false
    }
  }

  async function createCard(data: {
    fileId?: string
    sourceHeading?: string
    front: string
    back: string
    cardType: string
  }): Promise<Card> {
    const result = await apiFetch<Card>('/cards', {
      method: 'POST',
      body: JSON.stringify(data),
    })
    await fetchCards()
    return result
  }

  async function updateCard(id: string, data: { front: string; back: string }): Promise<Card> {
    const result = await apiFetch<Card>(`/cards/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    })
    const idx = cards.value.findIndex(c => c.id === id)
    if (idx !== -1) cards.value[idx] = result
    return result
  }

  async function deleteCard(id: string) {
    await apiFetch(`/cards/${id}`, { method: 'DELETE' })
    cards.value = cards.value.filter(c => c.id !== id)
  }

  async function extractContent(fileId: string, heading?: string): Promise<ExtractedContent> {
    const params = heading ? `?fileId=${fileId}&heading=${encodeURIComponent(heading)}` : `?fileId=${fileId}`
    return apiFetch<ExtractedContent>(`/cards/extract${params}`)
  }

  return { cards, loading, fetchCards, createCard, updateCard, deleteCard, extractContent }
})
