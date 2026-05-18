<script setup lang="ts">
import { Layers, FolderMinus, Pause, Play, Trash2, X } from 'lucide-vue-next'

const props = defineProps<{
  count: number
  someSuspended: boolean
  allSuspended: boolean
  canRemoveFromDeck: boolean
}>()

defineEmits<{
  addToDeck: []
  removeFromDeck: []
  suspend: []
  unsuspend: []
  delete: []
  clear: []
}>()

void props
</script>

<template>
  <div class="bulk-bar" role="region" aria-label="Bulk actions">
    <div class="bulk-bar-left">
      <span class="fa-num bulk-count">{{ count }}</span>
      <span class="bulk-label">selected</span>
    </div>
    <div class="bulk-bar-actions">
      <button class="bulk-action" @click="$emit('addToDeck')">
        <Layers :size="13" />
        Add to deck
      </button>
      <button
        v-if="canRemoveFromDeck"
        class="bulk-action"
        @click="$emit('removeFromDeck')"
      >
        <FolderMinus :size="13" />
        Remove from deck
      </button>
      <button v-if="!allSuspended" class="bulk-action" @click="$emit('suspend')">
        <Pause :size="13" />
        {{ someSuspended ? 'Suspend rest' : 'Suspend' }}
      </button>
      <button v-if="someSuspended || allSuspended" class="bulk-action" @click="$emit('unsuspend')">
        <Play :size="13" />
        Unsuspend
      </button>
      <button class="bulk-action is-danger" @click="$emit('delete')">
        <Trash2 :size="13" />
        Delete
      </button>
    </div>
    <button class="bulk-clear" aria-label="Clear selection" @click="$emit('clear')">
      <X :size="14" />
    </button>
  </div>
</template>

<style scoped>
.bulk-bar {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 9px 14px;
  border: 1px solid var(--accent);
  background: var(--accent-soft);
  border-radius: 10px;
  animation: bar-pop .2s cubic-bezier(.4,0,.2,1) both;
}
@keyframes bar-pop {
  from { opacity: 0; transform: translateY(-4px); }
  to   { opacity: 1; transform: translateY(0); }
}
.bulk-bar-left {
  display: flex;
  align-items: baseline;
  gap: 6px;
  padding-right: 12px;
  border-right: 1px solid color-mix(in oklch, var(--accent) 25%, transparent);
}
.bulk-count {
  font-size: 20px;
  line-height: 1;
  font-weight: 500;
  color: var(--accent-hi);
}
.bulk-label {
  font-family: 'Geist Mono', ui-monospace, monospace;
  text-transform: uppercase;
  font-size: 10px;
  letter-spacing: 0.18em;
  color: var(--accent-hi);
}
.bulk-bar-actions {
  display: flex;
  align-items: center;
  gap: 4px;
  flex: 1;
  flex-wrap: wrap;
}
.bulk-action {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 28px;
  padding: 0 10px;
  font-size: 12.5px;
  font-weight: 500;
  background: transparent;
  border: 1px solid transparent;
  border-radius: 6px;
  color: var(--accent-hi);
  cursor: pointer;
  transition: background .12s, border-color .12s, color .12s;
}
.bulk-action:hover {
  background: var(--paper-1);
  border-color: var(--accent);
  color: var(--ink-0);
}
.bulk-action.is-danger { color: var(--c-again); }
.bulk-action.is-danger:hover {
  border-color: var(--c-again);
  background: color-mix(in oklch, var(--c-again) 8%, var(--paper-1));
  color: var(--c-again);
}
.bulk-clear {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 5px;
  background: transparent;
  border: 1px solid transparent;
  color: var(--accent-hi);
  cursor: pointer;
  transition: background .12s, border-color .12s;
}
.bulk-clear:hover {
  background: var(--paper-1);
  border-color: var(--accent);
}
</style>
