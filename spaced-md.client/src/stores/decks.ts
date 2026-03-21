import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Deck, DeckDetail } from '@/types'
import { apiFetch } from '@/api/client'

export const useDecksStore = defineStore('decks', () => {
  const decks = ref<Deck[]>([])
  const loading = ref(false)

  async function fetchDecks() {
    loading.value = true
    try {
      decks.value = await apiFetch<Deck[]>('/decks')
    } finally {
      loading.value = false
    }
  }

  async function createDeck(name: string, description?: string): Promise<Deck> {
    const result = await apiFetch<Deck>('/decks', {
      method: 'POST',
      body: JSON.stringify({ name, description }),
    })
    decks.value.push(result)
    return result
  }

  async function updateDeck(id: string, name: string, description?: string): Promise<Deck> {
    const result = await apiFetch<Deck>(`/decks/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ name, description }),
    })
    const idx = decks.value.findIndex(d => d.id === id)
    if (idx !== -1) decks.value[idx] = result
    return result
  }

  async function deleteDeck(id: string) {
    await apiFetch(`/decks/${id}`, { method: 'DELETE' })
    decks.value = decks.value.filter(d => d.id !== id)
  }

  async function getDeckDetail(id: string): Promise<DeckDetail> {
    return apiFetch<DeckDetail>(`/decks/${id}`)
  }

  async function addCards(deckId: string, cardIds: string[]) {
    await apiFetch(`/decks/${deckId}/cards`, {
      method: 'POST',
      body: JSON.stringify({ cardIds }),
    })
  }

  async function removeCard(deckId: string, cardId: string) {
    await apiFetch(`/decks/${deckId}/cards/${cardId}`, { method: 'DELETE' })
  }

  return { decks, loading, fetchDecks, createDeck, updateDeck, deleteDeck, getDeckDetail, addCards, removeCard }
})
