import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { MarkdownFile, FileDetail, BulkUploadResult } from '@/types'
import { apiFetch, apiUpload } from '@/api/client'

export const useFilesStore = defineStore('files', () => {
  const files = ref<MarkdownFile[]>([])
  const loading = ref(false)

  async function fetchFiles() {
    loading.value = true
    try {
      files.value = await apiFetch<MarkdownFile[]>('/files')
    } finally {
      loading.value = false
    }
  }

  async function uploadFile(file: File): Promise<MarkdownFile> {
    const formData = new FormData()
    formData.append('file', file)
    const result = await apiUpload<MarkdownFile>('/files', formData)
    await fetchFiles()
    return result
  }

  async function uploadFiles(fileList: File[]): Promise<BulkUploadResult[]> {
    const formData = new FormData()
    for (const file of fileList) {
      formData.append('files', file)
    }
    const results = await apiUpload<BulkUploadResult[]>('/files/bulk', formData)
    await fetchFiles()
    return results
  }

  async function deleteFile(id: string) {
    await apiFetch(`/files/${id}`, { method: 'DELETE' })
    files.value = files.value.filter(f => f.id !== id)
  }

  async function getFileContent(id: string): Promise<FileDetail> {
    return apiFetch<FileDetail>(`/files/${id}`)
  }

  return { files, loading, fetchFiles, uploadFile, uploadFiles, deleteFile, getFileContent }
})
