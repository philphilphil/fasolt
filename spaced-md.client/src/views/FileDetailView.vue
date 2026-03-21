<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useFilesStore } from '@/stores/files'
import { useCardsStore } from '@/stores/cards'
import { useMarkdown } from '@/composables/useMarkdown'
import type { FileDetail } from '@/types'
import { Button } from '@/components/ui/button'
import CardCreateDialog from '@/components/CardCreateDialog.vue'

const route = useRoute()
const router = useRouter()
const files = useFilesStore()
const { render, stripFrontmatter } = useMarkdown()

const file = ref<FileDetail | null>(null)
const loading = ref(true)
const showSource = ref(false)

const cardsStore = useCardsStore()
const createOpen = ref(false)
const createFront = ref('')
const createBack = ref('')
const createHeading = ref<string | undefined>(undefined)
const createType = ref<'file' | 'section'>('file')
const extracting = ref(false)

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

async function createFromFile() {
  if (!file.value) return
  extracting.value = true
  try {
    const content = await cardsStore.extractContent(file.value.id)
    createFront.value = content.front
    createBack.value = content.back
    createHeading.value = undefined
    createType.value = 'file'
    createOpen.value = true
  } finally {
    extracting.value = false
  }
}

async function createFromSection(headingText: string) {
  if (!file.value) return
  extracting.value = true
  try {
    const content = await cardsStore.extractContent(file.value.id, headingText)
    createFront.value = content.front
    createBack.value = content.back
    createHeading.value = headingText
    createType.value = 'section'
    createOpen.value = true
  } finally {
    extracting.value = false
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
      <div class="flex items-center gap-2">
        <Button variant="outline" size="sm" class="h-7 text-xs" :disabled="extracting" @click="createFromFile">
          Create card
        </Button>
        <Button variant="outline" size="sm" class="h-7 text-xs" @click="showSource = !showSource">
          {{ showSource ? 'Preview' : 'Source' }}
        </Button>
      </div>
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
              class="h-5 text-[10px] opacity-0 group-hover:opacity-100 shrink-0"
              @click.stop="createFromSection(heading.text)"
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

    <CardCreateDialog
      v-model:open="createOpen"
      :file-id="file?.id"
      :source-heading="createHeading"
      :initial-front="createFront"
      :initial-back="createBack"
      :card-type="createType"
    />
  </div>
</template>
