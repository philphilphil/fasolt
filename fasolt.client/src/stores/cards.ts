import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Card } from '@/types'
import { apiFetch } from '@/api/client'
import type { PaginatedResponse } from '@/api/client'

export const useCardsStore = defineStore('cards', () => {
  const cards = ref<Card[]>([])
  const loading = ref(false)

  async function fetchCards(sourceFile?: string) {
    loading.value = true
    try {
      const params = new URLSearchParams({ limit: '200' })
      if (sourceFile) params.set('sourceFile', sourceFile)
      const response = await apiFetch<PaginatedResponse<Card>>(`/cards?${params}`)
      cards.value = response.items
    } finally {
      loading.value = false
    }
  }

  async function createCard(data: {
    sourceFile?: string
    sourceHeading?: string
    front: string
    back: string
  }): Promise<Card> {
    const result = await apiFetch<Card>('/cards', {
      method: 'POST',
      body: JSON.stringify(data),
    })
    return result
  }

  async function updateCard(id: string, data: { front: string; back: string; frontSvg?: string | null; backSvg?: string | null }): Promise<Card> {
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

  async function getCard(id: string): Promise<Card> {
    return apiFetch<Card>(`/cards/${id}`)
  }

  return { cards, loading, fetchCards, getCard, createCard, updateCard, deleteCard }
})
