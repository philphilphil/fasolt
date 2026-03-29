import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { DeckSnapshot, SnapshotDiff } from '@/types'
import { apiFetch } from '@/api/client'

export const useSnapshotsStore = defineStore('snapshots', () => {
  const snapshots = ref<DeckSnapshot[]>([])
  const loading = ref(false)

  async function createAll(): Promise<{ count: number }> {
    return apiFetch<{ count: number }>('/snapshots', { method: 'POST' })
  }

  async function fetchByDeck(deckId: string) {
    loading.value = true
    try {
      snapshots.value = await apiFetch<DeckSnapshot[]>(`/decks/${deckId}/snapshots`)
    } finally {
      loading.value = false
    }
  }

  async function getDiff(snapshotId: string): Promise<SnapshotDiff> {
    return apiFetch<SnapshotDiff>(`/snapshots/${snapshotId}/diff`)
  }

  async function restore(snapshotId: string, restoreDeletedCardIds: string[], revertModifiedCardIds: string[]) {
    await apiFetch(`/snapshots/${snapshotId}/restore`, {
      method: 'POST',
      body: JSON.stringify({ restoreDeletedCardIds, revertModifiedCardIds }),
    })
  }

  return { snapshots, loading, createAll, fetchByDeck, getDiff, restore }
})
