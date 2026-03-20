# Epic 1: User Accounts

## US-1.1 — Registration (P0)

As a user, I want to create an account with email and password so my cards and progress are saved.

**Acceptance criteria:**

- Email format validation
- Password strength requirements (min 8 chars, mixed case, number)
- Reject duplicate email with clear error
- Auto-login after successful registration
- Redirect to dashboard

## US-1.2 — Login / Logout (P0)

As a user, I want to log in and out so I can access my data securely from any browser.

**Acceptance criteria:**

- Cookie-based session authentication
- "Remember me" option extends session
- Redirect to dashboard after login
- Logout clears session and redirects to landing page
- Show error on invalid credentials without revealing which field is wrong

## US-1.3 — Password Reset (P1)

As a user, I want to reset my password via email so I don't lose access if I forget it.

**Acceptance criteria:**

- Send reset link to registered email
- Link expires after 1 hour
- Confirm new password with re-entry
- Invalidate link after use

## US-1.4 — Profile Settings (P2)

As a user, I want to update my display name, email, and password from a settings page.

**Acceptance criteria:**

- Require current password to change email or password
- Validate new email uniqueness
- Show success confirmation
