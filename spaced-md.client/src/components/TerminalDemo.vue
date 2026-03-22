<script setup lang="ts">
interface TerminalLine {
  text: string
  type: 'prompt' | 'output' | 'success' | 'dim' | 'blank'
}

const lines: TerminalLine[] = [
  { type: 'prompt', text: 'Read my distributed-systems.md and create flashcards' },
  { type: 'blank', text: '' },
  { type: 'dim', text: 'Reading distributed-systems.md...' },
  { type: 'dim', text: 'Found 4 sections: CAP Theorem, Consensus, Replication, Partitioning' },
  { type: 'blank', text: '' },
  { type: 'dim', text: 'Creating 8 flashcards...' },
  { type: 'success', text: '✓ What are the three guarantees of the CAP theorem?' },
  { type: 'success', text: "✓ Why can't a distributed system provide all three CAP properties?" },
  { type: 'success', text: '✓ What is the difference between strong and eventual consistency?' },
  { type: 'success', text: '✓ How does the Raft consensus algorithm handle leader election?' },
  { type: 'success', text: '✓ What is quorum, and why does it matter for replication?' },
  { type: 'success', text: '✓ What is the difference between synchronous and async replication?' },
  { type: 'success', text: '✓ What causes a network partition in a distributed system?' },
  { type: 'success', text: '✓ How does partition tolerance differ from fault tolerance?' },
  { type: 'blank', text: '' },
  { type: 'output', text: '8 cards created in "Distributed Systems" deck' },
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
