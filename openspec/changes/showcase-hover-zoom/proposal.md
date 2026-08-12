# Proposal: Showcase Tile Hover/Focus Zoom

## Intent

The landing page exists to show a visitor what MyBudget looks like before signup, but the LANDING-2 showcase grid renders each of the 9 curated flow screenshots at ~350–380px CSS width in a 3-column grid. At that size a visitor cannot read the UI — amounts, tables, charts, labels are illegible — so the section signals "there are screens" without proving what the product does. Exploration confirmed the assets are already at their resolution ceiling (source PNGs are 1280px native, `withoutEnlargement: true` in `scripts/build-showcase.mjs`): this is a **display-size problem, not an asset-quality problem**. Enlarging a tile in place lets a visitor actually inspect a flow without leaving the page or generating a single new asset.

## Scope

### In Scope

- Enlarge one showcase tile on interaction, magnifying the **same `aspect-[16/10]` crop** (user-confirmed: no crop relaxation, no aspect change).
- ONE unified state model — an `activeSlug` ref lifted into `FlowShowcase.vue` — driven by hover, focus, and click/Enter/Space. Not two parallel pointer/touch code paths.
- **In-grid overlay**, not a modal: the visitor stays on the page. The DaisyUI `<dialog>` click-to-expand pattern (`ExecutionListModal.vue`, `DeleteCutModal.vue`) is explicitly rejected here.
- Accessibility: tile becomes a real `<button>` (free Enter/Space semantics over manual `tabindex`+`@keydown`); `:focus-visible`/`:focus-within` triggers the same enlarge state as hover (WCAG 1.4.13); Escape or blur dismisses without pointer movement.
- Sibling de-emphasis via JS-toggled `aria-hidden`/`inert` (not opacity alone — avoids focusable-but-invisible tiles).
- New requirement `LANDING-9` in `openspec/specs/landing-page/spec.md` (LANDING-8 is the last used ID).
- New component specs `ShowcaseTile.spec.ts` / `FlowShowcase.spec.ts`, plus one E2E interaction test in `e2e/landing/landing.spec.ts`.
- i18n keys for any new label/`aria-label`, in `en.json` and `es.json`.

### Out of Scope

- Regenerating or re-shooting screenshots; any change to `scripts/build-showcase.mjs` or the webp widths.
- Relaxing the `object-cover object-top` crop to reveal more of the screenshot (decided against).
- A lightbox/modal/carousel, multi-tile compare, or tile-to-tile navigation.
- Changing which 9 flows are curated (`config/showcase.ts`) or their copy.
- Any hover/zoom behavior outside the landing showcase.
- Multi-PR chaining — this is one small single-PR change.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `landing-page`: adds `LANDING-9 — Showcase Tile Enlarge On Interaction` (hover/focus enlarge, click/tap enlarge, sibling de-emphasis, reduced-motion, dismissal). `LANDING-2` (fixed 9 tiles) and `LANDING-7` (no mobile overflow) are unchanged and act as regression guards.

## Approach

Carried from exploration `sdd/showcase-hover-zoom/explore` (Approach 3, hybrid, with the modal rejected):

| Decision | Choice | Why |
|---|---|---|
| What "zoom" means | Magnify the same 16:10 crop | User-confirmed; no reflow/overflow risk from an aspect change |
| Surface | In-grid overlay | A modal takes the visitor out of the page, contradicting the intent |
| State | One `activeSlug` in `FlowShowcase.vue` | Hover, focus, and click converge on one testable state |
| Trigger element | `<button>` wrapper | Native focus + Enter/Space; no manual keydown wiring |
| Siblings | JS-toggled `aria-hidden`/`inert` | Opacity-only leaves invisible tiles focusable |
| Sizing | Tailwind classes / viewport units only | Inline px widths break the LANDING-7 guard |

Frontend-only, additive. No backend, router, store, or asset-pipeline work.

## New Conventions Introduced

No precedent exists in this codebase for either — both are new patterns, not reuse:

1. `@media (prefers-reduced-motion: reduce)` handling for the enlarge transition (`main.css` has zero transition/reduced-motion tokens today).
2. `aria-hidden`/`inert`-driven sibling de-emphasis.

Both should be documented in `design.md` so future contributors know they are deliberate.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `frontend/src/features/landing/components/ShowcaseTile.vue` | Modified | `<figure>` → focusable `<button>`; active/enlarged rendering, ARIA |
| `frontend/src/features/landing/components/FlowShowcase.vue` | Modified | Owns `activeSlug`; overlay container; sibling `aria-hidden`/`inert` |
| `frontend/src/assets/main.css` | Modified | Transition tokens + `prefers-reduced-motion` block |
| `frontend/src/i18n/locales/{en,es}.json` | Modified | New showcase zoom/close label keys (EN + ES) |
| `frontend/src/features/landing/__tests__/` | New | `ShowcaseTile.spec.ts`, `FlowShowcase.spec.ts` |
| `frontend/e2e/landing/landing.spec.ts` | Modified | One interaction + no-overflow-while-active test |
| `openspec/specs/landing-page/spec.md` | Modified | `LANDING-9` delta |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Enlarged tile causes horizontal overflow at 375px, breaking LANDING-7 | Med | No inline px widths; Tailwind/viewport sizing only; existing `LandingView.spec.ts` inline-width guard + 375×812 E2E `scrollWidth <= clientWidth` check both kept green, plus a new "while active" overflow assertion |
| Hover-only enlarge is unreachable by keyboard (WCAG 1.4.13 failure) | Med | Focus triggers the same state as hover; Escape/blur dismisses; asserted at component-spec level |
| Sibling dimming creates a focusable-but-invisible tab trap | Med | `aria-hidden`/`inert` toggled with the active state, asserted in `FlowShowcase.spec.ts` |
| Hover-triggered enlarge feels twitchy when scanning across the grid | Med | Design pins an intent delay/transition timing; reduced-motion path skips the animation entirely |
| Overlay z-index/stacking conflicts with `PublicBackdrop` or `AppFooter` | Low | Overlay scoped inside the `max-w-6xl` grid container, not portaled to `<body>` |
| Enlarging the existing 1280w asset still looks soft on a hi-DPI display | Low | 1280px is the source ceiling; enlarged display width stays under it. Accepted — no asset work in scope |

## Rollback Plan

Additive, frontend-only, no data or migration. Revert `feat/showcase-hover-zoom`: restore `ShowcaseTile.vue` to its `<figure>` form, drop the `activeSlug` state from `FlowShowcase.vue`, remove the new `main.css` transition/reduced-motion block and the new i18n keys, delete the two new component specs and the new E2E case, and drop `LANDING-9` from `openspec/specs/landing-page/spec.md`. The grid returns to today's static behavior; `LANDING-2`/`LANDING-7` coverage is untouched by the revert.

## Dependencies

- Branch `feat/showcase-hover-zoom` off `main` (branch-before-cycle convention); `main` already carries the merged/archived `landing-page-and-visual-polish`.
- Existing `public/showcase/{slug}-{640,1280}.webp` assets — reused as-is, no regeneration.

## Security & i18n Notes

- **Security**: no change to the auth surface. The showcase stays anonymous-safe — no authenticated API call, no user or budget data. `LANDING-1` is unaffected.
- **i18n**: any new label or `aria-label` MUST exist in both `en.json` and `es.json`, following the existing `landing.showcase.*` naming (precedent: `budgetExecution.modal.fullscreen`/`exitFullscreen`). Covered by `i18n/__tests__/locales.spec.ts`.

## Success Criteria

- [x] A mouse user hovering a showcase tile sees it enlarge in place and can read the screenshot's UI detail. (E2E: "hovering a tile past the dwell delay enlarges it to the grid-container width")
- [x] A keyboard user reaches the same enlarged state via Tab/focus and dismisses it with Escape, no pointer required. (E2E: "Tab enlarges a tile immediately ... Escape dismisses it and focus stays reachable")
- [x] A touch user taps a tile, gets the same enlarged state, and taps away (tap-outside) to dismiss. (Unit tests: `FlowShowcase.spec.ts` click-activates + click-outside-dismisses; touch itself is a click-equivalent per design's click/Enter/Space/focus trigger set — no separate close affordance, per resolved Open Question 2)
- [x] While a tile is active, non-active tiles are visually de-emphasized AND removed from the tab/reading order. (`inert` + `aria-hidden` + `sm:opacity-40`, asserted in `ShowcaseTile.spec.ts`/`FlowShowcase.spec.ts`/E2E)
- [x] With `prefers-reduced-motion: reduce`, the enlarged state still appears but without the scale/opacity animation. (`main.css` `@media (prefers-reduced-motion: reduce) { .showcase-zoom-card { transition: none } }`)
- [x] No horizontal overflow at 375px in either the idle or the active state; the existing LANDING-7 unit and E2E guards stay green. (`LandingView.spec.ts` regression test + E2E "no horizontal overflow while a tile is active" + "below the sm: breakpoint ... LANDING-7 still holds")
- [x] New zoom/close copy renders in EN and ES. (`landing.showcase.enlarge`/`dismissHint` in both locales, asserted in `locales.spec.ts`)
- [ ] Total diff fits comfortably in one PR under the 400-line review budget. **NOT MET** — actual diff is ~964 changed lines (see tasks.md Unit 6 evidence), well over budget. The maintainer explicitly directed a single PR covering all 6 work units for this change rather than a chained/stacked delivery; flagged here for review-sizing awareness rather than silently passing.

**Test evidence**: `pnpm test` 87 files / 728 tests passed. `pnpm build` clean (0 type errors). `pnpm lint` 0 errors in all files touched by this change. `pnpm exec playwright test --project=chromium` 127/127 passed (full suite, screenshots project excluded).

## Open Questions (resolved — sign-off complete)

1. **Enlarge magnitude** — RESOLVED: full container width (~1152px, close to the 1280px native asset ceiling). Chosen for maximum legibility gain — this is the whole point of the feature.
2. **Dismiss affordance on touch** — RESOLVED: tap-outside only, no visible close button. No new EN/ES copy or icon needed for dismissal.
3. **Hover intent delay** — RESOLVED: short ~150–200ms dwell on `mouseenter` before enlarging, to avoid tiles popping while a visitor sweeps the pointer across the 3-column grid. Immediate (no delay) on `focus` (keyboard) and `click`/`Enter`/`Space` — those are deliberate activations, not passive pointer movement. Decided as a low-stakes implementation default, not put to the user.
4. **Caption in the active state** — RESOLVED: keep the `<figcaption>` title/caption visible in the enlarged state — it lives below the image already and doesn't consume image area.
5. **Mobile behavior** — RESOLVED: `sm:`-and-up ONLY. On mobile (375px, `grid-cols-1`, tile already full-width) the zoom/enlarge interaction is disabled entirely; tiles render exactly as they do today, no change.
