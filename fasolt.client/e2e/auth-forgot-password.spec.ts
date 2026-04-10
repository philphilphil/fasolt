import { test, expect, request } from '@playwright/test'

// Prerequisites:
// - ./dev.sh running (backend on :8080, vite on :5173, postgres in docker)
// - TestEmailSink registered (Dev only, see Program.cs)
// - Dev seed user dev@fasolt.local / Dev1234! auto-created by the backend
//
// This test rotates the dev seed user's password. Running it twice in a
// row from a fresh DB will fail because the password is no longer Dev1234!
// after the first run. Either (a) reset the DB between runs
// ('docker compose down -v && ./dev.sh'), or (b) run once, manually reset
// the password via another reset cycle, or (c) accept that this test is
// a one-shot and ignore it on subsequent runs.
//
// In CI (not yet wired) each run would start from a fresh DB so this
// wouldn't be an issue.

const SEED_EMAIL = 'dev@fasolt.local'
const NEW_PASSWORD = 'NewDev1234!'

async function fetchResetCode(email: string): Promise<string> {
  const apiContext = await request.newContext()
  // The test sink endpoint lives on the backend (8080). It's guarded by
  // IsDevelopment() in Program.cs so production builds never see it.
  const response = await apiContext.get(
    `http://localhost:8080/api/test/last-email?email=${encodeURIComponent(email)}`,
  )
  expect(response.ok(), 'test email sink endpoint should be available in dev').toBeTruthy()
  const body = await response.json()
  expect(body.code, 'captured email must contain a code').toBeTruthy()
  return body.code
}

test.describe('auth: forgot password', () => {
  test('full reset flow: request code → enter code → new password → sign in', async ({ page }) => {
    // 1. Start at /login and click "Forgot password?"
    await page.goto('/login?returnUrl=%2F')
    await page.click('a[href*="/oauth/forgot-password"]')
    await expect(page).toHaveURL(/\/oauth\/forgot-password/)
    await expect(page.locator('h1')).toContainText('Reset your password')

    // 2. Enter the dev seed email and submit the form
    await page.fill('input[name="Input.Email"]', SEED_EMAIL)
    await page.click('button[type="submit"]')

    // The PageModel always redirects to ?sent=true regardless of whether
    // the email matches (enumeration guard). We assert on the URL and the
    // "Check your email" heading.
    await expect(page).toHaveURL(/\/oauth\/forgot-password.*sent=true/i)
    await expect(page.locator('h1')).toContainText('Check your email')

    // 3. Click "Enter reset code" → land on /oauth/reset-password
    await page.click('button[type="submit"]')
    await expect(page).toHaveURL(/\/oauth\/reset-password/)
    await expect(page.locator('h1')).toContainText('Enter reset code')

    // 4. Read the code from the test email sink. The backend captured it
    //    when SendPasswordResetCodeAsync fired in the previous step.
    const code = await fetchResetCode(SEED_EMAIL)
    expect(code).toMatch(/^\d{6}$/)

    // 5. Fill the code + new password + confirm, then submit
    await page.fill('input[name="Input.Code"]', code)
    await page.fill('input[name="Input.Password"]', NEW_PASSWORD)
    await page.fill('input[name="Input.ConfirmPassword"]', NEW_PASSWORD)
    await page.click('button[type="submit"]')

    // 6. Success screen
    await expect(page.locator('h1')).toContainText('Password updated')

    // 7. Click "Go to sign in" → /login, then sign in with the new password
    await page.click('button[type="submit"]')
    await expect(page).toHaveURL(/\/oauth\/login/)

    await page.fill('input[name="Input.Email"]', SEED_EMAIL)
    await page.fill('input[name="Input.Password"]', NEW_PASSWORD)
    await page.click('button[type="submit"]')

    // 8. Landed on the SPA home, fully authenticated
    await expect(page).toHaveURL('/')

    // Cleanup note: the dev seed user's password is now NEW_PASSWORD.
    // Subsequent runs of this test will fail at step 5 above (sign in
    // with old password) because OLD_PASSWORD no longer works. Run
    // 'docker compose down -v && ./dev.sh' between runs if repeated
    // execution is needed.
    console.log(
      `[e2e cleanup] dev seed password rotated to ${NEW_PASSWORD}. ` +
        `Reset the DB if you need to re-run this test.`,
    )
  })
})
