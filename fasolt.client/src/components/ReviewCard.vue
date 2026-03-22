<script setup lang="ts">
import type { DueCard } from '@/types'
import { useMarkdown } from '@/composables/useMarkdown'

defineProps<{ card: DueCard; isFlipped: boolean }>()
defineEmits<{ flip: [] }>()

const { render } = useMarkdown()
</script>

<template>
  <div
    class="flex flex-1 cursor-pointer flex-col items-center justify-center rounded-lg border border-border bg-card p-5 sm:p-8"
    @click="$emit('flip')"
  >
    <div class="text-[11px] uppercase tracking-widest text-muted-foreground">
      {{ isFlipped ? 'Answer' : 'Question' }}
    </div>
    <div
      class="mt-3 w-full max-w-lg text-center"
      :class="isFlipped ? 'text-muted-foreground' : 'text-foreground'"
    >
      <div class="prose prose-sm dark:prose-invert max-w-none" v-html="render(card.front)" />
    </div>
    <div v-if="isFlipped" class="mt-4 w-full max-w-lg text-center">
      <div class="prose prose-sm dark:prose-invert max-w-none" v-html="render(card.back)" />
    </div>
    <div v-if="card.sourceHeading" class="mt-3 font-mono text-[11px] text-muted-foreground">
      {{ card.sourceHeading }}
    </div>
  </div>
</template>
