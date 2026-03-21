<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useFilesStore } from '@/stores/files'
import { useMarkdown } from '@/composables/useMarkdown'
import type { FileDetail } from '@/types'
import { Button } from '@/components/ui/button'

const route = useRoute()
const router = useRouter()
const files = useFilesStore()
const { render, stripFrontmatter } = useMarkdown()

const file = ref<FileDetail | null>(null)
const loading = ref(true)
const showSource = ref(false)

onMounted(async () => {
  try {
    file.value = await files.getFileContent(route.params.id as string)
  } catch {
    router.replace('/files')
  } finally {
    loading.value = false
  }
})

const strippedContent = computed(() => {
  if (!file.value) return ''
  return stripFrontmatter(file.value.content)
})

const renderedHtml = computed(() => render(strippedContent.value))

function scrollToHeading(text: string) {
  const headings = document.querySelectorAll('.markdown-body h1, .markdown-body h2, .markdown-body h3, .markdown-body h4, .markdown-body h5, .markdown-body h6')
  for (const el of headings) {
    if (el.textContent?.trim() === text) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' })
      return
    }
  }
}
</script>

<template>
  <div v-if="loading" class="py-12 text-center text-sm text-muted-foreground">Loading...</div>

  <div v-else-if="file" class="space-y-4">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <Button variant="ghost" size="sm" class="h-7 text-xs" @click="router.push('/files')">
          &larr; Files
        </Button>
        <span class="font-mono text-sm font-medium">{{ file.fileName }}</span>
      </div>
      <Button variant="outline" size="sm" class="h-7 text-xs" @click="showSource = !showSource">
        {{ showSource ? 'Preview' : 'Source' }}
      </Button>
    </div>

    <div class="flex gap-4">
      <!-- Heading tree sidebar -->
      <aside v-if="file.headings.length > 0" class="hidden w-56 shrink-0 md:block">
        <div class="sticky top-4 space-y-1">
          <div class="text-[10px] uppercase tracking-wider text-muted-foreground mb-2">Sections</div>
          <div
            v-for="(heading, idx) in file.headings"
            :key="idx"
            class="group flex items-center justify-between text-xs"
          >
            <button
              class="text-left text-muted-foreground hover:text-foreground transition-colors truncate"
              :style="{ paddingLeft: `${(heading.level - 1) * 12}px` }"
              @click="scrollToHeading(heading.text)"
            >
              {{ heading.text }}
            </button>
            <Button
              variant="ghost"
              size="sm"
              class="h-5 text-[10px] opacity-0 group-hover:opacity-50 cursor-not-allowed shrink-0"
              disabled
            >
              Create cards
            </Button>
          </div>
        </div>
      </aside>

      <!-- Content -->
      <div class="min-w-0 flex-1">
        <div v-if="showSource" class="whitespace-pre-wrap rounded-lg border border-border bg-muted/50 p-4 font-mono text-xs">{{ strippedContent }}</div>
        <div v-else class="markdown-body prose prose-sm dark:prose-invert max-w-none" v-html="renderedHtml" />
      </div>
    </div>
  </div>
</template>
