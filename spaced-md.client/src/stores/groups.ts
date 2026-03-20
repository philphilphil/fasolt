import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Group } from '@/types'

export const useGroupsStore = defineStore('groups', () => {
  const groups = ref<Group[]>([
    { id: 'g1', name: 'Interview Prep', cardCount: 42, dueCount: 8 },
    { id: 'g2', name: 'Backend Deep Dive', cardCount: 35, dueCount: 5 },
  ])

  function addGroup(name: string) {
    groups.value.push({ id: `g${Date.now()}`, name, cardCount: 0, dueCount: 0 })
  }

  function deleteGroup(id: string) {
    groups.value = groups.value.filter(g => g.id !== id)
  }

  return { groups, addGroup, deleteGroup }
})
