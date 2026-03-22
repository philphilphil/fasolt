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
    class="overflow-hidden rounded border border-border/60 shadow-2xl glow-accent-lg"
    style="background: #0a0e17"
  >
    <!-- Window chrome -->
    <div class="flex items-center gap-2 px-4 py-3 border-b border-white/5" style="background: #0f1420">
      <span class="h-2.5 w-2.5 rounded-full bg-[#ff5f57]/80"></span>
      <span class="h-2.5 w-2.5 rounded-full bg-[#febc2e]/80"></span>
      <span class="h-2.5 w-2.5 rounded-full bg-[#28c840]/80"></span>
      <span class="ml-3 text-[11px] text-white/20">terminal</span>
    </div>

    <!-- Terminal body -->
    <div class="px-5 py-4 text-[12.5px] leading-relaxed" style="color: #d1d5db">
      <template v-for="(line, i) in lines" :key="i">
        <div v-if="line.type === 'blank'" class="h-3"></div>
        <div v-else-if="line.type === 'prompt'" class="flex gap-2 mb-1">
          <span class="text-[hsl(188,86%,53%)]">›</span>
          <span class="text-white/90">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'success'" class="flex gap-2">
          <span class="text-emerald-400/90">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'dim'" class="flex gap-2">
          <span class="text-white/30">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'output-link'" class="flex gap-1">
          <span class="text-white/60">{{ line.text.split('https://')[0] }}</span><span class="text-[hsl(188,86%,53%)] underline decoration-[hsl(188,86%,53%)]/30 underline-offset-2">https://{{ line.text.split('https://')[1] }}</span>
        </div>
        <div v-else-if="line.type === 'output'" class="flex gap-2">
          <span class="text-white/60">{{ line.text }}</span>
        </div>
      </template>

      <!-- Cursor -->
      <div class="flex gap-2 mt-1">
        <span class="text-[hsl(188,86%,53%)]">›</span>
        <span
          class="inline-block w-[7px] h-[14px] align-text-bottom animate-glow-pulse rounded-sm"
          style="background: hsl(188, 86%, 53%)"
        ></span>
      </div>
    </div>
  </div>
</template>
