<script setup lang="ts">
import { Layers, FolderMinus, Pause, Play, Trash2, X } from 'lucide-vue-next'

withDefaults(defineProps<{
  count: number
  someSuspended: boolean
  allSuspended: boolean
  canRemoveFromDeck: boolean
  busy?: boolean
}>(), { busy: false })

defineEmits<{
  addToDeck: []
  removeFromDeck: []
  suspend: []
  unsuspend: []
  delete: []
  clear: []
}>()
</script>

<template>
  <div
    class="bulk-bar"
    :class="{ 'is-busy': busy }"
    role="region"
    aria-label="Bulk actions"
    :aria-busy="busy"
  >
    <div class="bulk-bar-left">
      <span class="fa-num bulk-count">{{ count }}</span>
      <span class="fa-cap bulk-label">selected</span>
    </div>
    <div class="bulk-bar-actions">
      <button class="bulk-action" :disabled="busy" @click="$emit('addToDeck')">
        <Layers :size="13" />
        Add to deck
      </button>
      <button
        v-if="canRemoveFromDeck"
        class="bulk-action"
        :disabled="busy"
        @click="$emit('removeFromDeck')"
      >
        <FolderMinus :size="13" />
        Remove from deck
      </button>
      <button v-if="!allSuspended" class="bulk-action" :disabled="busy" @click="$emit('suspend')">
        <Pause :size="13" />
        {{ someSuspended ? 'Suspend rest' : 'Suspend' }}
      </button>
      <button v-if="someSuspended || allSuspended" class="bulk-action" :disabled="busy" @click="$emit('unsuspend')">
        <Play :size="13" />
        Unsuspend
      </button>
      <button class="bulk-action is-danger" :disabled="busy" @click="$emit('delete')">
        <Trash2 :size="13" />
        Delete
      </button>
    </div>
    <button class="bulk-clear" :disabled="busy" aria-label="Clear selection" @click="$emit('clear')">
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
  border: 1px solid var(--rule-1);
  background: var(--paper-1);
  border-radius: 12px;
  box-shadow: var(--sh-2);
  animation: bar-fade .15s ease both;
  transition: opacity .12s;
}
.bulk-bar.is-busy {
  opacity: 0.65;
  pointer-events: none;
}
.bulk-action:disabled,
.bulk-clear:disabled {
  cursor: progress;
  opacity: 0.6;
}
@keyframes bar-fade {
  from { opacity: 0; }
  to   { opacity: 1; }
}
.bulk-bar-left {
  display: flex;
  align-items: baseline;
  gap: 6px;
  padding-right: 12px;
  border-right: 1px solid var(--rule-1);
}
.bulk-count {
  font-size: 20px;
  line-height: 1;
  font-weight: 600;
  color: var(--accent-text);
}
.bulk-label {
  font-size: 11px;
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
  border-radius: 7px;
  color: var(--ink-0);
  cursor: pointer;
  transition: background .12s, color .12s;
}
.bulk-action:hover {
  background: var(--paper-2);
  color: var(--ink-0);
}
.bulk-action.is-danger { color: var(--c-again); }
.bulk-action.is-danger:hover {
  background: var(--paper-2);
  color: var(--c-again);
}
.bulk-clear {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 7px;
  background: transparent;
  border: 1px solid transparent;
  color: var(--ink-2);
  cursor: pointer;
  transition: background .12s, color .12s;
}
.bulk-clear:hover {
  background: var(--paper-2);
  color: var(--ink-0);
}
</style>
