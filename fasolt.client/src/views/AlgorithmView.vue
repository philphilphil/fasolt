<script setup lang="ts">
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
</script>

<template>
  <div class="mx-auto max-w-3xl px-6 py-12 space-y-8">
    <div>
      <h1 class="text-lg font-semibold tracking-tight">How the Algorithm Works</h1>
      <p class="text-xs text-muted-foreground mt-1">
        fasolt uses FSRS (Free Spaced Repetition Scheduler) to schedule your reviews.
      </p>
    </div>

    <!-- What is Spaced Repetition -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">What is Spaced Repetition?</CardTitle>
      </CardHeader>
      <CardContent class="text-xs text-muted-foreground leading-relaxed space-y-3">
        <p>
          Spaced repetition is a study technique where you review material at increasing intervals.
          Instead of cramming, you see a card right before you're likely to forget it.
          Each successful recall makes the memory stronger, so the next review can wait longer.
        </p>
        <p>
          A new card might be reviewed after 1 day, then 3 days, then 8 days, then 3 weeks —
          growing exponentially as the memory stabilizes.
        </p>
      </CardContent>
    </Card>

    <!-- The FSRS Algorithm -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">The FSRS Algorithm</CardTitle>
      </CardHeader>
      <CardContent class="text-xs text-muted-foreground leading-relaxed space-y-3">
        <p>
          FSRS (Free Spaced Repetition Scheduler) is a modern, open-source algorithm based on the
          <strong class="text-foreground">Three Component Model of Memory</strong>. It tracks three variables for each card:
        </p>
        <dl class="space-y-3 pl-1">
          <div>
            <dt class="font-medium text-foreground">Stability (S)</dt>
            <dd>How long the memory lasts. Higher stability means longer intervals between reviews. This is the core scheduling driver.</dd>
          </div>
          <div>
            <dt class="font-medium text-foreground">Difficulty (D)</dt>
            <dd>How inherently hard the card is for you. Updated with each review based on your rating.</dd>
          </div>
          <div>
            <dt class="font-medium text-foreground">Retrievability (R)</dt>
            <dd>The probability you can recall the card right now. Decays over time — when it drops below the target retention (90%), the card becomes due.</dd>
          </div>
        </dl>
        <p>
          Unlike older algorithms that use the same fixed formula for everyone, FSRS's default parameters were
          optimized on over 700 million reviews from 20,000 users, making it significantly more accurate out of the box.
        </p>
      </CardContent>
    </Card>

    <!-- How Reviews Work -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">How Reviews Work</CardTitle>
      </CardHeader>
      <CardContent class="text-xs text-muted-foreground leading-relaxed space-y-3">
        <p>When you review a card, you rate how well you recalled it. Each rating affects the card differently:</p>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div class="rounded border border-border/60 p-3 space-y-1">
            <div class="font-medium text-foreground">Again</div>
            <div>You forgot. Stability resets and the card re-enters the learning phase for short-interval reviews.</div>
          </div>
          <div class="rounded border border-border/60 p-3 space-y-1">
            <div class="font-medium text-foreground">Hard</div>
            <div>You recalled with significant difficulty. Stability increases slightly, difficulty goes up.</div>
          </div>
          <div class="rounded border border-border/60 p-3 space-y-1">
            <div class="font-medium text-foreground">Good</div>
            <div>Normal recall. Stability increases proportionally, the standard path.</div>
          </div>
          <div class="rounded border border-border/60 p-3 space-y-1">
            <div class="font-medium text-foreground">Easy</div>
            <div>Effortless recall. Large stability increase, difficulty decreases. Longer until next review.</div>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- How Intervals Grow -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">How Intervals Grow</CardTitle>
      </CardHeader>
      <CardContent class="text-xs text-muted-foreground leading-relaxed space-y-3">
        <p>Here's a typical progression for a card rated "Good" each time:</p>
        <div class="flex flex-wrap items-center gap-2 text-foreground font-mono text-[11px]">
          <span class="rounded bg-muted/50 px-2 py-1">1d</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="rounded bg-muted/50 px-2 py-1">3d</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="rounded bg-muted/50 px-2 py-1">8d</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="rounded bg-muted/50 px-2 py-1">21d</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="rounded bg-muted/50 px-2 py-1">55d</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="rounded bg-muted/50 px-2 py-1">4mo</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="text-[10px]">...</span>
        </div>
        <p>
          The intervals grow roughly exponentially. If you rate "Easy", they grow even faster.
          If you rate "Again", the card resets and works its way back up from short intervals.
        </p>
      </CardContent>
    </Card>

    <!-- Why FSRS -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Why FSRS?</CardTitle>
      </CardHeader>
      <CardContent class="text-xs text-muted-foreground leading-relaxed space-y-3">
        <p>
          FSRS replaced the SM-2 algorithm (created in 1987) as the standard for spaced repetition.
          It's now the default in Anki, the most popular flashcard app.
        </p>
        <p>Key advantages:</p>
        <ul class="list-disc list-inside space-y-1 pl-1">
          <li><strong class="text-foreground">20-30% fewer reviews</strong> for the same retention rate</li>
          <li><strong class="text-foreground">Optimized defaults</strong> trained on 700M+ real reviews</li>
          <li><strong class="text-foreground">Better handling of overdue cards</strong> — SM-2 penalized you for reviewing late, FSRS adapts</li>
          <li><strong class="text-foreground">Open source</strong> — transparent, peer-reviewed, continuously improving</li>
        </ul>
      </CardContent>
    </Card>
  </div>
</template>
