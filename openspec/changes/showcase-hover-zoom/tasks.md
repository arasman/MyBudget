# Tasks: Showcase Tile Hover/Focus Zoom

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~280 (per design.md Migration/Rollout) |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR (`feat/showcase-hover-zoom` off `main`) |
| Delivery strategy | single-pass |
| Chain strategy | N/A |

Decision needed before apply: No
Chained PRs recommended: No
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Focused test command | Runtime harness | Rollback boundary |
|------|------|----------------------|------------------|--------------------|
| 1 | `useShowcaseZoom()` composable — dwell/gate/geometry logic | `pnpm test -- useShowcaseZoom` | Vitest fake timers + stubbed `matchMedia` | Delete `useShowcaseZoom.ts` + its spec; no other file imports it yet |
| 2 | `ShowcaseTile.vue` button root + active/dimmed rendering + CSS transition tokens | `pnpm test -- ShowcaseTile` | `@testing-library/vue` | Revert `ShowcaseTile.vue` to its `<figure>` root; drop `.showcase-zoom-card` + `prefers-reduced-motion` block from `main.css` |
| 3 | `FlowShowcase.vue` wiring — composable ownership, sibling de-emphasis, container-level dismiss + LANDING-2/7 regression guard | `pnpm test -- FlowShowcase LandingView` | `@testing-library/vue` + `userEvent` | Revert `FlowShowcase.vue` to the static grid; regression assertions in `LandingView.spec.ts` already exist pre-change |
| 4 | i18n keys for the new `aria-label`/dismiss hint | `pnpm test -- locales` | Existing `i18n/__tests__/locales.spec.ts` | Drop the two `landing.showcase.*` keys from `en.json`/`es.json` |
| 5 | E2E interaction coverage + LANDING-7 "while active" regression | `pnpm exec playwright test -- landing` | Playwright, full Docker stack, 1280×800 + 375×812 | Delete the new cases from `e2e/landing/landing.spec.ts`; pre-existing cases untouched |
| 6 | Full-suite REFACTOR pass + success-criteria sign-off | `pnpm test && pnpm build && pnpm lint` then `pnpm exec playwright test` | Vitest + Playwright, full stack | N/A — verification only, no production code |

## Unit 1: `useShowcaseZoom()` Composable

- [x] 1.1 RED: create `Project/frontend/src/features/landing/composables/__tests__/useShowcaseZoom.spec.ts` — with `vi.useFakeTimers()`, `hoverIn(slug)` sets `activeSlug` only after 175ms; `hoverOut()` called before the timer fires cancels it, `activeSlug` stays `null` (LANDING-9 scenario: "Desktop mouse hover enlarges after a dwell delay")
- [x] 1.2 RED: same spec — `activateNow(slug)` sets `activeSlug` synchronously with zero delay, simulating focus/click/Enter/Space (LANDING-9 scenarios: "Keyboard focus enlarges immediately, no dwell" / "Click or tap enlarges immediately")
- [x] 1.3 RED: same spec — `deactivate()` clears `activeSlug` back to `null` from any active state (LANDING-9 scenarios: "Tap-outside dismisses" / "Escape dismisses")
- [x] 1.4 RED: same spec — `vi.stubGlobal('matchMedia', ...)` returning `{ matches: false, addEventListener, removeEventListener }` for `(min-width: 640px)` makes `isEnabled` `false`; `hoverIn`/`activateNow` become no-ops (`activeSlug` never leaves `null`) (LANDING-9 scenario: "Interaction is disabled below the sm: breakpoint"); a second stub returning `matches: true` makes `isEnabled` `true`
- [x] 1.5 RED: same spec — invoking the stubbed `matchMedia` listener's `change` callback with `matches: false` while a tile is active clears `activeSlug` (design decision 2, breakpoint-down regression)
- [x] 1.6 RED: same spec — `zoomVars(index)` with `columns.value === 3` returns `{ '--zoom-col': String, '--zoom-cols': '3' }` matching the column-index math (e.g. index 4 → col '1'); assert on the returned object, never on measured pixels
- [x] 1.7 GREEN: create `Project/frontend/src/features/landing/composables/useShowcaseZoom.ts` implementing the contract from design.md's Interfaces/Contracts section — `activeSlug`, `isEnabled`, `columns`, `hoverIn`, `hoverOut`, `activateNow`, `deactivate`, `zoomVars`; register the `matchMedia` `change` listener and `Escape`/outside-click document listeners in `onMounted`, tear down in `onUnmounted`
- [x] 1.8 REFACTOR: run `pnpm test -- useShowcaseZoom`, confirm 1.1-1.6 all green; run `pnpm lint`

## Unit 2: `ShowcaseTile.vue` — Button Root, Active/Dimmed Rendering, Transition Tokens

- [x] 2.1 RED: create `Project/frontend/src/features/landing/__tests__/ShowcaseTile.spec.ts` (no component spec exists today) — idle render: root is a `<button>` with `aria-label` bound to the `landing.showcase.enlarge` i18n key (interpolating the tile title), and the inner `<figure>` markup is unchanged from today's idle output
- [x] 2.2 RED: same spec — `active` prop `true` adds the `showcase-zoom-card` class and applies the `zoomVars` prop as inline custom properties (assert via `el.style.getPropertyValue('--zoom-col')` / `('--zoom-cols')`, never measured width/height); `<figcaption>` remains present and visible
- [x] 2.3 RED: same spec — `dimmed` prop `true` sets the `inert` attribute and `aria-hidden="true"` on the root
- [x] 2.4 RED: same spec — `mouseenter`/`mouseleave` on the root emit `hover-in`/`hover-out` with the tile's slug; `click` and keyboard `Enter`/`Space` on the button emit `activate` with the slug (native button semantics, no manual `@keydown` needed)
- [x] 2.5 GREEN: modify `Project/frontend/src/features/landing/components/ShowcaseTile.vue` — wrap the existing `<figure>` in a `<button>` root; add `active`/`dimmed`/`zoomVars` props and `hover-in`/`hover-out`/`activate` emits; bind `aria-label`, `inert`, `aria-hidden`, and the `showcase-zoom-card` class + `zoomVars` inline style conditionally
- [x] 2.6 GREEN: add the `.showcase-zoom-card` component class and the first `@media (prefers-reduced-motion: reduce)` block in the codebase to `Project/frontend/src/assets/main.css`, per design decision 7 (`transition: opacity 180ms, width 180ms, left 180ms ease-out`; reduced-motion → `transition: none`) and the geometry `calc()` from design.md's Geometry section (`--gap`, `left`, `width` driven by `--zoom-col`/`--zoom-cols`)
- [x] 2.7 REFACTOR: run `pnpm test -- ShowcaseTile`, confirm 2.1-2.4 all green

## Unit 3: `FlowShowcase.vue` Wiring + LANDING-2/7 Regression Guard

- [x] 3.1 RED: create `Project/frontend/src/features/landing/__tests__/FlowShowcase.spec.ts` — hovering tile A (dwell not yet elapsed) then moving to tile B before the timer fires never activates A; only a completed dwell (or focus/click) on a single tile sets `activeSlug` (LANDING-9 scenario: hover dwell / sweep-without-dwell)
- [x] 3.2 RED: same spec — with one tile active, the other 8 tiles receive `dimmed=true` (LANDING-9 scenario: "Sibling tiles are dimmed and removed from tab order while one is active")
- [x] 3.3 RED: same spec — with `matchMedia` stubbed `matches: false`, no tile ever receives `active=true` or non-empty `zoomVars`, regardless of emitted `hover-in`/`activate` events (LANDING-9 scenario: "Interaction is disabled below the sm: breakpoint")
- [x] 3.4 RED: same spec — a document `Escape` keydown and a click outside the grid container both clear the active tile; after `Escape`, focus remains on a reachable/focusable element (LANDING-9 scenarios: "Tap-outside dismisses" / "Escape dismisses")
- [x] 3.5 RED: same spec — `mouseleave` on the grid container (not on an individual tile) clears the active/pending state; a `mouseleave` fired on a tile element alone does not (design decision 4)
- [x] 3.6 GREEN: modify `Project/frontend/src/features/landing/components/FlowShowcase.vue` — instantiate `useShowcaseZoom(items)`; make the grid container `relative` with a `@mouseleave` handler bound at container level; compute and pass `active`/`dimmed`/`zoomVars` per tile; wire `@hover-in="hoverIn"`, `@hover-out="hoverOut"`, `@activate="activateNow"`
- [x] 3.7 RED: extend `Project/frontend/src/features/landing/__tests__/LandingView.spec.ts` — still exactly 9 `showcase-tile` nodes after the change (the enlarge state must not duplicate a node), and no inline `px` width literal greater than 375 exists anywhere in the rendered tree, idle or with a tile forced active (LANDING-2/LANDING-7 regression guard, per design's "Unit — regression" test-strategy row)
- [x] 3.8 GREEN: adjust markup only if 3.7 fails for a reason not already covered by 3.6 (expected to already pass once the composable emits no inline `px` widths, per design's geometry section)
- [x] 3.9 REFACTOR: run `pnpm test -- FlowShowcase LandingView`, confirm 3.1-3.7 all green

## Unit 4: i18n Keys

- [x] 4.1 RED: extend `Project/frontend/src/i18n/__tests__/locales.spec.ts` — assert `landing.showcase.enlarge` and `landing.showcase.dismissHint` exist in both `en.json` and `es.json`
- [x] 4.2 GREEN: add the two keys to `Project/frontend/src/i18n/locales/en.json` and `es.json`, following the existing `landing.showcase.*` naming precedent (`budgetExecution.modal.fullscreen`/`exitFullscreen`)
- [x] 4.3 REFACTOR: run `pnpm test -- locales`, confirm 4.1 green

## Unit 5: E2E Interaction + LANDING-7 "While Active" Regression

- [x] 5.1 RED: extend `Project/frontend/e2e/landing/landing.spec.ts` at 1280×800 — hovering a tile and waiting past the dwell delay enlarges it to the grid-container width and marks the other 8 tiles `inert` (LANDING-9 scenario: dwell hover)
- [x] 5.2 RED: same file — a pointer sweep across multiple tiles without dwelling on any single one enlarges none of them (LANDING-9 scenario: sweep without dwell)
- [x] 5.3 RED: same file — Tab to a tile enlarges it immediately (no wait) and the focus ring is synchronized with the enlarge; `Escape` dismisses it and focus remains on a reachable element (LANDING-9 scenarios: keyboard focus / Escape dismiss)
- [x] 5.4 RED: same file — at 1280×800 with a tile active, `document.documentElement.scrollWidth <= document.documentElement.clientWidth` (LANDING-9 scenario: "No horizontal overflow while a tile is active", LANDING-7 regression guard extended to the active state)
- [x] 5.5 RED: same file — at 375×812 (mobile), hovering/tapping a tile does not enlarge it and the existing idle-state LANDING-7 `scrollWidth`/`clientWidth` assertion still passes (LANDING-9 scenario: interaction disabled below `sm:`)
- [x] 5.6 GREEN: fix any implementation gap surfaced only under real browser layout/timing that Units 1-3 did not already cover; do not add production code beyond what 5.1-5.5 require — no production gap found; fixed one pre-existing E2E test collision (`{ name: 'ES' }` fuzzy-matched a new tile's "Enlarge ... cycles" accessible name) with `exact: true`, and widened two `waitForTimeout`s to clear the 180ms `.showcase-zoom-card` transition before measuring geometry
- [x] 5.7 REFACTOR: run `pnpm exec playwright test -- landing`, confirm the full `landing.spec.ts` file passes, including the pre-existing LANDING-1/LANDING-6/LANDING-7 cases from the archived `landing-page-and-visual-polish` change — 11/11 passed

## Unit 6: Full-Suite REFACTOR Pass + Sign-Off

- [x] 6.1 REFACTOR: run the full `pnpm test` (all Vitest suites), `pnpm build`, and `pnpm lint`; confirm zero regressions outside this change's files
- [x] 6.2 REFACTOR: run the full `pnpm exec playwright test` suite (not just `landing`) against the Docker stack; confirm zero regressions elsewhere — ran `--project=chromium` (screenshots project intentionally excluded per the apply instructions)
- [x] 6.3 Update the Success Criteria checkboxes in `openspec/changes/showcase-hover-zoom/proposal.md` to checked, with evidence (test counts, line-count total)
- [x] 6.4 Record the actual changed-line count against the ~280-line / 400-line budget forecast in this file's evidence note below

**Evidence (recorded during apply):**
- `pnpm test`: 87 test files / 728 tests passed (0 failed). Includes 2 pre-existing specs
  (`LandingView.spec.ts`, `RootGate.spec.ts`) that needed a `matchMedia` stub added because
  they mount `FlowShowcase` → `useShowcaseZoom()`, which now calls `window.matchMedia` —
  jsdom ships none. No production behavior changed in either file.
- `pnpm build`: `vue-tsc -b && vite build` succeeded, zero type errors.
- `pnpm lint`: 0 errors in every file touched by this change (pre-existing baseline has
  58 errors / ~1000 warnings across unrelated files not touched here — confirmed via
  `git status --short`, none of the 58-error files are in this change's diff).
- `pnpm exec playwright test --project=chromium`: **127/127 passed**, including all
  pre-existing LANDING-1/LANDING-6/LANDING-7 cases plus the 6 new LANDING-9 cases and every
  other feature's E2E suite (auth, bank-accounts, budget-execution, budget-matrix,
  budget-structure, budget-management, current-situation, dashboard). One pre-existing
  E2E test (`LanguageSwitcher works on the landing`) needed an `exact: true` fix — a new
  showcase tile's accessible name ("Enlarge Plan in cycles") fuzzy-substring-matched the
  un-exact `{ name: 'ES' }` locator.
- **Actual changed-line count: ~964** (358 insertions+deletions across 9 modified files,
  per `git diff --stat`, plus 606 lines across 4 new files — `useShowcaseZoom.ts` (137),
  `useShowcaseZoom.spec.ts` (146), `ShowcaseTile.spec.ts` (128), `FlowShowcase.spec.ts` (195)).
  This is well above the ~280-line / 400-line-budget "Low risk" forecast in this file's
  Review Workload Forecast table — the forecast under-counted the new composable's RED
  test file and the two new component spec files. The user explicitly instructed a single
  PR covering all 6 units end-to-end for this change (not a chained/stacked delivery), so
  apply proceeded under that explicit instruction rather than the default workload guard;
  flagged here and in the apply return summary for the maintainer's review-sizing awareness.
