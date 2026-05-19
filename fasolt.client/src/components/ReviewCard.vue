<script setup lang="ts">
import type { DueCard } from '@/types'
import { useMarkdown } from '@/composables/useMarkdown'
import { sanitizeSvg } from '@/composables/useSvgSanitizer'
import { Pencil, Copy, Check, FileText } from 'lucide-vue-next'
import { ref } from 'vue'

defineProps<{ card: DueCard; isFlipped: boolean }>()
defineEmits<{ flip: [] }>()

const { render } = useMarkdown()
const idCopied = ref(false)

async function copyId(cardId: string) {
  await navigator.clipboard.writeText(cardId)
  idCopied.value = true
  setTimeout(() => idCopied.value = false, 2000)
}

function openEdit(cardId: string) {
  window.open(`/cards/${cardId}?edit=true`, '_blank')
}
</script>

<template>
  <div
    class="review-card"
    :class="{ 'is-flipped': isFlipped }"
    @click="!isFlipped && $emit('flip')"
  >
    <!-- Top accent stitch -->
    <div class="card-stitch" :class="{ 'is-strong': isFlipped }" />

    <header class="card-head">
      <span class="fa-cap card-label">{{ isFlipped ? 'Answer' : 'Question' }}</span>
      <div v-if="card.sourceFile" class="card-source">
        <FileText :size="12" />
        <span class="fa-mono">{{ card.sourceFile }}</span>
      </div>
    </header>

    <!-- Front body — when flipped, also still shown as a small re-statement -->
    <div v-if="!isFlipped" class="card-body card-body-front">
      <div v-if="card.frontSvg" class="card-svg" v-html="sanitizeSvg(card.frontSvg)" />
      <div class="card-question" v-html="render(card.front)" />
    </div>

    <div v-else class="card-body card-body-back">
      <div class="card-restate" v-html="render(card.front)" />
      <div v-if="card.backSvg" class="card-svg card-svg-back" v-html="sanitizeSvg(card.backSvg)" />
      <div class="card-answer fa-prose" v-html="render(card.back)" />
    </div>

    <footer class="card-foot">
      <div class="card-foot-meta">
        <button class="card-id" :title="idCopied ? 'Copied!' : 'Copy card ID'" @click.stop="copyId(card.id)">
          <Copy v-if="!idCopied" :size="10" />
          <Check v-else :size="10" />
          <span class="fa-mono">{{ card.id.slice(0, 8) }}</span>
        </button>
        <span class="card-state fa-cap">{{ card.state }}</span>
      </div>
      <button class="card-edit" title="Edit card" @click.stop="openEdit(card.id)">
        <Pencil :size="13" />
      </button>
    </footer>
  </div>
</template>

<style scoped>
.review-card {
  position: relative;
  display: flex;
  flex-direction: column;
  min-height: 280px;
  padding: 28px 32px;
  border: 1px solid var(--rule-1);
  border-radius: 14px;
  background: var(--paper-1);
  box-shadow: var(--sh-2);
  cursor: pointer;
  overflow: hidden;
  animation: card-fade-in .35s cubic-bezier(.4,0,.2,1) both;
}
.review-card.is-flipped {
  cursor: default;
  min-height: 420px;
}
@keyframes card-fade-in {
  from { opacity: 0; transform: translateY(6px); }
  to   { opacity: 1; transform: translateY(0); }
}

.card-stitch {
  position: absolute;
  top: 0;
  left: 24px;
  right: 24px;
  height: 2px;
  background: var(--accent);
  opacity: 0.4;
  border-radius: 0 0 2px 2px;
  transition: opacity .25s;
}
.card-stitch.is-strong { opacity: 1; }

.card-head {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
}
.card-label { color: var(--accent); }
.card-source {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  color: var(--ink-2);
  min-width: 0;
  max-width: 60%;
  flex-wrap: wrap;
  justify-content: flex-end;
}
.card-source-sep { color: var(--ink-3); }
.card-source-heading { color: var(--ink-1); }

.card-body {
  flex: 1;
  display: flex;
  flex-direction: column;
}
.card-body-front {
  align-items: center;
  justify-content: center;
  padding: 28px 0;
  gap: 20px;
}
.card-question {
  font-family: 'Instrument Serif', serif;
  font-size: 44px;
  line-height: 1.1;
  letter-spacing: -0.02em;
  text-align: center;
  max-width: 640px;
  color: var(--ink-0);
}
.card-question :deep(p) { margin: 0; }
.card-question :deep(code) {
  font-family: 'Geist Mono', monospace;
  font-size: 0.65em;
  vertical-align: baseline;
  padding: 0 6px;
  background: var(--paper-2);
  border-radius: 4px;
  color: var(--ink-0);
}

.card-body-back { padding-top: 18px; gap: 14px; }
.card-restate {
  font-size: 13px;
  color: var(--ink-2);
  margin-bottom: 4px;
}
.card-restate :deep(p) { margin: 0; }

.card-svg {
  width: 100%;
  max-width: 100%;
  display: flex;
  justify-content: center;
}
.card-svg :deep(svg) {
  max-height: 300px;
  width: 100%;
  height: auto;
}
.card-svg-back :deep(svg) { max-height: 240px; }

.card-prose { color: var(--ink-1); }
.card-prose :deep(p) { margin: 0 0 0.6em; line-height: 1.6; font-size: 15px; }
.card-prose :deep(strong) { color: var(--ink-0); font-weight: 600; }
.card-prose :deep(code) {
  font-family: 'Geist Mono', monospace;
  font-size: 0.92em;
  padding: 1px 5px;
  background: var(--paper-2);
  border: 1px solid var(--rule-1);
  border-radius: 4px;
  color: var(--ink-0);
}
.card-prose :deep(pre) {
  margin: 12px 0;
  padding: 12px 14px;
  background: var(--paper-2);
  border: 1px solid var(--rule-1);
  border-radius: 6px;
  overflow-x: auto;
  font-family: 'Geist Mono', monospace;
  font-size: 13px;
}
.card-prose :deep(pre code) { background: transparent; border: none; padding: 0; }
.card-prose :deep(ul), .card-prose :deep(ol) { padding-left: 1.4em; margin: 0 0 0.6em; }

.card-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: auto;
  padding-top: 14px;
  border-top: 1px solid var(--rule-1);
}
.card-foot-meta {
  display: flex;
  align-items: center;
  gap: 14px;
}
.card-id {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 11px;
  color: var(--ink-3);
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
  transition: color .12s;
}
.card-id:hover { color: var(--ink-1); }
.card-state { font-size: 9px; }
.card-edit {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 5px;
  background: none;
  border: 1px solid transparent;
  color: var(--ink-3);
  cursor: pointer;
  transition: color .12s, border-color .12s, background .12s;
}
.card-edit:hover {
  color: var(--ink-0);
  border-color: var(--rule-1);
  background: var(--paper-2);
}
</style>
