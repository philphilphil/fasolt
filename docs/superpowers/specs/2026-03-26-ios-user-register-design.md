# iOS User Registration & MCP Setup Instructions

**Issue:** #26 — iOS User-Register
**Date:** 2026-03-26

## Overview

Two features to make the iOS app self-sufficient for new users:

1. **Registration** — native in-app registration so users can sign up without visiting the web app
2. **MCP Setup Instructions** — collapsible setup guide in Settings so users know how to connect their AI agent

## Part 1: Registration

### Landing Screen Changes

The current `OnboardingView` has a single "Sign In" button. Replace it with two buttons:

- **Create Account** — primary style (`.borderedProminent`), blue
- **Sign In** — secondary style (`.bordered`), dark/subtle

The "Self-hosting? Change server" link stays at the bottom. When self-hosting mode is active, the server URL field appears above both buttons (applies to both registration and sign-in).

### Registration Screen

A new `RegisterView` pushed onto a `NavigationStack` when the user taps "Create Account".

**Layout:**
- Navigation back button (automatic via NavigationStack)
- Title: "Create Account"
- Subtitle: "Start learning with spaced repetition"
- Fields: Email, Password, Confirm Password
- Password hint: "Min 8 characters, with uppercase, lowercase, and a number"
- CTA button: "Create Account" (`.borderedProminent`, `.controlSize(.large)`)
- Error message area below the button

**Field configuration:**
- Email: `.textContentType(.emailAddress)`, `.keyboardType(.emailAddress)`, `.autocorrectionDisabled()`, `.textInputAutocapitalization(.never)`
- Password: `.textContentType(.newPassword)`, `SecureField`
- Confirm Password: `.textContentType(.newPassword)`, `SecureField`

**Client-side validation (before API call):**
- All fields non-empty
- Email contains `@` (basic check, server does full validation)
- Password meets minimum requirements (8+ chars, has uppercase, lowercase, digit)
- Passwords match

**Button state:**
- Disabled while fields are invalid or request is in-flight
- Shows `ProgressView` spinner during request

### Registration Flow

1. User fills form, taps "Create Account"
2. `RegisterView` calls `AuthService.register(email:password:serverURL:)`
3. `AuthService.register()` makes unauthenticated POST to `/api/identity/register` with `{ "email": "...", "password": "..." }`
4. On success (200): automatically calls `AuthService.signIn(serverURL:)` to trigger the OAuth/PKCE flow
5. User sees brief Safari sheet for OAuth, lands in the app authenticated
6. On failure: display server error message (e.g., "DuplicateEmail", "PasswordTooShort") mapped to user-friendly strings

### AuthService Changes

Add a new method:

```swift
func register(email: String, password: String, serverURL: String) async
```

This method:
- Sets `isLoading = true`
- POSTs to `{serverURL}/api/identity/register`
- On success: calls `signIn(serverURL:)` to auto-login
- On failure: sets `errorMessage` with a user-friendly error
- Sets `isLoading = false`

Uses `APIClient.unauthenticatedRequest()` since the user has no token yet.

Client-side validation (password strength, matching, non-empty) lives in `RegisterViewModel`. `AuthService.register()` only handles the API call and auto-login — it does not validate.

### Error Mapping

Server error codes from ASP.NET Identity to user-friendly messages:

| Server Code | Display Message |
|---|---|
| `DuplicateEmail` / 409 | "An account with this email already exists" |
| `PasswordTooShort` | "Password must be at least 8 characters" |
| `PasswordRequiresUpper` | "Password must contain an uppercase letter" |
| `PasswordRequiresLower` | "Password must contain a lowercase letter" |
| `PasswordRequiresDigit` | "Password must contain a number" |
| `InvalidEmail` | "Please enter a valid email address" |
| Other/unknown | "Registration failed. Please try again." |

### Navigation Structure

Wrap `OnboardingView` in a `NavigationStack`. The landing screen is the root; "Create Account" pushes `RegisterView`.

```
NavigationStack
├── OnboardingView (root — landing with two buttons)
└── RegisterView (pushed — registration form)
```

## Part 2: MCP Setup Instructions in Settings

### Placement

New section in `SettingsView` between the Account section and Notifications section.

### Structure

**Section: "MCP Setup"**

1. **Explanation row** — static text: "Connect your AI agent to create flashcards from your notes. Copy your MCP URL and add it to your client."

2. **MCP URL row** — displays `https://{serverURL}/mcp` (using server URL from keychain) with a "Copy" button. Tapping copies to clipboard with haptic feedback and brief "Copied!" confirmation.

3. **Client Instructions** — three `DisclosureGroup` items, each collapsible:

   **Claude Code:**
   ```
   Run in your terminal:
   claude mcp add fasolt --transport http {serverURL}/mcp
   ```
   With a Copy button for the command.

   **Claude.ai Web:**
   ```
   1. Go to claude.ai/settings
   2. Click Connectors → Add
   3. Paste your MCP URL
   4. Authorize with your Fasolt account
   ```

   **GitHub Copilot CLI:**
   ```
   Add to ~/.copilot/mcp-config.json:
   {
     "mcpServers": {
       "fasolt": {
         "type": "http",
         "url": "{serverURL}/mcp"
       }
     }
   }
   ```
   With a Copy button for the JSON config.

4. **Auth note** — footer text: "You'll be asked to log in when your AI client first connects."

### Implementation

New `McpSetupSection` view (extracted for clarity) used within `SettingsView`. The server URL is read from `AuthService.serverURL` (already available from keychain).

Copy functionality uses `UIPasteboard.general.string` with a brief visual confirmation (checkmark replacing "Copy" for 2 seconds, similar to the web implementation).

## Backend Changes

None required. The existing `/api/identity/register` endpoint and OAuth flow handle everything.

## Files to Create/Modify

**New files:**
- `Views/Onboarding/RegisterView.swift` — registration form
- `ViewModels/RegisterViewModel.swift` — registration logic and validation
- `Views/Settings/McpSetupSection.swift` — MCP instructions section

**Modified files:**
- `Views/Onboarding/OnboardingView.swift` — wrap in NavigationStack, add two buttons, navigation to RegisterView
- `Services/AuthService.swift` — add `register()` method
- `Views/Settings/SettingsView.swift` — add McpSetupSection

## Testing

- **Registration flow:** navigate to Create Account, fill form, submit, verify auto-login lands in the app
- **Validation:** test empty fields, mismatched passwords, weak passwords — verify inline errors
- **Duplicate email:** register twice with same email, verify friendly error
- **Self-hosting:** toggle self-hosting, verify registration uses custom server URL
- **MCP Setup:** navigate to Settings, verify MCP URL shows correct server, test copy buttons, expand/collapse all client sections
