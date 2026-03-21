<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useFilesStore } from '@/stores/files'
import { isApiError } from '@/api/client'
import type { BulkUploadResult } from '@/types'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const router = useRouter()
const files = useFilesStore()
const expandedId = ref<string | null>(null)
const sortBy = ref<'name' | 'date' | 'cards'>('date')
const dragging = ref(false)
const uploading = ref(false)
const uploadProgress = ref('')
const uploadError = ref('')
const uploadResults = ref<BulkUploadResult[] | null>(null)
const deleteTarget = ref<{ id: string; name: string } | null>(null)
const fileInput = ref<HTMLInputElement>()

onMounted(() => files.fetchFiles())

const sortedFiles = computed(() => {
  const sorted = [...files.files]
  switch (sortBy.value) {
    case 'name': sorted.sort((a, b) => a.fileName.localeCompare(b.fileName)); break
    case 'date': sorted.sort((a, b) => new Date(b.uploadedAt).getTime() - new Date(a.uploadedAt).getTime()); break
    case 'cards': sorted.sort((a, b) => b.cardCount - a.cardCount); break
  }
  return sorted
})

function toggle(id: string) {
  expandedId.value = expandedId.value === id ? null : id
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  return `${(bytes / 1024).toFixed(1)} KB`
}

function formatDate(date: string): string {
  return new Date(date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

function validateFiles(fileList: File[]): File[] {
  const valid: File[] = []
  const errors: string[] = []
  for (const f of fileList) {
    if (!f.name.endsWith('.md')) {
      errors.push(`${f.name}: not a .md file`)
    } else if (f.size > 1_048_576) {
      errors.push(`${f.name}: exceeds 1MB`)
    } else {
      valid.push(f)
    }
  }
  if (errors.length) uploadError.value = errors.join('. ')
  return valid
}

async function handleFiles(fileList: File[]) {
  uploadError.value = ''
  uploadResults.value = null
  const valid = validateFiles(fileList)
  if (!valid.length) return

  uploading.value = true
  try {
    if (valid.length === 1) {
      uploadProgress.value = `Uploading ${valid[0].name}...`
      await files.uploadFile(valid[0])
      uploadProgress.value = `Uploaded ${valid[0].name}`
    } else {
      uploadProgress.value = `Uploading ${valid.length} files...`
      const results = await files.uploadFiles(valid)
      uploadResults.value = results
      const succeeded = results.filter(r => r.success).length
      uploadProgress.value = `${succeeded}/${results.length} files uploaded`
      const failures = results.filter(r => !r.success)
      if (failures.length) {
        uploadError.value = failures.map(f => `${f.fileName}: ${f.error}`).join('. ')
      }
    }
  } catch (err) {
    if (isApiError(err)) {
      uploadError.value = Object.values(err.errors).flat().join('. ')
    } else {
      uploadError.value = 'Upload failed.'
    }
    uploadProgress.value = ''
  } finally {
    uploading.value = false
  }
}

function onDrop(e: DragEvent) {
  dragging.value = false
  const droppedFiles = Array.from(e.dataTransfer?.files ?? [])
  if (droppedFiles.length) handleFiles(droppedFiles)
}

function onFileSelect(e: Event) {
  const input = e.target as HTMLInputElement
  const selected = Array.from(input.files ?? [])
  if (selected.length) handleFiles(selected)
  input.value = ''
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  await files.deleteFile(deleteTarget.value.id)
  deleteTarget.value = null
}
</script>

<template>
  <div class="space-y-4">
    <!-- Upload zone -->
    <div
      class="flex flex-col items-center justify-center gap-2 rounded-lg border-2 border-dashed p-8 text-center text-sm transition-colors"
      :class="dragging ? 'border-primary bg-primary/5 text-primary' : 'border-border text-muted-foreground'"
      @dragover.prevent="dragging = true"
      @dragleave.prevent="dragging = false"
      @drop.prevent="onDrop"
      @click="fileInput?.click()"
    >
      <input ref="fileInput" type="file" accept=".md" multiple hidden @change="onFileSelect" />
      <template v-if="uploading">
        <span>{{ uploadProgress }}</span>
      </template>
      <template v-else>
        <span>Drop .md files here or click to upload</span>
      </template>
    </div>

    <!-- Upload feedback -->
    <div v-if="uploadProgress && !uploading" class="text-xs text-green-600 dark:text-green-400">
      {{ uploadProgress }}
    </div>
    <div v-if="uploadError" class="text-xs text-destructive">
      {{ uploadError }}
    </div>

    <!-- Sort controls -->
    <div v-if="files.files.length > 0" class="flex gap-1">
      <Button
        v-for="s in [{ key: 'date', label: 'Date' }, { key: 'name', label: 'Name' }, { key: 'cards', label: 'Cards' }]"
        :key="s.key"
        variant="ghost"
        size="sm"
        class="h-7 text-[10px]"
        :class="sortBy === s.key ? 'bg-accent' : ''"
        @click="sortBy = s.key as typeof sortBy"
      >
        {{ s.label }}
      </Button>
    </div>

    <!-- File table -->
    <Table v-if="files.files.length > 0">
      <TableHeader>
        <TableRow class="text-[10px] uppercase tracking-wider text-muted-foreground hover:bg-transparent">
          <TableHead class="h-8">File</TableHead>
          <TableHead class="h-8">Cards</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Uploaded</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Size</TableHead>
          <TableHead class="h-8 w-12"></TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <template v-for="file in sortedFiles" :key="file.id">
          <TableRow class="cursor-pointer text-xs" @click="toggle(file.id)">
            <TableCell class="font-mono font-medium text-foreground">
              <router-link
                :to="`/files/${file.id}`"
                class="hover:underline"
                @click.stop
              >
                {{ file.fileName }}
              </router-link>
            </TableCell>
            <TableCell class="font-mono text-muted-foreground">{{ file.cardCount }}</TableCell>
            <TableCell class="hidden text-muted-foreground sm:table-cell">{{ formatDate(file.uploadedAt) }}</TableCell>
            <TableCell class="hidden text-muted-foreground sm:table-cell">{{ formatSize(file.sizeBytes) }}</TableCell>
            <TableCell>
              <Button
                variant="ghost"
                size="sm"
                class="h-6 w-6 p-0 text-muted-foreground hover:text-destructive"
                @click.stop="deleteTarget = { id: file.id, name: file.fileName }"
              >
                &times;
              </Button>
            </TableCell>
          </TableRow>
          <TableRow v-if="expandedId === file.id" class="hover:bg-transparent">
            <TableCell :colspan="5" class="p-0">
              <div class="space-y-1 border-t border-border px-4 py-3">
                <div
                  v-for="heading in file.headings"
                  :key="heading.text"
                  class="flex items-center justify-between text-xs"
                >
                  <span class="text-muted-foreground" :style="{ paddingLeft: `${(heading.level - 1) * 12}px` }">
                    {{ '#'.repeat(heading.level) }} {{ heading.text }}
                  </span>
                  <Button variant="ghost" size="sm" class="h-6 text-[10px] opacity-50 cursor-not-allowed" disabled>
                    Create cards
                  </Button>
                </div>
              </div>
            </TableCell>
          </TableRow>
        </template>
      </TableBody>
    </Table>

    <!-- Empty state -->
    <div v-if="!files.loading && files.files.length === 0" class="py-12 text-center text-sm text-muted-foreground">
      No files uploaded yet. Drop a .md file above to get started.
    </div>

    <!-- Delete confirmation dialog -->
    <Dialog :open="!!deleteTarget" @update:open="deleteTarget = null">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete file</DialogTitle>
          <DialogDescription>
            Are you sure you want to delete {{ deleteTarget?.name }}? This cannot be undone.
          </DialogDescription>
        </DialogHeader>
        <div class="flex items-center gap-2 text-xs text-muted-foreground opacity-50">
          <input type="checkbox" disabled />
          <span>Also delete associated flashcards (coming soon)</span>
        </div>
        <DialogFooter>
          <Button variant="outline" @click="deleteTarget = null">Cancel</Button>
          <Button variant="destructive" @click="confirmDelete">Delete</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
