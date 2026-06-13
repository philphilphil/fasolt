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
  <!-- Terminal mock keeps a dark + mono treatment by design, but de-glossed:
       no glow, no gradient, at most a whisper-soft shadow, system monospace. -->
  <div
    class="fa-mono overflow-hidden rounded-xl border"
    style="background: #16181d; border-color: #2a2d35; box-shadow: var(--sh-2)"
  >
    <!-- Window chrome -->
    <div class="flex items-center gap-2 px-4 py-3 border-b" style="background: #1c1f26; border-color: #2a2d35">
      <span class="h-2.5 w-2.5 rounded-full" style="background: var(--c-again)"></span>
      <span class="h-2.5 w-2.5 rounded-full" style="background: var(--c-hard)"></span>
      <span class="h-2.5 w-2.5 rounded-full" style="background: var(--c-good)"></span>
      <span class="ml-3 text-[11px]" style="color: #6b7280">terminal</span>
    </div>

    <!-- Terminal body -->
    <div class="px-5 py-4 text-[12.5px] leading-relaxed" style="color: #c5c8cf">
      <template v-for="(line, i) in lines" :key="i">
        <div v-if="line.type === 'blank'" class="h-3"></div>
        <div v-else-if="line.type === 'prompt'" class="flex gap-2 mb-1">
          <span style="color: var(--accent)">›</span>
          <span style="color: #e8eaee">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'success'" class="flex gap-2">
          <span style="color: var(--c-good)">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'dim'" class="flex gap-2">
          <span style="color: #6b7280">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'output-link'" class="flex gap-1">
          <span style="color: #9ca3af">{{ line.text.split('https://')[0] }}</span><span class="underline underline-offset-2" style="color: var(--accent)">https://{{ line.text.split('https://')[1] }}</span>
        </div>
        <div v-else-if="line.type === 'output'" class="flex gap-2">
          <span style="color: #9ca3af">{{ line.text }}</span>
        </div>
      </template>

      <!-- Cursor — static block, no glow/pulse -->
      <div class="flex gap-2 mt-1">
        <span style="color: var(--accent)">›</span>
        <span
          class="inline-block w-[7px] h-[14px] align-text-bottom rounded-sm"
          style="background: var(--accent)"
        ></span>
      </div>
    </div>
  </div>
</template>
