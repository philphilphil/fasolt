<script setup lang="ts">
import type { ReviewRating } from '@/types'
import { Button } from '@/components/ui/button'

defineEmits<{ rate: [rating: ReviewRating] }>()

const ratings: { key: string; label: string; rating: ReviewRating; highlight?: boolean }[] = [
  { key: '1', label: 'Again', rating: 'again' },
  { key: '2', label: 'Hard', rating: 'hard' },
  { key: '3', label: 'Good', rating: 'good', highlight: true },
  { key: '4', label: 'Easy', rating: 'easy' },
]
</script>

<template>
  <div class="flex justify-center gap-1.5">
    <Button
      v-for="r in ratings"
      :key="r.rating"
      variant="outline"
      size="sm"
      class="font-mono text-[11px]"
      :class="r.highlight ? 'border-success text-success' : ''"
      @click="$emit('rate', r.rating)"
    >
      <span class="hidden text-muted-foreground sm:inline mr-1">{{ r.key }}</span>
      {{ r.label }}
    </Button>
  </div>
</template>
