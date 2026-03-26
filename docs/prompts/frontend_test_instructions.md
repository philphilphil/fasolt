# Frontend Test Instructions

Test the entire frontend using Playwright MCP. Start the full stack (`./dev.sh`) before testing.

**IMPORTANT: Never skip a test.** Every test in this document must be executed. If a test cannot be run due to missing data (e.g., pagination requires >20 cards), note it as N/A with a reason — do not silently skip it.

**Login credentials:** `dev@fasolt.local` / `Dev1234!`

---

## Prerequisites

```
1. Start full stack: ./dev.sh
2. Wait for backend health: curl http://localhost:8080/api/health
3. Open browser: navigate to http://localhost:5173/login
4. Log in as dev@fasolt.local / Dev1234!
```

---

## 1. Authentication

### 1.1 Login
- Navigate to `/login`
- Fill email and password, click "Log in"
- Verify redirect to `/study`
- Verify TopBar shows user email initial ("D") and email in dropdown

### 1.2 Register
- Navigate to `/register`
- Type a weak password — verify validation rules show (8+ chars, uppercase, lowercase, number)
- Verify submit button is disabled until all rules pass and passwords match
- Do NOT actually submit (would create a user)

### 1.3 Forgot Password
- Navigate to `/forgot-password`
- Verify email input and submit button render

### 1.4 Logout
- Click user initial button in TopBar
- Click "Log out"
- Verify redirect to `/login`
- Log back in for remaining tests

---

## 2. Study / Dashboard

### 2.1 Dashboard Overview
- Navigate to `/study`
- Verify due card count is displayed (large number)
- Verify "Start reviewing" button appears (if cards are due)
- Verify total cards and "studied today" stats
- Verify "Study by deck" section shows deck cards with name, card count, and due badges

### 2.2 Review Session
- Click "Start reviewing" (or navigate to `/review`)
- Verify card displays front text
- Click card (or press Space) to flip — verify back text appears
- Rate card using buttons (Again/Hard/Good/Easy) or keyboard (1-4)
- Verify progress bar advances
- Press Escape to end session early — verify summary screen
- Verify summary shows cards reviewed and rating breakdown

### 2.3 Deck-specific Review
- From dashboard, click a deck with due cards
- Verify review starts with only cards from that deck

---

## 3. Cards View

### 3.1 Card Table
- Navigate to `/cards`
- Verify table columns: Front (with source subtitle), State, Decks, Due, Actions
- Verify Due dates are formatted as `dd.mm.yyyy`
- Verify deck badges appear for cards in decks
- Verify "+" button appears next to deck badges

### 3.2 Filters
- Type in "Filter cards..." — verify cards filter by front text
- Type in "Filter by source..." — verify cards filter by source file
- Change state dropdown — verify only matching state cards show
- Change deck dropdown to a specific deck — verify only cards in that deck show
- Change deck dropdown to "None (no deck)" — verify only deckless cards show
- Uncheck "Active" checkbox — verify cards from inactive decks appear

### 3.3 Create Card
- Click "New card"
- Fill front and back text
- Optionally add source file and heading
- Click Save — verify card appears in table
- Test preview/edit toggle in the dialog
- Test creating multiple cards (add more front inputs)

### 3.4 Edit Card (via table)
- Click "Edit" on a card row
- Verify navigation to `/cards/:id?edit=true`
- Verify card detail page opens in edit mode

### 3.5 Delete Card (via table)
- Click "×" on a card row
- Verify delete dialog appears
- If card has decks: verify dialog shows "removed from: Deck A, Deck B"
- If card has no decks: verify dialog shows "permanently deleted"
- Click Cancel — verify card is NOT deleted
- Click "×" again, then Delete — verify card is removed from table

### 3.6 Add to Deck
- Click "+" on a card's deck badges
- Verify "Add to deck" dialog with deck selector
- Select a deck, click Add
- Verify the new deck badge appears on the card

### 3.7 Pagination
- If >20 cards exist, verify pagination controls appear
- Click Next/Previous — verify table updates

---

## 4. Card Detail View

### 4.1 View Mode
- Click a card's front text link in the table
- Verify breadcrumb: Cards / [card front]
- Verify card front and state badge in header
- Verify metadata: source file, section (on one line), decks (on next line)
- Verify deck names are clickable links to `/decks/:id`
- Verify SRS stats grid: State, Due, Stability, Difficulty, Step, Last Review, Created
- Verify front/back content rendered as markdown
- Verify SVG images render (if card has SVG)

### 4.2 Edit Mode
- Click "Edit" button
- Verify source file and section inputs appear
- Verify deck checkboxes appear (all decks listed)
- Verify front/back textareas with SVG collapsible sections
- Modify front text, click Save — verify change persists
- Test SVG editing: paste SVG markup, verify preview renders
- Test Cancel — verify changes are discarded

### 4.3 Edit via Query Param
- Navigate directly to `/cards/:id?edit=true`
- Verify page opens in edit mode automatically

### 4.4 Reset Progress
- Click "Reset Progress"
- Verify confirmation dialog explains SRS data will be cleared
- Click Cancel — verify nothing changes
- Click Reset — verify success message and card returns to "new" state

### 4.5 Delete Card
- Click "Delete"
- Verify delete dialog shows deck names (if applicable)
- Click Cancel — verify card is NOT deleted
- (Only test actual delete on a disposable card)

---

## 5. Decks View

### 5.1 Deck Grid
- Navigate to `/decks`
- Verify deck cards in grid layout
- Verify each card shows: name, description (if any), card count, due badge
- Verify inactive decks show "Inactive" badge with reduced opacity
- Verify active decks sort before inactive

### 5.2 Create Deck
- Click "New deck"
- Enter name and optional description
- Click Create — verify deck appears in grid

### 5.3 Deck Detail
- Click a deck card
- Verify breadcrumb: Decks / [deck name]
- Verify header with name, description, action buttons
- Verify stat bar: card count, due count, state distribution
- Verify card table shows cards in this deck
- Verify table columns: Front (with source), State, Due, Actions (Edit, Remove, ×)

### 5.4 Deck Actions
- **Study:** Click "Study this deck" (if due cards exist) — verify review starts
- **Deactivate:** Click "Deactivate" — verify inactive banner appears
- **Activate:** Click "Activate" — verify banner disappears
- **Edit:** Click "Edit" — verify name/description dialog, save changes
- **Delete:** Click "Delete" — verify confirmation with "also delete cards" checkbox

### 5.5 Card Actions in Deck
- **Edit:** Click "Edit" on a card row — verify navigates to card detail page in edit mode
- **Remove:** Click "Remove" — verify card is removed from deck (not deleted)
- **Delete (×):** Click "×" — verify delete dialog, confirm deletes the card entirely

---

## 6. Sources View

### 6.1 Source Grid
- Navigate to `/sources`
- Verify source cards show source file name, card count, and due badge
- Click a source card — verify navigates to `/cards?sourceFile=<name>`
- Verify cards view is filtered to that source

---

## 7. Search

### 7.1 Open Search
- Click search input or press ⌘K
- Verify search input focuses

### 7.2 Search Results
- Type at least 2 characters
- Verify dropdown appears, centered below search bar
- Verify **Decks section appears before Cards section**
- Verify dropdown is wide enough to show full card titles (~500px)
- Verify query text is highlighted in results
- Verify decks show card count, cards show state badge

### 7.3 Navigation
- Use ↑/↓ arrow keys — verify active highlight moves
- Press Enter — verify navigates to selected item
- Press Escape — verify dropdown closes and query clears
- Click a result — verify navigates to that card/deck

### 7.4 Edge Cases
- Type 1 character — verify no search triggers
- Type query with no results — verify "No results found"
- Clear search — verify dropdown closes

---

## 8. Navigation & Layout

### 8.1 TopBar
- Verify logo + "fasolt" text on left
- Verify search bar in center (desktop only)
- Verify dark mode toggle icon (sun/moon) next to user menu
- Verify user menu: email initial, dropdown with email, Settings link, Log out

### 8.2 Desktop Nav Tabs
- Verify tabs: Study, Cards, Decks, Sources, MCP, Settings, Admin (if admin)
- Click each tab — verify correct page loads
- Verify active tab has underline/bold styling

### 8.3 Mobile Bottom Nav
- Resize browser to mobile width (<640px)
- Verify bottom navigation bar appears with icons
- Verify desktop tab bar hides
- Verify search bar hides

### 8.4 Dark Mode Toggle
- Click sun/moon icon in TopBar
- Verify theme switches (light ↔ dark)
- Refresh page — verify theme persists
- Click again — verify toggles back

---

## 9. Settings

### 9.1 Page Layout
- Navigate to `/settings`
- Verify only two cards: "Email address" and "Change password"
- Verify NO "Display name" card
- Verify NO "Appearance" card (dark mode is in nav bar now)

### 9.2 Change Email
- Verify current email is pre-filled
- Enter new email and current password
- (Only test submission with a disposable account)

### 9.3 Change Password
- Enter current password, new password, confirm password
- Verify password mismatch shows error
- (Only test submission with a disposable account)

---

## 10. MCP Setup

### 10.1 Page Content
- Navigate to `/mcp-setup` (click MCP tab)
- Verify "How It Works" card with explanation text
- Verify "Add to Your AI Client" card with Claude Code and GitHub Copilot instructions
- Verify copy buttons work (click, verify check icon appears briefly)
- Verify the MCP URL uses the current origin

---

## 11. Admin (Admin Users Only)

### 11.1 User Table
- Navigate to `/admin`
- Verify user table with columns: Email, Cards, Decks, Status, Actions
- Verify pagination controls

### 11.2 Lock/Unlock User
- Click "Lock" on a non-admin user
- Verify confirmation dialog
- Click Cancel — verify no change
- (Only test actual lock on a test user)

---

## 12. Error Handling & Edge Cases

### 12.1 404 Page
- Navigate to `/nonexistent-route`
- Verify 404 page with link back to dashboard

### 12.2 Unauthorized Access
- Log out, navigate to `/cards`
- Verify redirect to `/login`

### 12.3 Auth Redirect
- While logged in, navigate to `/login`
- Verify redirect to `/study`

### 12.4 Invalid Card/Deck ID
- Navigate to `/cards/nonexistent-id`
- Verify redirect to `/cards`
- Navigate to `/decks/nonexistent-id`
- Verify redirect to `/decks`
