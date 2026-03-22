import { ref } from 'vue'
import { defineStore } from 'pinia'
import { apiFetch } from '@/api/client'
import type { SourceItem } from '@/types'

export const useSourcesStore = defineStore('sources', () => {
  const sources = ref<SourceItem[]>([])
  const loading = ref(false)

  async function fetchSources() {
    loading.value = true
    try {
      const data = await apiFetch<{ items: SourceItem[] }>('/sources')
      sources.value = data.items
    } finally {
      loading.value = false
    }
  }

  return { sources, loading, fetchSources }
})
