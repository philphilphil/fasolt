<script setup lang="ts">
import type { Card } from '@/types'

const props = defineProps<{ card: Card; isFlipped: boolean }>()
defineEmits<{ flip: [] }>()
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
      class="mt-3 text-center text-[17px] leading-relaxed"
      :class="isFlipped ? 'text-muted-foreground' : 'text-foreground'"
    >
      {{ card.question }}
    </div>
    <div v-if="isFlipped" class="mt-4 text-center text-[17px] leading-relaxed text-foreground">
      {{ card.answer }}
    </div>
    <div v-if="card.sourceSection" class="mt-3 font-mono text-[11px] text-muted-foreground">
      {{ card.sourceFile }} → {{ card.sourceSection }}
    </div>
  </div>
</template>
