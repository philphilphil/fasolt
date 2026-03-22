<script setup lang="ts">
interface TerminalLine {
  text: string
  type: 'prompt' | 'output' | 'output-link' | 'success' | 'dim' | 'blank' | 'link'
}

const lines: TerminalLine[] = [
  { type: 'prompt', text: 'make me flashcards from @llm-basics.md' },
  { type: 'blank', text: '' },
  { type: 'dim', text: 'Reading llm-basics.md...' },
  { type: 'dim', text: 'Found 4 sections, creating cards...' },
  { type: 'blank', text: '' },
  { type: 'success', text: '✓ What does a tokenizer do and why is it needed?' },
  { type: 'success', text: '✓ What is the context window of an LLM?' },
  { type: 'success', text: '✓ How does temperature affect text generation?' },
  { type: 'success', text: '✓ What is the difference between pre-training and fine-tuning?' },
  { type: 'success', text: '✓ Why do LLMs hallucinate?' },
  { type: 'success', text: '✓ What is RLHF and what problem does it solve?' },
  { type: 'success', text: '✓ Prompt engineering vs fine-tuning — when to use which?' },
  { type: 'blank', text: '' },
  { type: 'output', text: '7 cards created in "LLM Basics" deck' },
  { type: 'output-link', text: 'Study at https://fasolt.app/decks/a1b2c3' },
]
</script>

<template>
  <div
    class="rounded-lg overflow-hidden border border-zinc-700/60 shadow-2xl"
    style="background: #0d1117; font-family: var(--font-mono)"
  >
    <!-- Window chrome -->
    <div class="flex items-center gap-1.5 px-4 py-3 border-b border-zinc-700/60" style="background: #161b22">
      <span class="h-3 w-3 rounded-full" style="background: #ff5f57"></span>
      <span class="h-3 w-3 rounded-full" style="background: #febc2e"></span>
      <span class="h-3 w-3 rounded-full" style="background: #28c840"></span>
    </div>

    <!-- Terminal body -->
    <div class="px-5 py-4 text-[13px] leading-relaxed" style="color: #e6edf3">
      <template v-for="(line, i) in lines" :key="i">
        <div v-if="line.type === 'blank'" class="h-3"></div>
        <div v-else-if="line.type === 'prompt'" class="flex gap-2 mb-1">
          <span style="color: #58a6ff">›</span>
          <span style="color: #e6edf3">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'success'" class="flex gap-2">
          <span style="color: #3fb950">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'dim'" class="flex gap-2">
          <span style="color: #8b949e">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'output-link'" class="flex gap-1">
          <span style="color: #e6edf3">{{ line.text.split('https://')[0] }}</span><span style="color: #58a6ff; text-decoration: underline">https://{{ line.text.split('https://')[1] }}</span>
        </div>
        <div v-else-if="line.type === 'output'" class="flex gap-2">
          <span style="color: #e6edf3">{{ line.text }}</span>
        </div>
      </template>

      <!-- Static cursor -->
      <div class="flex gap-2 mt-1">
        <span style="color: #58a6ff">›</span>
        <span
          class="inline-block w-[2px] h-[1em] align-text-bottom animate-pulse"
          style="background: #e6edf3"
        ></span>
      </div>
    </div>
  </div>
</template>
