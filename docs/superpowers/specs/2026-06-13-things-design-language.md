# fasolt design language — "Quiet & Functional" (Things-anchored)

**Date:** 2026-06-13
**Status:** Approved direction, in implementation
**Anchor reference:** Cultured Code's *Things* — chosen by the user for being "simple and functional."

## Why this exists

The previous look ("paper & ink, vermilion") was not default-shadcn — it was the *other* generic AI aesthetic: Geist font + warm-neutral paper + one tasteful accent + uppercase-mono micro-labels + 1px hairline grids. That whole vocabulary is the current Vercel/Linear "default good taste" that every polished AI-generated app reaches for, so it still reads as machine-made.

The fix is not a new accent color. It is **committing to restraint with a point of view**, anchored on Things: the things that make Things *not* look AI are all subtractive.

## Principles (the rules every screen obeys)

1. **System font, not a designer font.** UI type is the platform system font (SF on Apple). The only exception is the **fasolt wordmark/logo**, which keeps its Geist logotype. No Geist / Geist Mono / Instrument Serif anywhere in the UI chrome. A system monospace is allowed *only* for genuine code, IDs, and keyboard hints.
2. **Whitespace over borders and cards.** Prefer air and faint hairline separators to boxed cards and bordered tile-grids. When something must be a surface, make it quiet.
3. **One accent, used sparingly and meaningfully.** The warm marigold appears only where it carries meaning: the primary action, "today/due" emphasis, active state, and progress. It is not decoration. Per-deck identity dots (blue/green/purple/etc.) are a separate system from the brand accent.
4. **No gloss.** No gradients, no glow/pulse animation, no inset highlight shadows, no hover-lift transforms, no hard offset shadows. Depth comes from a single very-soft shadow at most, and mostly from hairlines + spacing.
5. **Sentence case, quiet labels.** Section labels are calm sentence-case grey text (e.g. "Decks"), never wide-tracked uppercase mono.
6. **Calm motion.** Short, functional transitions (color/opacity ~120ms). Nothing performative.
7. **Native-portable.** Because this is essentially the iOS design language already, the same tokens, type, and component patterns derive to the iOS app later with minimal translation.

## Color tokens

Defined as CSS variables in `fasolt.client/src/style.css`, consumed everywhere through the existing token names so changes propagate app-wide. Values in OKLCH; hex shown for intuition.

### Light
| Token | Value (approx hex) | Role |
|---|---|---|
| `--paper-0` | `#ffffff` | page background |
| `--paper-1` | `#ffffff` | surface/card (defined by hairline, not fill) |
| `--paper-2` | `#f4f4f3` | sunken / hover / input fill / stripe |
| `--rule-1` | `#ececec` | hairline border/separator |
| `--rule-2` | `#f2f2f2` | faintest rule |
| `--ink-0` | `#1c1c1e` | primary text |
| `--ink-1` | `#6b6b70` | secondary text |
| `--ink-2` | `#8e8e93` | muted text / quiet labels |
| `--ink-3` | `#c2c2c7` | tertiary (chevrons, ring tracks) |
| `--accent` | `#ee9b42` | brand marigold — fills, rings, active, "today" |
| `--accent-hi` | `#e08f31` | accent hover/pressed |
| `--accent-text` | `#c9781a` | accent as **text/links on light** (AA-legible) |
| `--accent-soft` | `#fbeede` | accent tint background |
| `--accent-on` | `#ffffff` | text/icon on an accent fill |

### Dark (warm charcoal, flat)
| Token | Value (approx hex) | Role |
|---|---|---|
| `--paper-0` | `#1b1b1c` | page background |
| `--paper-1` | `#242427` | surface/card |
| `--paper-2` | `#2f2f33` | sunken / hover |
| `--rule-1` | `#34343a` | hairline |
| `--rule-2` | `#2c2c30` | faintest rule |
| `--ink-0` | `#ededf0` | primary text |
| `--ink-1` | `#a8a8af` | secondary text |
| `--ink-2` | `#8a8a91` | muted / quiet labels |
| `--ink-3` | `#5c5c63` | tertiary |
| `--accent` | `#f2a352` | brand marigold (lifts slightly on dark) |
| `--accent-hi` | `#f4ad63` | accent hover |
| `--accent-text` | `#f2a352` | accent as text (legible on dark as-is) |
| `--accent-soft` | `#3a2c1a` | accent tint background |
| `--accent-on` | `#ffffff` | text on accent fill |

### Status (review ratings & states)
Aligned to Apple system hues, kept legible in both themes. "Hard" leans amber/yellow so it reads distinct from the brand marigold when the four rating buttons sit together.
- `--c-again` red, `--c-hard` amber-yellow, `--c-good` green, `--c-easy` blue.
- Per-deck identity dots draw from a fixed multi-hue palette (blue `#2e7cf6`/`#0a84ff`, green `#34c759`/`#30d158`, purple `#af52de`/`#bf5af2`, etc.) — unrelated to the brand accent.

## Type

- **Family:** `-apple-system, BlinkMacSystemFont, system-ui, "Segoe UI", Roboto, sans-serif`. Mono: `ui-monospace, "SF Mono", Menlo, monospace` (code/IDs/kbd only).
- **Wordmark:** keeps `Geist` (logotype only; still loaded for that single use).
- **Scale (web):** page title ~32px/600 tight; section heading ~16–20px/600; body 14–15px/400; quiet label 12–13px/600 in `--ink-2`; big "hero number" ~54–64px/600 tabular.
- Tabular lining numerals for all counts/stats.
- Retire the three-voice system (`fa-serif`, `fa-num-serif`, the Instrument Serif "moments"). Numbers are clean tabular system.

## Component patterns

- **Primary button:** flat `--accent` fill, `--accent-on` text, ~9px radius, weight 600, no shadow/gloss. Hover → `--accent-hi`.
- **Secondary/ghost button:** transparent or `--paper-1`, hairline border, `--ink-0` text; hover surface is subtle `--paper-2` — **never** the full brand accent.
- **List row** (decks, cards, sources): a row on the page separated by a faint `--rule-1` hairline (not a boxed card), generous vertical padding (~14–15px), optional leading identity dot or progress ring, name in `--ink-0`, meta in `--ink-2`, due/emphasis in `--accent-text` (light) / `--accent` (dark), trailing `--ink-3` chevron.
- **Progress ring** (Things-style): ~21px, 3px stroke, `--ink-3`/track + `--accent` arc, round caps, starts at 12 o'clock. Shows due/total per deck.
- **"Today" marker:** small filled star in `--accent` + sentence-case label.
- **Quiet section label** (replaces `.fa-cap`): system font, sentence case, 12–13px, weight 600, `--ink-2`, no letter-spacing, no uppercase.
- **Stat display:** prefer an open row of number+label separated by whitespace over a bordered 4-tile grid. If a container is needed, one quiet surface, hairline, no per-tile borders.
- **Shadows:** at most `0 1px 3px rgba(0,0,0,.05)` (light). Dark mode leans on surface lightness, not shadow.

## "AI tells" to remove (per-file migration checklist)

For every view / component / page:
- [ ] No `font-family` literal of `Geist` (except the wordmark), `Geist Mono`, or `Instrument Serif` → system / system-mono.
- [ ] No hardcoded vermilion or old accent hex/oklch → use `--accent` / `--accent-text`.
- [ ] No hardcoded status/green/streak oklch literals → use status tokens.
- [ ] Uppercase wide-tracked mono labels (`.fa-cap` look) → quiet sentence-case labels.
- [ ] Boxed bordered tile-grids → whitespace + hairlines where it reads cleaner.
- [ ] Gloss removed: inset-highlight shadows, glow/pulse, hover-lift transforms, gradients, hard offset shadows.
- [ ] Accent used only for action/active/today/progress — not as decoration or default hover surface.
- [ ] Both light and dark verified to use tokens (no theme-blind hardcoded colors).

## Scope of rollout

Foundation (frozen before fan-out): `index.html` (fonts), `tailwind.config.js` (font families), `src/style.css` (tokens + shared `.fa-*` classes).

Then every screen and shared piece is swept for the checklist above:
- Chrome: `AppLayout`, `TopBar`, `BottomNav`, `AppFooter`.
- App views: Study, Progress, Cards, CardDetail, CardTable, Decks, DeckDetail, DeckSnapshots, RestoreSnapshot, Sources, Review, StudyView's review flow (`ReviewCard`, `RatingButtons`, `SessionComplete`), Settings, Mcp, Algorithm, admin views.
- Marketing/legal: Landing, Privacy, Terms, Impressum, NotFound.
- Dialogs & shared: the `CardCreateDialog`/`*Dialog` family, `ProgressMeter`, `KbdHint`, `SearchResults`, `BulkActionBar`, `ToolSwitcher`, `ThemeToggle`.
- shadcn `ui/` primitives: ensure hover/selected states use subtle surfaces (not brand orange) and inherit the new tokens.
- Auth (server-rendered): `fasolt.Server/Pages/**/*.cshtml` + `src/auth.css`.

## Verification

- `npm run build` (vue-tsc + vite) passes.
- Spot-check key screens (Study, Review, Decks, Cards, Landing) in light **and** dark in a browser; confirm no Geist UI text, no vermilion, no uppercase-mono labels, no gloss.
- iOS derivation is a later, separate effort that reuses these tokens/patterns.
