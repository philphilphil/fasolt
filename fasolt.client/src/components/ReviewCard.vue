<script setup lang="ts">
import type { DueCard } from '@/types'
import { useMarkdown } from '@/composables/useMarkdown'
import { sanitizeSvg } from '@/composables/useSvgSanitizer'
import { Pencil } from 'lucide-vue-next'

defineProps<{ card: DueCard; isFlipped: boolean }>()
defineEmits<{ flip: [] }>()

const { render } = useMarkdown()

function openEdit(cardId: string) {
  window.open(`/cards/${cardId}?edit=true`, '_blank')
}
</script>

<template>
  <div
    class="relative flex min-h-[200px] max-h-[60vh] overflow-y-auto cursor-pointer flex-col items-center justify-center rounded border border-border/60 bg-card p-6 sm:p-10 hover:border-accent/20 transition-colors"
    @click="$emit('flip')"
  >
    <button
      class="absolute right-3 top-3 text-muted-foreground/40 hover:text-muted-foreground transition-colors"
      @click.stop="openEdit(card.id)"
      title="Edit card"
    >
      <Pencil :size="14" />
    </button>
    <div class="text-[10px] uppercase tracking-[0.2em] text-accent/70">
      {{ isFlipped ? 'Answer' : 'Question' }}
    </div>
    <div v-if="card.frontSvg" class="mt-4 flex w-full max-w-lg justify-center">
      <div class="max-h-[300px] w-full [&>svg]:max-h-[300px] [&>svg]:w-full" v-html="sanitizeSvg(card.frontSvg)" />
    </div>
    <div
      class="mt-4 w-full max-w-lg text-center"
      :class="isFlipped ? 'text-muted-foreground' : 'text-foreground'"
    >
      <div class="prose dark:prose-invert max-w-none" v-html="render(card.front)" />
    </div>
    <div v-if="isFlipped && card.backSvg" class="mt-4 flex w-full max-w-lg justify-center">
      <div class="max-h-[300px] w-full [&>svg]:max-h-[300px] [&>svg]:w-full" v-html="sanitizeSvg(card.backSvg)" />
    </div>
    <div v-if="isFlipped" class="mt-5 w-full max-w-lg text-center">
      <div class="prose dark:prose-invert max-w-none" v-html="render(card.back)" />
    </div>
    <div v-if="card.sourceHeading" class="mt-4 text-[11px] text-muted-foreground/60">
      {{ card.sourceHeading }}
    </div>
  </div>
</template>
