import { test, expect } from '@playwright/test'

// Prerequisites:
// - ./dev.sh running (backend on :8080, vite on :5173, postgres in docker)
// - dev seed user dev@fasolt.local / Dev1234! auto-created by the backend
//
// These tests rotate no state (they only log in and log out), so they can
// run repeatedly without a fresh DB. The login tests seed no users of
// their own — the dev seed user is enough.

const SEED_EMAIL = 'dev@fasolt.local'
const SEED_PASSWORD = 'Dev1234!'

test.describe('auth: login', () => {
  test('anonymous visit to /study redirects to /oauth/login and back after login', async ({ page }) => {
    await page.goto('/study')

    // Full-page nav to the server-rendered Razor login page.
    await expect(page).toHaveURL(/\/oauth\/login\?returnUrl=%2Fstudy/)
    await expect(page.locator('h1')).toContainText('Sign in to fasolt')

    // Sign in.
    await page.fill('input[name="Input.Email"]', SEED_EMAIL)
    await page.fill('input[name="Input.Password"]', SEED_PASSWORD)
    await page.click('button[type="submit"]')

    // After successful login, the returnUrl takes effect → we land on /study.
    await expect(page).toHaveURL('/study')
    // The SPA has mounted; the server-rendered login heading is gone.
    await expect(page.locator('body')).not.toContainText('Sign in to fasolt')
  })

  test('wrong password renders inline error and stays on login page', async ({ page }) => {
    await page.goto('/oauth/login?returnUrl=%2F')
    await expect(page.locator('h1')).toContainText('Sign in to fasolt')

    await page.fill('input[name="Input.Email"]', SEED_EMAIL)
    await page.fill('input[name="Input.Password"]', 'definitely-wrong-password')
    await page.click('button[type="submit"]')

    // The Razor PageModel returns Page() with ErrorMessage set — no redirect.
    await expect(page).toHaveURL(/\/oauth\/login/)
    await expect(page.locator('.oauth-error')).toContainText('Invalid email or password.')
  })

  test('missing email shows field-level validation error', async ({ page }) => {
    await page.goto('/oauth/login?returnUrl=%2F')

    // HTML5 required attribute would block a plain submit click. Bypass via JS
    // so we can verify the server-side [Required, EmailAddress] validators
    // also surface errors — the browser-side check is a nice-to-have, the
    // server-side check is the security contract.
    await page.evaluate(() => {
      const form = document.querySelector('form[action="/oauth/login"]') as HTMLFormElement
      const email = form.querySelector('input[name="Input.Email"]') as HTMLInputElement
      email.removeAttribute('required')
      email.value = ''
      const password = form.querySelector('input[name="Input.Password"]') as HTMLInputElement
      password.removeAttribute('required')
      password.value = 'Abcdefg1'
      form.submit()
    })

    // Razor's model validation re-renders the page with field-level error.
    await expect(page).toHaveURL(/\/oauth\/login/)
    await expect(page.locator('.oauth-field-error').first()).toBeVisible()
  })

  test('logout from the TopBar dropdown redirects to /oauth/login', async ({ page }) => {
    // Log in first via the server-rendered login page.
    await page.goto('/oauth/login?returnUrl=%2F')
    await page.fill('input[name="Input.Email"]', SEED_EMAIL)
    await page.fill('input[name="Input.Password"]', SEED_PASSWORD)
    await page.click('button[type="submit"]')
    await expect(page).toHaveURL('/')

    // Open the user dropdown. TopBar.vue renders the avatar as a
    // Button inside a DropdownMenuTrigger. The button text is the
    // user's initial (first character of email, so 'D' for dev@fasolt.local).
    // Select it by its aria role — shadcn-vue buttons are role="button".
    // If multiple buttons match, scope to the header region.
    const header = page.locator('header').first()
    const avatarButton = header.getByRole('button').last()
    await avatarButton.click()

    // Click the "Log out" menu item. It appears in a menu that renders
    // when the dropdown opens; getByRole('menuitem') is the reka-ui
    // DropdownMenuItem role.
    await page.getByRole('menuitem', { name: /log out/i }).click()

    // handleLogout calls auth.logout() then window.location.href = '/oauth/login',
    // which is a full-page nav to the server-rendered Razor page.
    await expect(page).toHaveURL(/\/oauth\/login/)
    await expect(page.locator('h1')).toContainText('Sign in to fasolt')
  })

  test('landing page "Log in" CTA navigates to /oauth/login', async ({ page }) => {
    await page.goto('/')
    // LandingView has two "Log in" CTAs (hero + footer). Click the first
    // visible one. They're plain <a href="/oauth/login"> anchors after
    // Task 13 retired the SPA /login route.
    await page.getByRole('link', { name: /^log in$/i }).first().click()
    await expect(page).toHaveURL(/\/oauth\/login/)
    await expect(page.locator('h1')).toContainText('Sign in to fasolt')
  })
})
