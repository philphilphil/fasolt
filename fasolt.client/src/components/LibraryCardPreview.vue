<script setup lang="ts">
import type { LibrarySampleCard } from '@/types'
import { useMarkdown } from '@/composables/useMarkdown'
import { sanitizeSvg } from '@/composables/useSvgSanitizer'

defineProps<{ card: LibrarySampleCard; isFlipped: boolean }>()
defineEmits<{ flip: [] }>()

const { render } = useMarkdown()
</script>

<template>
  <button
    type="button"
    class="preview-card"
    :class="{ 'is-flipped': isFlipped }"
    :aria-label="isFlipped ? 'Show question' : 'Show answer'"
    @click="$emit('flip')"
  >
    <div class="card-stitch" :class="{ 'is-strong': isFlipped }" />

    <header class="card-head">
      <span class="fa-cap card-label">{{ isFlipped ? 'Answer' : 'Question' }}</span>
      <span class="card-hint">{{ isFlipped ? 'Click to flip back' : 'Click to flip' }}</span>
    </header>

    <div v-if="!isFlipped" class="card-body card-body-front">
      <div v-if="card.frontSvg" class="card-svg" v-html="sanitizeSvg(card.frontSvg)" />
      <div class="card-question" v-html="render(card.front)" />
    </div>

    <div v-else class="card-body card-body-back">
      <div class="card-restate" v-html="render(card.front)" />
      <div v-if="card.backSvg" class="card-svg card-svg-back" v-html="sanitizeSvg(card.backSvg)" />
      <div class="card-answer fa-prose" v-html="render(card.back)" />
    </div>
  </button>
</template>

<style scoped>
/* Mirrors ReviewCard's surface, minus the owner-only affordances
   (card id, edit, source file) that a public preview must not show. */
.preview-card {
  position: relative;
  display: flex;
  flex-direction: column;
  width: 100%;
  min-height: 220px;
  padding: 22px 26px;
  border: 1px solid var(--rule-1);
  border-radius: 14px;
  background: var(--paper-1);
  box-shadow: var(--sh-2);
  text-align: left;
  font: inherit;
  color: inherit;
  cursor: pointer;
  overflow: hidden;
  transition: border-color .12s;
}
.preview-card:hover { border-color: var(--ink-3); }
.preview-card:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }
.preview-card.is-flipped { min-height: 280px; }

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
  align-items: baseline;
  justify-content: space-between;
  gap: 12px;
}
.card-label { color: var(--ink-2); }
.card-hint { font-size: 11.5px; color: var(--ink-3); }

.card-body {
  flex: 1;
  display: flex;
  flex-direction: column;
}
.card-body-front {
  align-items: center;
  justify-content: center;
  padding: 22px 0;
  gap: 18px;
}
.card-question {
  font-size: 24px;
  font-weight: 600;
  line-height: 1.2;
  letter-spacing: -0.02em;
  text-align: center;
  max-width: 560px;
  color: var(--ink-0);
}
.card-question :deep(p) { margin: 0; }
.card-question :deep(code) {
  font-family: ui-monospace, 'SF Mono', Menlo, monospace;
  font-size: 0.8em;
  padding: 0 6px;
  background: var(--paper-2);
  border-radius: 4px;
  color: var(--ink-0);
}

.card-body-back { padding-top: 16px; gap: 12px; }
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
  max-height: 240px;
  width: 100%;
  height: auto;
}
.card-svg-back :deep(svg) { max-height: 200px; }
</style>
