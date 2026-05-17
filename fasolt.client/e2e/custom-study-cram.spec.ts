import { test, expect, type Page, type Request } from '@playwright/test'

// Prerequisites:
// - `make dev` (or ./scripts/dev.sh) running (backend on :8080, vite on :5173, postgres in docker)
// - dev seed user dev@fasolt.local / Dev1234! auto-created by the backend
// - server-side GET /api/review/custom?deckId=... endpoint implemented (issue #156)

const SEED_EMAIL = 'dev@fasolt.local'
const SEED_PASSWORD = 'Dev1234!'

async function login(page: Page) {
  await page.goto('/login?returnUrl=%2Fdashboard')
  await page.fill('input[name="Input.Email"]', SEED_EMAIL)
  await page.fill('input[name="Input.Password"]', SEED_PASSWORD)
  await page.click('button[type="submit"]')
  await expect(page).toHaveURL('/dashboard')
}

async function createDeckWithCards(page: Page, deckName: string, cardCount: number): Promise<string> {
  const ctx = page.context()
  // Create deck
  const deckRes = await ctx.request.post('http://localhost:8080/api/decks', {
    data: { name: deckName, description: 'Cram test deck' },
  })
  expect(deckRes.ok()).toBeTruthy()
  const deck = await deckRes.json()
  const deckId: string = deck.id

  // Create cards
  const cards: Array<{ front: string; back: string }> = []
  for (let i = 0; i < cardCount; i++) {
    cards.push({ front: `Cram front ${i}`, back: `Cram back ${i}` })
  }
  const bulkRes = await ctx.request.post('http://localhost:8080/api/cards/bulk', {
    data: { cards, deckId },
  })
  expect(bulkRes.ok()).toBeTruthy()
  return deckId
}

async function deleteDeck(page: Page, deckId: string) {
  await page.context().request.delete(`http://localhost:8080/api/decks/${deckId}?deleteCards=true`)
}

test.describe('custom study (cram mode)', () => {
  test('flip + Next advances the queue without rating, label visible, no /review/rate calls', async ({ page }) => {
    await login(page)
    const deckId = await createDeckWithCards(page, `cram-test-${Date.now()}`, 3)

    // Track network calls to assert no rate calls fire during cram.
    const rateCalls: string[] = []
    const onRequest = (req: Request) => {
      if (req.method() === 'POST' && req.url().includes('/api/review/rate')) {
        rateCalls.push(req.url())
      }
    }
    page.on('request', onRequest)

    try {
      // Navigate to the deck detail and click "Custom study".
      await page.goto(`/decks/${deckId}`)
      const customStudyBtn = page.getByTestId('custom-study-button')
      await expect(customStudyBtn).toBeVisible()

      // Wait for the cram fetch to fire when we click.
      const customFetch = page.waitForResponse(
        (resp) => resp.url().includes('/api/review/custom') && resp.request().method() === 'GET'
      )
      await customStudyBtn.click()
      await customFetch

      // We should now be on the review view in cram mode.
      await expect(page).toHaveURL(/\/review\?deckId=.+&mode=cram/)

      // The small FSRS-not-adjusted label is visible.
      await expect(page.getByText('Custom study — FSRS not adjusted')).toBeVisible()

      // Context chip should say "Custom study", not "Review".
      await expect(page.getByText('Custom study', { exact: true })).toBeVisible()

      // Wait for the first card to be rendered (seeded front text).
      await expect(page.getByText(/Cram front \d/).first()).toBeVisible()

      // Flip the first card with space.
      await page.keyboard.press(' ')

      // The "Next" button should now be visible (cram replaces RatingButtons).
      const nextBtn = page.getByTestId('next-button')
      await expect(nextBtn).toBeVisible()
      await expect(nextBtn).toHaveText(/Next/)

      // Click Next — the queue should advance to the next card (reviewed count goes up).
      await nextBtn.click()

      // The reviewed counter in the context bar should now read "1 reviewed".
      await expect(page.getByText('1 reviewed')).toBeVisible()

      // After advancing, we're back to the unflipped state on the next card —
      // the Next button should be gone until we flip again.
      await expect(nextBtn).not.toBeVisible()

      // Flip and advance the remaining cards via the keyboard (space=flip, n=next).
      // Card 2:
      await page.keyboard.press(' ')
      await expect(nextBtn).toBeVisible()
      await page.keyboard.press('n')
      await expect(page.getByText('2 reviewed')).toBeVisible()

      // Card 3 (final):
      await page.keyboard.press(' ')
      await expect(nextBtn).toBeVisible()
      await page.keyboard.press('n')

      // Session complete screen — should show 3 cards reviewed and a Done button,
      // but NOT the rating breakdown (no Again/Hard/Good/Easy labels).
      await expect(page.getByText('cards reviewed')).toBeVisible()
      await expect(page.getByRole('button', { name: /Done/ })).toBeVisible()
      await expect(page.getByText('Again', { exact: true })).not.toBeVisible()
      await expect(page.getByText('Easy', { exact: true })).not.toBeVisible()

      // Critical invariant: no /api/review/rate calls were made during cram.
      expect(rateCalls).toEqual([])
    } finally {
      page.off('request', onRequest)
      await deleteDeck(page, deckId)
    }
  })

  test('1-4 keyboard shortcuts are no-ops in cram mode', async ({ page }) => {
    await login(page)
    const deckId = await createDeckWithCards(page, `cram-keys-${Date.now()}`, 2)

    const rateCalls: string[] = []
    const onRequest = (req: Request) => {
      if (req.method() === 'POST' && req.url().includes('/api/review/rate')) {
        rateCalls.push(req.url())
      }
    }
    page.on('request', onRequest)

    try {
      await page.goto(`/decks/${deckId}`)
      const customFetch = page.waitForResponse((resp) => resp.url().includes('/api/review/custom'))
      await page.getByTestId('custom-study-button').click()
      await customFetch

      // Wait for the first card to render before sending keystrokes.
      await expect(page.getByText(/Cram front \d/).first()).toBeVisible()

      // Flip then press '3' (would be "good" in normal review). Nothing should happen.
      await page.keyboard.press(' ')
      await expect(page.getByTestId('next-button')).toBeVisible()
      await page.keyboard.press('3')

      // Still on the first card, still flipped, still 0 reviewed.
      await expect(page.getByText('0 reviewed')).toBeVisible()
      await expect(page.getByTestId('next-button')).toBeVisible()
      expect(rateCalls).toEqual([])
    } finally {
      page.off('request', onRequest)
      await deleteDeck(page, deckId)
    }
  })
})
