import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { MarkdownFile } from '@/types'

export const useFilesStore = defineStore('files', () => {
  const files = ref<MarkdownFile[]>([
    {
      id: 'f1', name: 'distributed-systems.md', cardCount: 24,
      uploadedAt: new Date('2026-03-15'), sizeBytes: 14200,
      headings: [
        { level: 2, text: 'CAP Theorem', cardCount: 6 },
        { level: 2, text: 'Consensus Algorithms', cardCount: 8 },
        { level: 2, text: 'Replication', cardCount: 10 },
      ],
    },
    {
      id: 'f2', name: 'rust-ownership.md', cardCount: 18,
      uploadedAt: new Date('2026-03-16'), sizeBytes: 9800,
      headings: [
        { level: 2, text: 'Ownership Rules', cardCount: 5 },
        { level: 2, text: 'Borrowing', cardCount: 7 },
        { level: 2, text: 'Lifetimes', cardCount: 6 },
      ],
    },
    {
      id: 'f3', name: 'system-design.md', cardCount: 31,
      uploadedAt: new Date('2026-03-18'), sizeBytes: 22400,
      headings: [
        { level: 2, text: 'Load Balancing', cardCount: 8 },
        { level: 2, text: 'Caching', cardCount: 10 },
        { level: 2, text: 'Message Queues', cardCount: 13 },
      ],
    },
    {
      id: 'f4', name: 'postgresql-internals.md', cardCount: 11,
      uploadedAt: new Date('2026-03-19'), sizeBytes: 7600,
      headings: [
        { level: 2, text: 'MVCC', cardCount: 4 },
        { level: 2, text: 'Query Planning', cardCount: 7 },
      ],
    },
  ])

  return { files }
})
