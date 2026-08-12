# Design: Showcase Tile Hover/Focus Zoom

## Technical Approach

Frontend-only, additive, one PR. `FlowShowcase.vue` gains a single `activeSlug` state (extracted into a
`useShowcaseZoom()` composable, per the `features/*/composables/` precedent) fed by hover-with-dwell, focus,
and click. `ShowcaseTile.vue`'s root `<figure>` becomes `<button><figure>` for free focus + Enter/Space.
The active tile is lifted out of flow **inside its own grid cell** and widened to the exact grid-container
width with a percentage `calc()`; siblings stay put, dimmed and `inert`. Implements LANDING-9; LANDING-2
(9 tiles) and LANDING-7 (no overflow) stay green as regression guards.

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|---|---|---|---|
| 1 | State plumbing | `activeSlug` in `FlowShowcase` via `useShowcaseZoom()`; **props down** (`active`, `dimmed`, `zoomVars`), **events up** (`hover-in`, `hover-out`, `activate`) | `provide/inject`; per-tile local state | One level of nesting — props/emits keep `ShowcaseTile` a pure presentational unit renderable standalone in its spec. Inject makes the tile untestable without a host and unenforced by types. |
| 2 | `sm:`-and-up gate | **JS `matchMedia('(min-width: 640px)')` is authoritative**, plus `sm:` Tailwind prefixes on every active-state class as defense in depth | CSS-only; JS-only | LANDING-9 requires *no active-state markup* below `sm:` — `inert`/`aria-hidden`/timers are DOM state CSS cannot suppress. The flash-of-wrong-state risk is removed by a `change` listener that clears `activeSlug` when the query stops matching; the `sm:` prefixes mean even a gate regression cannot break mobile layout. |
| 3 | Enlarge geometry | Active card is `absolute` inside its own `relative` grid cell, shifted left and widened by a `calc()` on the cell width (see below); the cell keeps its box via a `::before` spacer | (a) `grid-column: 1 / -1` reflow; (b) abspos **grid child** with `grid-row: N`; (c) `transform: scale()` | (a) moves the tile out from under the pointer and pushes the CTA/footer ~700px down on every hover. (b) is spec-illegal here: abspos grid children can only be placed on **explicit** lines, and this grid has no `grid-template-rows`. (c) at ~3.1× rasterizes blurry and blows the caption up 3×. The chosen form keeps the cell as the vertical anchor (free, exact) and derives horizontal placement from the column index. |
| 4 | Pointer dismiss | `mouseleave` is bound to the **grid container**, not the tile; entering another tile re-arms the dwell timer | `mouseleave` per tile | The enlarged card overlaps its neighbours, so per-tile leave events fire spuriously and oscillate. One container-level leave is also the natural "tap/click outside" boundary. |
| 5 | Dwell | 175ms `setTimeout` on `mouseenter`, cleared on leave/other-tile; `focus`, `click`, `Enter`, `Space` activate with **zero** delay | Uniform delay | Keyboard and touch are deliberate acts; only the sweeping pointer needs intent filtering. |
| 6 | Sibling de-emphasis | Non-active tiles get `inert` + `aria-hidden="true"` + `opacity-40`, all reverted on dismiss | `opacity` alone | Dimmed-but-focusable tiles are a tab trap (proposal risk). `inert` also blocks their pointer events, which keeps decision 4 clean. |
| 7 | Motion | New `.showcase-zoom-card` component class in `main.css`: `transition: opacity 180ms, width 180ms, left 180ms ease-out`. Under `prefers-reduced-motion: reduce` → `transition: none`, so the enlarged state simply appears | Animating `transform` | Positioning already uses `left`/`width`, so no transform is involved and reduced-motion needs no scale unwind. The dwell timer is intent, **not** motion — it is unaffected by the media query. |
| 8 | Accessible name | Button `aria-label` = `landing.showcase.enlarge` (`"Enlarge {title}"`); visually-hidden `landing.showcase.dismissHint` on the active tile | Visible close button | User decision: tap-outside/Escape only, no close control. |

### Geometry

```css
/* .showcase-zoom-card — applied only at >=sm and only while active.
   Percentages resolve against the containing block = the grid cell. */
position: absolute; top: 0; z-index: 20;
--gap: 1.5rem;                                            /* mirrors gap-6 */
left:  calc(-1 * var(--zoom-col) * (100% + var(--gap)));
width: calc(var(--zoom-cols) * 100% + (var(--zoom-cols) - 1) * var(--gap));
```

At `lg` (3 cols): `3 × 368px + 2 × 24px = 1152px` = exactly `max-w-6xl`. `--zoom-col`/`--zoom-cols` are
unitless integers set inline by the composable, so **no inline `px` width exists** and the LANDING-7 unit
guard (`el.style.width`) is untouched. By construction the card can never exceed the container.

## Data Flow

    mouseenter ──175ms dwell──┐
    focus / click / Enter/Space ──┤──► FlowShowcase.activeSlug = slug   (only if matchMedia >= 640px)
                                  │        ├─► active tile  : --zoom-col/--zoom-cols + .showcase-zoom-card + ::before spacer
    Escape / grid mouseleave /    │        └─► other 8 tiles: inert + aria-hidden + opacity-40
    click outside grid ───────────┴──► activeSlug = null   (also on matchMedia change → false)

## File Changes

| File | Action | Description |
|---|---|---|
| `.../landing/composables/useShowcaseZoom.ts` | Create | `activeSlug`, `isEnabled`, `columns`, dwell timer, Escape + outside-click listeners, `zoomVars(index)` |
| `.../landing/composables/__tests__/useShowcaseZoom.spec.ts` | Create | Pure-logic RED tests |
| `.../landing/components/FlowShowcase.vue` | Modify | Owns the composable, `relative` grid, container-level `mouseleave`, per-tile props/handlers |
| `.../landing/components/ShowcaseTile.vue` | Modify | `<button>` root wrapping the existing `<figure>`; `active`/`dimmed`/`zoomVars` props; `aria-label`; `figcaption` stays visible when active |
| `.../landing/__tests__/{ShowcaseTile,FlowShowcase}.spec.ts` | Create | Component specs (none exist today) |
| `.../landing/__tests__/LandingView.spec.ts` | Modify | Keep tile count at 9 and the LANDING-7 inline-width guard green |
| `src/assets/main.css` | Modify | `.showcase-zoom-card` + first `prefers-reduced-motion` block in the codebase |
| `src/i18n/locales/{en,es}.json` | Modify | `landing.showcase.enlarge`, `landing.showcase.dismissHint` |
| `e2e/landing/landing.spec.ts` | Modify | Interaction + keyboard + no-overflow-while-active cases |

## Interfaces / Contracts

```ts
export function useShowcaseZoom(items: ShowcaseItem[]): {
  activeSlug: Ref<string | null>
  isEnabled: Ref<boolean>                       // matchMedia('(min-width: 640px)')
  columns: Ref<1 | 2 | 3>                       // 3 at lg, 2 at sm, 1 below (gate off)
  hoverIn(slug: string): void                   // arms the 175ms dwell timer
  hoverOut(): void                              // clears a pending timer only
  activateNow(slug: string): void               // focus / click / Enter / Space
  deactivate(): void                            // Escape / outside / grid leave / breakpoint down
  zoomVars(index: number): Record<string, string>  // { '--zoom-col': '2', '--zoom-cols': '3' }
}
```

## Testing Strategy

`strict_tdd: true` — RED first. jsdom has **no `matchMedia`** and no layout: every new spec must
`vi.stubGlobal('matchMedia', ...)` (no global setup file exists in `vitest.config.ts`), and geometry is
asserted as emitted CSS custom properties/classes, never as measured pixels.

| Layer | What to Test | Approach |
|---|---|---|
| Unit — composable | Dwell fires at 175ms and is cancelled on early leave; focus/click activate synchronously; Escape/outside/leave clear; gate off → `activateNow` is a no-op; `zoomVars(4)` with 3 cols → `col 1`; breakpoint down clears `activeSlug` | Vitest fake timers + stubbed `matchMedia` |
| Unit — `ShowcaseTile` | Root is a `<button>` with an `aria-label`; `active` adds `showcase-zoom-card` + the vars and **keeps** `<figcaption>` visible; `dimmed` sets `inert` + `aria-hidden`; idle markup byte-identical to today | `@testing-library/vue` |
| Unit — `FlowShowcase` | At most one active slug; the other 8 are `inert`; below `sm:` no active-state attribute is ever applied; Escape and outside click dismiss | `@testing-library/vue` + `userEvent` |
| Unit — regression | `LandingView.spec.ts`: still exactly 9 `showcase-tile` nodes (the enlarge must not duplicate a node) and no inline `px` width > 375 | Existing spec, extended |
| Unit — i18n | Both new keys in `en.json` **and** `es.json` | Existing `i18n/__tests__/locales.spec.ts` |
| E2E | 1280×800: dwell-hover enlarges to the container width and siblings are `inert`; sweep without dwell does not enlarge; Tab enlarges, Escape dismisses; **`scrollWidth <= clientWidth` while a tile is active**; 375×812 hover/tap enlarges nothing and the existing LANDING-7 assertion still passes | Playwright, full Docker stack |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration
boundary. Frontend presentation only; the showcase stays anonymous and issues no API call (LANDING-1 unaffected).

## Migration / Rollout

No migration, no feature flag, no asset regeneration. Single PR, forecast ~280 changed lines —
**400-line budget risk: Low**.

**Rollback boundary**: revert the branch. Concretely — restore `ShowcaseTile.vue`'s `<figure>` root, delete
`useShowcaseZoom.ts` + its spec, revert `FlowShowcase.vue` to the static grid, drop the `main.css`
`.showcase-zoom-card` and `prefers-reduced-motion` block, drop the two `landing.showcase.*` keys from both
locales, delete `ShowcaseTile.spec.ts`/`FlowShowcase.spec.ts` and the new E2E cases, and drop LANDING-9.
No runtime dependency, no generated artifact, and no other feature reads any of it.

## Open Questions

- [ ] None. All five proposal questions were answered before this design; the geometry `calc()` is the only
      item that must be visually confirmed at `sm` (2 cols) and `lg` (3 cols) during apply.
