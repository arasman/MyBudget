# Delta for landing-page

## ADDED Requirements

### Requirement: LANDING-9 — Showcase Tile Enlarge On Interaction

On viewports `sm:` (≥640px) and up, the system MUST let a visitor enlarge one showcase tile at a time to magnify the same `aspect-[16/10]` crop already rendered — no crop or aspect change. Enlarge MUST be reachable by mouse hover, keyboard focus, and click/tap, driven by a single active-tile state (not separate hover/click paths). While a tile is active, non-active tiles MUST be visually de-emphasized and excluded from the tab/reading order via `aria-hidden`/`inert`. The `<figcaption>` title/caption MUST remain visible in the active state. Below `sm:` (mobile, `grid-cols-1`), the interaction MUST be a no-op — tiles render exactly as they do without this change. The enlarge/dismiss transition MUST respect `prefers-reduced-motion: reduce`. This requirement MUST NOT regress LANDING-7 (no horizontal overflow) at any viewport, idle or active.

#### Scenario: Desktop mouse hover enlarges after a dwell delay

- GIVEN a `sm:`-and-up viewport and the showcase grid at rest
- WHEN a visitor's pointer stays over a tile for a short dwell period (~150-200ms)
- THEN that tile enlarges to the full grid-container width and its `<figcaption>` remains visible
- AND a pointer merely sweeping across tiles without dwelling on any one does not trigger enlarge

#### Scenario: Keyboard focus enlarges immediately, no dwell

- GIVEN a `sm:`-and-up viewport
- WHEN a visitor Tabs to a showcase tile `<button>`
- THEN that tile enlarges immediately (no dwell delay), matching the hover-active state
- AND the enlarge is visibly synchronized with the native focus ring, satisfying WCAG 1.4.13

#### Scenario: Click or tap enlarges immediately

- GIVEN a `sm:`-and-up viewport
- WHEN a visitor clicks, taps, or presses Enter/Space on a showcase tile
- THEN that tile enlarges immediately, with the same result as hover or focus enlarge

#### Scenario: Tap-outside dismisses the active tile

- GIVEN a `sm:`-and-up viewport with a tile in the active/enlarged state
- WHEN the visitor taps or clicks outside the active tile
- THEN the tile returns to its idle grid size and no visible close control is required

#### Scenario: Escape dismisses the active tile

- GIVEN a `sm:`-and-up viewport with a tile in the active/enlarged state
- WHEN the visitor presses Escape
- THEN the tile returns to its idle grid size and focus remains on a reachable element

#### Scenario: Sibling tiles are dimmed and removed from tab order while one is active

- GIVEN a `sm:`-and-up viewport with one tile active
- WHEN the other showcase tiles are inspected
- THEN each non-active tile carries `aria-hidden`/`inert` and is visually de-emphasized
- AND pressing Tab does not move focus into a non-active tile

#### Scenario: Interaction is disabled below the `sm:` breakpoint

- GIVEN a mobile viewport (375px, `grid-cols-1`)
- WHEN a visitor hovers, taps, or focuses a showcase tile
- THEN the tile does not enlarge and renders exactly as it does today, with no active-state markup applied

#### Scenario: Reduced motion skips the enlarge animation

- GIVEN `prefers-reduced-motion: reduce` is set and a `sm:`-and-up viewport
- WHEN a tile becomes active via hover, focus, or click
- THEN the enlarged state appears without a scale/opacity transition

#### Scenario: No horizontal overflow while a tile is active (LANDING-7 regression guard)

- GIVEN a 375px-wide viewport (mobile, interaction disabled) and a `sm:`-and-up viewport with a tile active
- WHEN the page is inspected in each state
- THEN `document.scrollWidth` does not exceed `document.clientWidth` in either case, preserving LANDING-7
