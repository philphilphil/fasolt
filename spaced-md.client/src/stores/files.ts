import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { MarkdownFile, FileDetail, BulkUploadResult, FileUpdatePreview } from '@/types'
import { apiFetch, apiUpload } from '@/api/client'
import type { PaginatedResponse } from '@/api/client'

export const useFilesStore = defineStore('files', () => {
  const files = ref<MarkdownFile[]>([])
  const loading = ref(false)

  async function fetchFiles() {
    loading.value = true
    try {
      const response = await apiFetch<PaginatedResponse<MarkdownFile>>('/files')
      files.value = response.items
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

  async function previewUpdate(file: File): Promise<FileUpdatePreview | null> {
    const formData = new FormData()
    formData.append('file', file)
    try {
      return await apiUpload<FileUpdatePreview>('/files/preview-update', formData)
    } catch (err: unknown) {
      if (typeof err === 'object' && err !== null && 'status' in err && (err as { status: number }).status === 404) {
        return null
      }
      throw err
    }
  }

  async function confirmUpdate(fileId: string, file: File, deleteCardIds: string[]): Promise<void> {
    const formData = new FormData()
    formData.append('file', file)
    for (const id of deleteCardIds) {
      formData.append('deleteCardIds', id)
    }
    await apiUpload(`/files/${fileId}/update`, formData)
    await fetchFiles()
  }

  return { files, loading, fetchFiles, uploadFile, uploadFiles, deleteFile, getFileContent, previewUpdate, confirmUpdate }
})
