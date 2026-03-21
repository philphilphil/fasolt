<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

interface TerminalLine {
  text: string
  type: 'prompt' | 'output' | 'success' | 'dim' | 'blank'
  delay: number // ms after previous line finishes before this one starts appearing
  instant?: boolean // show instantly (no typing animation)
}

const lines: TerminalLine[] = [
  { type: 'prompt', text: 'Read my distributed-systems.md and create flashcards', delay: 400 },
  { type: 'blank', text: '', delay: 200, instant: true },
  { type: 'dim', text: 'Reading distributed-systems.md...', delay: 300, instant: true },
  { type: 'dim', text: 'Found 4 sections: CAP Theorem, Consensus, Replication, Partitioning', delay: 600, instant: true },
  { type: 'blank', text: '', delay: 200, instant: true },
  { type: 'dim', text: 'Creating 8 flashcards...', delay: 400, instant: true },
  { type: 'success', text: '✓ What are the three guarantees of the CAP theorem?', delay: 180, instant: true },
  { type: 'success', text: "✓ Why can't a distributed system provide all three CAP properties?", delay: 180, instant: true },
  { type: 'success', text: '✓ What is the difference between strong and eventual consistency?', delay: 180, instant: true },
  { type: 'success', text: '✓ How does the Raft consensus algorithm handle leader election?', delay: 180, instant: true },
  { type: 'success', text: '✓ What is quorum, and why does it matter for replication?', delay: 180, instant: true },
  { type: 'success', text: '✓ What is the difference between synchronous and async replication?', delay: 180, instant: true },
  { type: 'success', text: '✓ What causes a network partition in a distributed system?', delay: 180, instant: true },
  { type: 'success', text: '✓ How does partition tolerance differ from fault tolerance?', delay: 180, instant: true },
  { type: 'blank', text: '', delay: 300, instant: true },
  { type: 'output', text: '8 cards created in "Distributed Systems" deck', delay: 200, instant: true },
]

// State for each line: '' = not started, 'typing' = animating, full text = done
const displayed = ref<string[]>(lines.map(() => ''))
const lineStates = ref<('pending' | 'typing' | 'done')[]>(lines.map(() => 'pending'))

let animationTimeouts: ReturnType<typeof setTimeout>[] = []
let observerRef: IntersectionObserver | null = null
const containerRef = ref<HTMLElement | null>(null)
const started = ref(false)

function typeCharacters(lineIndex: number, text: string, charIndex: number, onDone: () => void) {
  if (charIndex > text.length) {
    lineStates.value[lineIndex] = 'done'
    onDone()
    return
  }
  displayed.value[lineIndex] = text.slice(0, charIndex)
  const charDelay = 28 + Math.random() * 18
  const t = setTimeout(() => {
    typeCharacters(lineIndex, text, charIndex + 1, onDone)
  }, charDelay)
  animationTimeouts.push(t)
}

function runLine(index: number) {
  if (index >= lines.length) return

  const line = lines[index]
  lineStates.value[index] = 'typing'

  const proceed = () => {
    lineStates.value[index] = 'done'
    if (index + 1 < lines.length) {
      const t = setTimeout(() => runLine(index + 1), lines[index + 1].delay)
      animationTimeouts.push(t)
    }
  }

  if (line.instant || line.type === 'blank') {
    displayed.value[index] = line.text
    proceed()
  } else {
    typeCharacters(index, line.text, 0, proceed)
  }
}

function startAnimation() {
  if (started.value) return
  started.value = true
  const t = setTimeout(() => runLine(0), lines[0].delay)
  animationTimeouts.push(t)
}

function resetAnimation() {
  animationTimeouts.forEach(clearTimeout)
  animationTimeouts = []
  displayed.value = lines.map(() => '')
  lineStates.value = lines.map(() => 'pending')
  started.value = false
}

onMounted(() => {
  if (!containerRef.value) return

  observerRef = new IntersectionObserver(
    (entries) => {
      if (entries[0].isIntersecting) {
        startAnimation()
      }
    },
    { threshold: 0.3 },
  )
  observerRef.observe(containerRef.value)
})

onUnmounted(() => {
  animationTimeouts.forEach(clearTimeout)
  observerRef?.disconnect()
})

function handleReplay() {
  resetAnimation()
  // Small delay so reset renders before restarting
  const t = setTimeout(() => startAnimation(), 80)
  animationTimeouts.push(t)
}
</script>

<template>
  <div
    ref="containerRef"
    class="rounded-lg overflow-hidden border border-zinc-700/60 shadow-2xl"
    style="background: #0d1117; font-family: var(--font-mono)"
  >
    <!-- Window chrome -->
    <div class="flex items-center justify-between px-4 py-3 border-b border-zinc-700/60" style="background: #161b22">
      <div class="flex items-center gap-1.5">
        <span class="h-3 w-3 rounded-full" style="background: #ff5f57"></span>
        <span class="h-3 w-3 rounded-full" style="background: #febc2e"></span>
        <span class="h-3 w-3 rounded-full" style="background: #28c840"></span>
      </div>
      <span class="text-xs" style="color: #8b949e">claude — spaced-md MCP</span>
      <button
        class="text-xs transition-colors cursor-pointer"
        style="color: #8b949e"
        title="Replay animation"
        @click="handleReplay"
      >
        ↺ replay
      </button>
    </div>

    <!-- Terminal body -->
    <div class="px-5 py-4 min-h-[280px] text-[13px] leading-relaxed" style="color: #e6edf3">
      <template v-for="(line, i) in lines" :key="i">
        <!-- Blank line spacer -->
        <div v-if="line.type === 'blank' && lineStates[i] !== 'pending'" class="h-3"></div>

        <!-- Prompt line -->
        <div v-else-if="line.type === 'prompt' && lineStates[i] !== 'pending'" class="flex gap-2 mb-1">
          <span style="color: #58a6ff">›</span>
          <span style="color: #e6edf3">{{ displayed[i] }}<span
            v-if="lineStates[i] === 'typing'"
            class="inline-block w-[2px] h-[1em] align-text-bottom ml-px animate-pulse"
            style="background: #e6edf3"
          ></span></span>
        </div>

        <!-- Success line -->
        <div v-else-if="line.type === 'success' && lineStates[i] !== 'pending'" class="flex gap-2">
          <span style="color: #3fb950">{{ displayed[i] }}</span>
        </div>

        <!-- Dim output line -->
        <div v-else-if="line.type === 'dim' && lineStates[i] !== 'pending'" class="flex gap-2">
          <span style="color: #8b949e">{{ displayed[i] }}</span>
        </div>

        <!-- Regular output line -->
        <div v-else-if="line.type === 'output' && lineStates[i] !== 'pending'" class="flex gap-2">
          <span style="color: #e6edf3">{{ displayed[i] }}</span>
        </div>
      </template>

      <!-- Blinking cursor at end when done -->
      <div v-if="lineStates[lineStates.length - 1] === 'done'" class="flex gap-2 mt-1">
        <span style="color: #58a6ff">›</span>
        <span
          class="inline-block w-[2px] h-[1em] align-text-bottom animate-pulse"
          style="background: #e6edf3"
        ></span>
      </div>
    </div>
  </div>
</template>
