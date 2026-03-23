<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'

defineProps<{
  totalCards: number
  ratingCounts: { again: number; hard: number; good: number; easy: number }
}>()

defineEmits<{ done: [] }>()
</script>

<template>
  <div class="flex flex-1 flex-col items-center justify-center gap-6">
    <div class="text-center">
      <div class="text-[40px] font-bold tracking-tight text-accent">
        {{ totalCards }}
      </div>
      <div class="text-xs text-muted-foreground uppercase tracking-[0.15em]">cards reviewed</div>
    </div>
    <Card class="w-full max-w-xs border-border/60">
      <CardContent class="grid grid-cols-4 gap-2 p-4 text-center">
        <div>
          <div class="text-sm font-bold text-destructive">{{ ratingCounts.again }}</div>
          <div class="text-[10px] text-muted-foreground">Again</div>
        </div>
        <div>
          <div class="text-sm font-bold text-warning">{{ ratingCounts.hard }}</div>
          <div class="text-[10px] text-muted-foreground">Hard</div>
        </div>
        <div>
          <div class="text-sm font-bold text-success">{{ ratingCounts.good }}</div>
          <div class="text-[10px] text-muted-foreground">Good</div>
        </div>
        <div>
          <div class="text-sm font-bold text-accent">{{ ratingCounts.easy }}</div>
          <div class="text-[10px] text-muted-foreground">Easy</div>
        </div>
      </CardContent>
    </Card>
    <Button @click="$emit('done')">Back to study</Button>
  </div>
</template>
