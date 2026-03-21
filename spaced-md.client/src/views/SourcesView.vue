<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useSourcesStore } from '@/stores/sources'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'

const router = useRouter()
const sourcesStore = useSourcesStore()

onMounted(() => sourcesStore.fetchSources())
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <h1 class="text-lg font-semibold tracking-tight">Sources</h1>
    </div>

    <p class="text-xs text-muted-foreground">
      Source files that cards have been created from. Use the MCP agent to create cards from your markdown notes.
    </p>

    <div v-if="sourcesStore.loading" class="py-12 text-center text-sm text-muted-foreground">Loading...</div>

    <div v-else class="grid gap-2.5 sm:grid-cols-2">
      <Card
        v-for="source in sourcesStore.sources"
        :key="source.sourceFile"
        class="cursor-pointer border-border"
        @click="router.push(`/cards?sourceFile=${encodeURIComponent(source.sourceFile)}`)"
      >
        <CardContent class="flex items-center justify-between p-4">
          <div class="min-w-0">
            <div class="truncate font-mono text-sm font-medium text-foreground">{{ source.sourceFile }}</div>
            <div class="mt-0.5 text-[11px] text-muted-foreground">{{ source.cardCount }} cards</div>
          </div>
          <div class="ml-3 flex shrink-0 items-center gap-2">
            <Badge v-if="source.dueCount > 0" variant="outline" class="font-mono text-[10px] text-warning">
              {{ source.dueCount }} due
            </Badge>
          </div>
        </CardContent>
      </Card>
    </div>

    <div v-if="!sourcesStore.loading && sourcesStore.sources.length === 0" class="py-12 text-center text-sm text-muted-foreground">
      No sources yet. Use the MCP agent to create cards from your markdown notes.
    </div>
  </div>
</template>
