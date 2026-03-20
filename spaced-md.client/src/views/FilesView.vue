<script setup lang="ts">
import { ref } from 'vue'
import { useFilesStore } from '@/stores/files'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'

const files = useFilesStore()
const expandedId = ref<string | null>(null)

function toggle(id: string) {
  expandedId.value = expandedId.value === id ? null : id
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  return `${(bytes / 1024).toFixed(1)} KB`
}

function formatDate(date: Date): string {
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}
</script>

<template>
  <div class="space-y-4">
    <!-- Upload zone -->
    <div class="flex items-center justify-center rounded-lg border-2 border-dashed border-border p-8 text-center text-sm text-muted-foreground">
      Drop .md files here or click to upload
    </div>

    <!-- File table -->
    <Table>
      <TableHeader>
        <TableRow class="text-[10px] uppercase tracking-wider text-muted-foreground hover:bg-transparent">
          <TableHead class="h-8">File</TableHead>
          <TableHead class="h-8">Cards</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Uploaded</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Size</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <template v-for="file in files.files" :key="file.id">
          <TableRow class="cursor-pointer text-xs" @click="toggle(file.id)">
            <TableCell class="font-mono font-medium text-foreground">{{ file.name }}</TableCell>
            <TableCell class="font-mono text-muted-foreground">{{ file.cardCount }}</TableCell>
            <TableCell class="hidden text-muted-foreground sm:table-cell">{{ formatDate(file.uploadedAt) }}</TableCell>
            <TableCell class="hidden text-muted-foreground sm:table-cell">{{ formatSize(file.sizeBytes) }}</TableCell>
          </TableRow>
          <TableRow v-if="expandedId === file.id" class="hover:bg-transparent">
            <TableCell :colspan="4" class="p-0">
              <div class="space-y-1 border-t border-border px-4 py-3">
                <div
                  v-for="heading in file.headings"
                  :key="heading.text"
                  class="flex items-center justify-between text-xs"
                >
                  <span class="text-muted-foreground">
                    {{ '#'.repeat(heading.level) }} {{ heading.text }}
                  </span>
                  <div class="flex items-center gap-2">
                    <Badge variant="secondary" class="font-mono text-[10px]">
                      {{ heading.cardCount }} cards
                    </Badge>
                    <Button variant="ghost" size="sm" class="h-6 text-[10px]">
                      Create cards
                    </Button>
                  </div>
                </div>
              </div>
            </TableCell>
          </TableRow>
        </template>
      </TableBody>
    </Table>

    <!-- Empty state -->
    <div v-if="files.files.length === 0" class="py-12 text-center text-sm text-muted-foreground">
      No files uploaded yet. Drop a .md file above to get started.
    </div>
  </div>
</template>
