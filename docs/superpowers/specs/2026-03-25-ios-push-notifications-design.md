# iOS Push Notifications for Due Cards

**Issue:** #23
**Date:** 2026-03-25

## Overview

Send push notifications to iOS users when cards become due for review. A background job on the server checks for due cards on a 1-hour cycle and sends notifications via Apple Push Notification service (APNs). Users choose their preferred notification interval (4–24 hours).

## Data Model

### DeviceToken Entity

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| UserId | string (FK → AppUser) | Unique constraint — single device per user |
| Token | string | APNs device token |
| CreatedAt | DateTimeOffset | |
| UpdatedAt | DateTimeOffset | |

### New Fields on AppUser

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| NotificationIntervalHours | int | 8 | One of: 4, 6, 8, 10, 12, 24 |
| LastNotifiedAt | DateTimeOffset? | null | When the last push was sent |

## API Endpoints

All endpoints require authentication.

### `PUT /api/device-token`

Upserts the device token for the authenticated user. Called by the iOS app after receiving the APNs token from Apple.

- Body: `{ "token": "abc123..." }`
- Returns: 204 No Content

### `DELETE /api/device-token`

Removes the device token for the authenticated user. Called on logout or when notifications are disabled.

- Returns: 204 No Content

### `PUT /api/notifications/settings`

Updates the notification interval preference.

- Body: `{ "intervalHours": 6 }`
- Validates `intervalHours` is one of [4, 6, 8, 10, 12, 24]
- Returns: 204 No Content

### `GET /api/notifications/settings`

Returns the current notification settings.

- Response: `{ "intervalHours": 8, "hasDeviceToken": true }`

## Background Job — NotificationBackgroundService

A `BackgroundService` using `PeriodicTimer` with a 1-hour interval, starting on app startup.

### Each Tick

1. Query all users where:
   - A device token is registered
   - `LastNotifiedAt` is null OR `LastNotifiedAt + NotificationIntervalHours <= now`
2. For each eligible user, query due card count grouped by deck
3. Skip users with 0 due cards
4. Build notification message (e.g., "You have 12 cards due: 5 in Japanese, 7 in History")
   - Cards not in any deck are grouped as "Unsorted" in the deck breakdown
   - Badge count is set to total due card count for the user (not just newly due)
5. Send via ApnsService
6. Update `LastNotifiedAt` to now

### Edge Cases

- APNs returns 410 Gone → delete the device token (user uninstalled or revoked)
- Each user is processed independently — one failure doesn't block others
- Missed ticks (server restart) are acceptable; next tick fires within the hour

## APNs Service

Direct HTTP/2 integration with Apple's APNs API. No third-party library.

### Configuration

| Key | Description |
|-----|-------------|
| `Apns:KeyId` | 10-character key ID from Apple |
| `Apns:TeamId` | Apple Developer Team ID |
| `Apns:BundleId` | App bundle identifier |
| `Apns:KeyPath` | Path to .p8 file |
| `Apns:KeyBase64` | Alternative: base64-encoded key for deployment environments |

### Behavior

- Generates a JWT signed with the .p8 key (ES256), cached for ~50 minutes
- POST to `https://api.push.apple.com/3/device/{token}` with headers:
  - `apns-topic: {BundleId}`
  - `apns-push-type: alert`
- Payload:
  ```json
  {
    "aps": {
      "alert": {
        "title": "Cards due",
        "body": "You have 12 cards due: 5 in Japanese, 7 in History"
      },
      "sound": "default",
      "badge": 12
    }
  }
  ```
- Singleton `HttpClient` configured for HTTP/2
- Returns APNs response status for caller to handle token invalidation

## iOS Changes

### Notification Permission Request

- Triggered after the user completes their first study session (rates their last card)
- Uses `UNUserNotificationCenter.requestAuthorization(options: [.alert, .sound, .badge])`
- Tracked via `@AppStorage("hasRequestedNotificationPermission")` — only asked once

### Device Token Registration

- On permission grant: `UIApplication.shared.registerForRemoteNotifications()`
- Requires `@UIApplicationDelegateAdaptor` in `FasoltApp` for the `application(_:didRegisterForRemoteNotificationsWithDeviceToken:)` callback
- Token converted to hex string and PUT to `/api/device-token`
- Re-registers automatically on token refresh

### Token Cleanup on Logout

- `DELETE /api/device-token` called before clearing local credentials in AuthService

### Settings Screen

- New "Notifications" section in existing SettingsView
- Interval picker: 4h, 6h, 8h, 10h, 12h, 24h
- Shows notification permission status with link to system settings if denied
- Fetches state from `GET /api/notifications/settings` on appear
- Updates via `PUT /api/notifications/settings` on picker change

### Badge Clearing

- Clear badge count on foreground: `UNUserNotificationCenter.current().setBadgeCount(0)` in existing `appDidBecomeActive` handler

## Testing

### Backend Unit Tests

- Users with no device token are skipped
- Users whose interval hasn't elapsed are skipped
- Users with 0 due cards get no notification
- Correct message format with deck breakdown (including "Unsorted")
- Invalid token (410) triggers device token deletion
- `LastNotifiedAt` updated after successful send
- JWT generation produces valid token structure and expiry
- Notification settings endpoint rejects invalid intervals

### iOS Unit Tests

- Notification permission requested after first session completion, not again
- Device token registration and cleanup in AuthService

### E2E (Playwright)

- Notification settings API round-trip: register token, set interval, verify GET returns correct state
- API contract verification (actual push delivery not testable in Playwright)
