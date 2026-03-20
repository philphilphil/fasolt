# Epic 6: Dashboard & Insights

## US-6.1 — Dashboard Overview (P1)

As a user, I want to see a dashboard when I log in so I know what needs my attention today.

**Acceptance criteria:**

- Cards due today count (prominent, clickable to start studying)
- Total cards in collection
- Cards by status: New, Learning, Mature (see [overview](00-overview.md) for definitions)
- Quick action: "Start studying" button

## US-6.2 — Study Streak (P1)

As a user, I want to see my study streak so I stay motivated to review daily.

**Acceptance criteria:**

- Consecutive days with at least 1 review
- Current streak and longest streak
- Visual streak indicator (calendar heatmap or flame icon)
- Streak resets if a day is missed

## US-6.3 — Review Heatmap (P2)

As a user, I want to see a GitHub-style heatmap of my study activity so I can see patterns over time.

**Acceptance criteria:**

- Calendar grid showing reviews per day (last 6 months)
- Color intensity based on review count
- Hover/tap shows exact count and date
- Shows gaps visually

## US-6.4 — Retention Stats (P2)

As a user, I want to see how well I'm retaining material so I know which areas need more work.

**Acceptance criteria:**

- Overall retention rate (% of cards rated Good or Easy)
- Retention by group
- Cards with lowest ease factor highlighted as "struggling"
- Trend over last 30 days

## US-6.5 — Upcoming Reviews Forecast (P2)

As a user, I want to see how many reviews are coming up in the next week so I can plan my study time.

**Acceptance criteria:**

- Bar chart or list showing due card count per day for next 7 days
- Helps anticipate heavy review days

## US-6.6 — Time Spent Studying (P2)

As a user, I want to see how much time I've spent studying so I can track my investment.

**Acceptance criteria:**

- Total time today, this week, this month
- Average time per card
- Displayed on dashboard
