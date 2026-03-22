import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Stat } from '@/types'

export const useDashboardStore = defineStore('dashboard', () => {
  const stats = ref<Stat[]>([
    { label: 'Due', value: '…' },
    { label: 'Total', value: '…' },
    { label: 'Studied today', value: '…' },
  ])

  return { stats }
})
