<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { useSnapshotsStore } from '@/stores/snapshots'
import { useDecksStore } from '@/stores/decks'
import type { DeckDetail } from '@/types'
import { Button } from '@/components/ui/button'
import RestoreDialog from '@/components/RestoreDialog.vue'

const route = useRoute()
const router = useRouter()
const snapshotsStore = useSnapshotsStore()
const decksStore = useDecksStore()

const deckId = route.params.id as string
const deck = ref<DeckDetail | null>(null)
const loading = ref(true)

const restoreOpen = ref(false)
const selectedSnapshotId = ref('')

onMounted(async () => {
  try {
    const [d] = await Promise.all([
      decksStore.getDeckDetail(deckId),
      snapshotsStore.fetchByDeck(deckId),
    ])
    deck.value = d
  } catch {
    router.replace('/decks')
  } finally {
    loading.value = false
  }
})

function formatDate(iso: string) {
  const d = new Date(iso)
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
    + ' at '
    + d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
}

function openRestore(snapshotId: string) {
  selectedSnapshotId.value = snapshotId
  restoreOpen.value = true
}

async function onRestored() {
  restoreOpen.value = false
  await snapshotsStore.fetchByDeck(deckId)
}
</script>

<template>
  <div v-if="loading" class="py-12 text-center text-xs text-muted-foreground">Loading...</div>

  <div v-else class="space-y-6">
    <!-- Breadcrumb -->
    <div class="text-[11px] text-muted-foreground">
      <RouterLink to="/decks" class="hover:text-foreground transition-colors">Decks</RouterLink>
      <span class="mx-1.5">/</span>
      <RouterLink :to="`/decks/${deckId}`" class="hover:text-foreground transition-colors">{{ deck?.name }}</RouterLink>
      <span class="mx-1.5">/</span>
      <span class="text-foreground">Snapshots</span>
    </div>

    <!-- Header -->
    <div class="flex items-start justify-between">
      <div>
        <h1 class="text-xl font-bold tracking-tight">Snapshots</h1>
        <p class="text-sm text-muted-foreground mt-1">Restore {{ deck?.name }} to a previous state</p>
      </div>
      <Button variant="outline" size="sm" class="text-xs" @click="router.push(`/decks/${deckId}`)">
        Back to deck
      </Button>
    </div>

    <!-- Snapshots list -->
    <div v-if="snapshotsStore.snapshots.length > 0" class="flex flex-col gap-2">
      <div
        v-for="snapshot in snapshotsStore.snapshots"
        :key="snapshot.id"
        class="flex items-center justify-between rounded-lg border border-border bg-card px-4 py-3"
      >
        <div>
          <div class="text-[13px] font-semibold">{{ formatDate(snapshot.createdAt) }}</div>
          <div class="text-[11px] text-muted-foreground">{{ snapshot.cardCount }} cards at time of snapshot</div>
        </div>
        <Button variant="outline" size="sm" class="text-xs" @click="openRestore(snapshot.id)">
          Restore
        </Button>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="py-12 text-center text-xs text-muted-foreground">
      No snapshots yet. Snapshots are created automatically or via the study page.
    </div>

    <!-- Restore dialog -->
    <RestoreDialog
      v-if="selectedSnapshotId"
      v-model:open="restoreOpen"
      :snapshot-id="selectedSnapshotId"
      :deck-id="deckId"
      @restored="onRestored"
    />
  </div>
</template>
