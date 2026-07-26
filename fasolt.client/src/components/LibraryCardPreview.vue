<script setup lang="ts">
import type { LibrarySampleCard } from '@/types'
import { useMarkdown } from '@/composables/useMarkdown'
import { sanitizeSvg } from '@/composables/useSvgSanitizer'

defineProps<{ card: LibrarySampleCard }>()

const { render } = useMarkdown()
</script>

<template>
  <article class="preview-card">
    <div class="card-stitch" />

    <header class="card-head">
      <span class="fa-cap card-label">Question</span>
    </header>

    <div class="card-body card-body-front">
      <div v-if="card.frontSvg" class="card-svg" v-html="sanitizeSvg(card.frontSvg)" />
      <div class="card-question" v-html="render(card.front)" />
    </div>

    <div class="card-divider">
      <span class="fa-cap card-label">Answer</span>
    </div>

    <div class="card-body card-body-back">
      <div v-if="card.backSvg" class="card-svg card-svg-back" v-html="sanitizeSvg(card.backSvg)" />
      <div class="card-answer fa-prose" v-html="render(card.back)" />
    </div>
  </article>
</template>

<style scoped>
/* Mirrors ReviewCard's surface, minus the owner-only affordances
   (card id, edit, source file) that a public preview must not show. */
.preview-card {
  position: relative;
  display: flex;
  flex-direction: column;
  width: 100%;
  min-height: 280px;
  padding: 22px 26px;
  border: 1px solid var(--rule-1);
  border-radius: 14px;
  background: var(--paper-1);
  box-shadow: var(--sh-2);
  text-align: left;
  color: inherit;
  overflow: hidden;
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
}

.card-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 12px;
}
.card-label { color: var(--ink-2); }

.card-body {
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

.card-divider {
  border-top: 1px solid var(--rule-1);
  padding-top: 12px;
}

.card-body-back { flex: 1; padding-top: 12px; gap: 12px; }

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
