<script setup lang="ts">
import { RouterLink } from 'vue-router'
import AppFooter from '@/components/AppFooter.vue'
import FasoltWordmark from '@/components/FasoltWordmark.vue'
import ThemeToggle from '@/components/ThemeToggle.vue'

const intervals = ['1d', '3d', '8d', '21d', '55d', '4mo']
</script>

<template>
  <div class="algo-page">
    <!-- Nav -->
    <nav class="nav">
      <div class="nav-inner">
        <RouterLink to="/" class="flex items-center">
          <FasoltWordmark :size="32" />
        </RouterLink>
        <div class="flex items-center gap-2">
          <ThemeToggle />
          <a href="/login" class="fa-btn fa-btn-ghost">Log in</a>
        </div>
      </div>
    </nav>

    <!-- Content -->
    <main class="main">
      <header class="page-head">
        <h1 class="page-title">How the algorithm works</h1>
        <p class="page-lede">
          fasolt uses FSRS (Free Spaced Repetition Scheduler) to schedule your reviews.
        </p>
      </header>

      <!-- What is Spaced Repetition -->
      <section class="block">
        <h2 class="block-title">What is spaced repetition?</h2>
        <div class="fa-prose">
          <p>
            <a href="https://en.wikipedia.org/wiki/Spaced_repetition" target="_blank" rel="noopener noreferrer">Spaced repetition</a> is a study technique where you review material at increasing intervals.
            Instead of cramming, you see a card right before you're likely to forget it.
            Each successful recall makes the memory stronger, so the next review can wait longer.
          </p>
          <p>
            A new card might be reviewed after 1 day, then 3 days, then 8 days, then 3 weeks —
            growing exponentially as the memory stabilizes. This exploits the
            <a href="https://en.wikipedia.org/wiki/Forgetting_curve" target="_blank" rel="noopener noreferrer">forgetting curve</a>
            discovered by Hermann Ebbinghaus in the 1880s.
          </p>
        </div>
      </section>

      <!-- The FSRS Algorithm -->
      <section class="block">
        <h2 class="block-title">The FSRS algorithm</h2>
        <div class="fa-prose">
          <p>
            <a href="https://github.com/open-spaced-repetition/fsrs4anki" target="_blank" rel="noopener noreferrer">FSRS</a> (Free Spaced Repetition Scheduler) is a modern, open-source algorithm based on the
            <strong>Three Component Model of Memory</strong>. It tracks three variables for each card:
          </p>
        </div>
        <dl class="defs">
          <div class="def">
            <dt>Stability (S)</dt>
            <dd>How long the memory lasts. Higher stability means longer intervals between reviews. This is the core scheduling driver.</dd>
          </div>
          <div class="def">
            <dt>Difficulty (D)</dt>
            <dd>How inherently hard the card is for you. Updated with each review based on your rating.</dd>
          </div>
          <div class="def">
            <dt>Retrievability (R)</dt>
            <dd>The probability you can recall the card right now. Decays over time — when it drops below the target retention (90%), the card becomes due.</dd>
          </div>
        </dl>
        <div class="fa-prose">
          <p>
            Unlike older algorithms that use the same fixed formula for everyone, FSRS's default parameters were
            optimized on over 700 million reviews from 20,000 users, making it significantly more accurate out of the box.
          </p>
        </div>
      </section>

      <!-- How Reviews Work -->
      <section class="block">
        <h2 class="block-title">How reviews work</h2>
        <div class="fa-prose">
          <p>When you review a card, you rate how well you recalled it. Each rating affects the card differently:</p>
        </div>
        <ul class="ratings">
          <li>
            <span class="rating-dot" :style="{ background: 'var(--c-again)' }" />
            <div>
              <div class="rating-name">Again</div>
              <p>You forgot. Stability resets and the card re-enters the learning phase for short-interval reviews.</p>
            </div>
          </li>
          <li>
            <span class="rating-dot" :style="{ background: 'var(--c-hard)' }" />
            <div>
              <div class="rating-name">Hard</div>
              <p>You recalled with significant difficulty. Stability increases slightly, difficulty goes up.</p>
            </div>
          </li>
          <li>
            <span class="rating-dot" :style="{ background: 'var(--c-good)' }" />
            <div>
              <div class="rating-name">Good</div>
              <p>Normal recall. Stability increases proportionally, the standard path.</p>
            </div>
          </li>
          <li>
            <span class="rating-dot" :style="{ background: 'var(--c-easy)' }" />
            <div>
              <div class="rating-name">Easy</div>
              <p>Effortless recall. Large stability increase, difficulty decreases. Longer until next review.</p>
            </div>
          </li>
        </ul>
      </section>

      <!-- How Intervals Grow -->
      <section class="block">
        <h2 class="block-title">How intervals grow</h2>
        <div class="fa-prose">
          <p>Here's a typical progression for a card rated "Good" each time:</p>
        </div>
        <div class="intervals">
          <template v-for="(step, i) in intervals" :key="step">
            <span class="interval-step fa-num">{{ step }}</span>
            <svg class="interval-arrow" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M13 5l7 7-7 7"/></svg>
            <span v-if="i === intervals.length - 1" class="interval-more">…</span>
          </template>
        </div>
        <div class="fa-prose">
          <p>
            The intervals grow roughly exponentially. If you rate "Easy", they grow even faster.
            If you rate "Again", the card resets and works its way back up from short intervals.
          </p>
        </div>
      </section>

      <!-- Why FSRS -->
      <section class="block">
        <h2 class="block-title">Why FSRS?</h2>
        <div class="fa-prose">
          <p>
            FSRS replaced the <a href="https://en.wikipedia.org/wiki/SuperMemo#Description_of_SM-2_algorithm" target="_blank" rel="noopener noreferrer">SM-2 algorithm</a> (created in 1987) as the standard for spaced repetition.
            It's now the default in <a href="https://en.wikipedia.org/wiki/Anki_(software)" target="_blank" rel="noopener noreferrer">Anki</a> (since version 23.10), the most popular flashcard app.
          </p>
          <p>Key advantages:</p>
          <ul>
            <li><strong>20-30% fewer reviews</strong> for the same retention rate</li>
            <li><strong>Optimized defaults</strong> trained on 700M+ real reviews</li>
            <li><strong>Better handling of overdue cards</strong> — SM-2 penalized you for reviewing late, FSRS adapts</li>
            <li><strong>Open source</strong> — transparent, peer-reviewed, continuously improving</li>
          </ul>
        </div>
      </section>

      <!-- Sources -->
      <section class="block">
        <h2 class="block-title">Sources</h2>
        <div class="fa-prose">
          <ul>
            <li><a href="https://en.wikipedia.org/wiki/Spaced_repetition" target="_blank" rel="noopener noreferrer">Spaced repetition</a> — Wikipedia</li>
            <li><a href="https://en.wikipedia.org/wiki/Forgetting_curve" target="_blank" rel="noopener noreferrer">Forgetting curve</a> — Wikipedia</li>
            <li><a href="https://github.com/open-spaced-repetition/fsrs4anki" target="_blank" rel="noopener noreferrer">FSRS4Anki</a> — open-source implementation and documentation</li>
            <li><a href="https://dl.acm.org/doi/10.1145/3534678.3539081" target="_blank" rel="noopener noreferrer">A Stochastic Shortest Path Algorithm for Optimizing Spaced Repetition Scheduling</a> — ACM KDD 2022</li>
            <li><a href="https://doi.org/10.1109/TKDE.2024.3407927" target="_blank" rel="noopener noreferrer">Optimizing Spaced Repetition Schedule by Capturing the Dynamics of Memory</a> — IEEE TKDE 2024</li>
            <li><a href="https://en.wikipedia.org/wiki/SuperMemo#Description_of_SM-2_algorithm" target="_blank" rel="noopener noreferrer">SM-2 algorithm</a> — Wikipedia</li>
          </ul>
        </div>
      </section>
    </main>

    <AppFooter />
  </div>
</template>

<style scoped>
.algo-page {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  background: var(--paper-0);
  color: var(--ink-0);
}

/* Nav — quiet, hairline underline, no glassy blur tint */
.nav {
  position: sticky;
  top: 0;
  z-index: 50;
  background: var(--paper-0);
  border-bottom: 1px solid var(--rule-1);
}
.nav-inner {
  margin: 0 auto;
  max-width: 760px;
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 24px;
}

/* Content column */
.main {
  margin: 0 auto;
  width: 100%;
  max-width: 760px;
  flex: 1;
  padding: 48px 24px 56px;
}

.page-head { margin-bottom: 12px; }
.page-lede {
  margin: 10px 0 0;
  color: var(--ink-2);
  font-size: 15px;
  line-height: 1.5;
}

/* Each section is air + hairline, not a boxed card */
.block {
  padding: 28px 0;
  border-top: 1px solid var(--rule-1);
}
.block-title {
  margin: 0 0 12px;
  font-size: 18px;
  font-weight: 600;
  letter-spacing: -0.01em;
  color: var(--ink-0);
}

/* Definition list (S / D / R) */
.defs {
  display: flex;
  flex-direction: column;
  gap: 16px;
  margin: 4px 0 16px;
}
.def dt {
  font-size: 15px;
  font-weight: 600;
  color: var(--ink-0);
  margin-bottom: 2px;
}
.def dd {
  margin: 0;
  color: var(--ink-1);
  font-size: 15px;
  line-height: 1.6;
}

/* Rating rows — leading status dot, hairline separated, no boxed tiles */
.ratings {
  list-style: none;
  margin: 4px 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
}
.ratings li {
  display: flex;
  gap: 12px;
  padding: 14px 0;
  border-bottom: 1px solid var(--rule-1);
}
.ratings li:last-child { border-bottom: none; }
.rating-dot {
  flex: none;
  width: 9px;
  height: 9px;
  border-radius: 999px;
  margin-top: 6px;
}
.rating-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--ink-0);
  margin-bottom: 1px;
}
.ratings p {
  margin: 0;
  color: var(--ink-1);
  font-size: 15px;
  line-height: 1.55;
}

/* Interval progression */
.intervals {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin: 4px 0 16px;
}
.interval-step {
  display: inline-flex;
  align-items: center;
  height: 26px;
  padding: 0 10px;
  border-radius: 7px;
  background: var(--paper-2);
  color: var(--ink-0);
  font-size: 13px;
}
.interval-arrow { color: var(--ink-3); }
.interval-more { color: var(--ink-2); font-size: 15px; }
</style>
