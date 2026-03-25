import { test, expect, APIRequestContext } from '@playwright/test';

/**
 * E2E API tests for /api/notifications endpoints.
 * Requires the full stack running (backend at http://localhost:8080).
 * Uses the dev seed user: dev@fasolt.local / Dev1234!
 */

const BASE = 'http://localhost:8080';
const DEV_EMAIL = 'dev@fasolt.local';
const DEV_PASSWORD = 'Dev1234!';

async function loginAndGetCookies(request: APIRequestContext): Promise<string> {
  const res = await request.post(`${BASE}/api/identity/login?useCookies=true`, {
    data: { email: DEV_EMAIL, password: DEV_PASSWORD },
    headers: { 'Content-Type': 'application/json' },
  });
  expect(res.status()).toBe(200);
  const setCookie = res.headers()['set-cookie'];
  expect(setCookie).toBeTruthy();
  return setCookie;
}

test.describe('Notification API', () => {
  let cookies: string;

  test.beforeAll(async ({ request }) => {
    cookies = await loginAndGetCookies(request);
  });

  // ── Device token ─────────────────────────────────────────────────────────

  test('PUT /api/notifications/device-token — stores a token', async ({ request }) => {
    const res = await request.put(`${BASE}/api/notifications/device-token`, {
      data: { token: 'test-apns-token-abc123' },
      headers: { 'Content-Type': 'application/json', Cookie: cookies },
    });
    expect(res.status()).toBe(204);
  });

  test('PUT /api/notifications/device-token — updates existing token', async ({ request }) => {
    // First upsert
    await request.put(`${BASE}/api/notifications/device-token`, {
      data: { token: 'first-token' },
      headers: { 'Content-Type': 'application/json', Cookie: cookies },
    });

    // Second upsert (should update, not create duplicate)
    const res = await request.put(`${BASE}/api/notifications/device-token`, {
      data: { token: 'second-token' },
      headers: { 'Content-Type': 'application/json', Cookie: cookies },
    });
    expect(res.status()).toBe(204);
  });

  test('PUT /api/notifications/device-token — rejects empty token', async ({ request }) => {
    const res = await request.put(`${BASE}/api/notifications/device-token`, {
      data: { token: '' },
      headers: { 'Content-Type': 'application/json', Cookie: cookies },
    });
    expect(res.status()).toBe(400);
  });

  test('PUT /api/notifications/device-token — rejects whitespace token', async ({ request }) => {
    const res = await request.put(`${BASE}/api/notifications/device-token`, {
      data: { token: '   ' },
      headers: { 'Content-Type': 'application/json', Cookie: cookies },
    });
    expect(res.status()).toBe(400);
  });

  test('GET /api/notifications/settings — HasDeviceToken is true after PUT', async ({ request }) => {
    // Ensure a token exists
    await request.put(`${BASE}/api/notifications/device-token`, {
      data: { token: 'settings-check-token' },
      headers: { 'Content-Type': 'application/json', Cookie: cookies },
    });

    const res = await request.get(`${BASE}/api/notifications/settings`, {
      headers: { Cookie: cookies },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.hasDeviceToken).toBe(true);
  });

  test('DELETE /api/notifications/device-token — removes token', async ({ request }) => {
    // Ensure a token exists first
    await request.put(`${BASE}/api/notifications/device-token`, {
      data: { token: 'token-to-delete' },
      headers: { 'Content-Type': 'application/json', Cookie: cookies },
    });

    const del = await request.delete(`${BASE}/api/notifications/device-token`, {
      headers: { Cookie: cookies },
    });
    expect(del.status()).toBe(204);

    // Verify HasDeviceToken is false
    const settings = await request.get(`${BASE}/api/notifications/settings`, {
      headers: { Cookie: cookies },
    });
    const body = await settings.json();
    expect(body.hasDeviceToken).toBe(false);
  });

  test('DELETE /api/notifications/device-token — idempotent when no token exists', async ({ request }) => {
    // Delete any existing token first
    await request.delete(`${BASE}/api/notifications/device-token`, {
      headers: { Cookie: cookies },
    });

    // Delete again — should still be 204
    const res = await request.delete(`${BASE}/api/notifications/device-token`, {
      headers: { Cookie: cookies },
    });
    expect(res.status()).toBe(204);
  });

  // ── Settings ─────────────────────────────────────────────────────────────

  test('GET /api/notifications/settings — returns default interval of 8', async ({ request }) => {
    const res = await request.get(`${BASE}/api/notifications/settings`, {
      headers: { Cookie: cookies },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(typeof body.intervalHours).toBe('number');
    expect(body).toHaveProperty('hasDeviceToken');
  });

  test('PUT /api/notifications/settings — accepts allowed intervals', async ({ request }) => {
    for (const interval of [4, 6, 8, 10, 12, 24]) {
      const res = await request.put(`${BASE}/api/notifications/settings`, {
        data: { intervalHours: interval },
        headers: { 'Content-Type': 'application/json', Cookie: cookies },
      });
      expect(res.status(), `interval ${interval} should be accepted`).toBe(204);
    }
  });

  test('PUT /api/notifications/settings — rejects disallowed intervals', async ({ request }) => {
    for (const interval of [1, 3, 5, 7, 48, 0, -1]) {
      const res = await request.put(`${BASE}/api/notifications/settings`, {
        data: { intervalHours: interval },
        headers: { 'Content-Type': 'application/json', Cookie: cookies },
      });
      expect(res.status(), `interval ${interval} should be rejected`).toBe(400);
    }
  });

  test('PUT /api/notifications/settings — persists interval change', async ({ request }) => {
    const res = await request.put(`${BASE}/api/notifications/settings`, {
      data: { intervalHours: 12 },
      headers: { 'Content-Type': 'application/json', Cookie: cookies },
    });
    expect(res.status()).toBe(204);

    const settings = await request.get(`${BASE}/api/notifications/settings`, {
      headers: { Cookie: cookies },
    });
    const body = await settings.json();
    expect(body.intervalHours).toBe(12);

    // Reset to default
    await request.put(`${BASE}/api/notifications/settings`, {
      data: { intervalHours: 8 },
      headers: { 'Content-Type': 'application/json', Cookie: cookies },
    });
  });

  // ── Auth ─────────────────────────────────────────────────────────────────

  test('All endpoints require authentication', async ({ request }) => {
    const endpoints = [
      () => request.put(`${BASE}/api/notifications/device-token`, { data: { token: 'x' } }),
      () => request.delete(`${BASE}/api/notifications/device-token`),
      () => request.get(`${BASE}/api/notifications/settings`),
      () => request.put(`${BASE}/api/notifications/settings`, { data: { intervalHours: 8 } }),
    ];

    for (const call of endpoints) {
      const res = await call();
      expect(res.status()).toBe(401);
    }
  });
});
