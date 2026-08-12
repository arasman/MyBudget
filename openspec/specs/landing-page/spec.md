# landing-page Specification

## Purpose

Public landing page at `/` presenting MyBudget's value proposition to anonymous visitors: a curated feature showcase, benefits-focused copy, and outbound links, before requiring signup. Renders via a root-level `RootGate` component that branches on auth state, with a shared `PublicBackdrop` visual shell.

## Requirements

### Requirement: LANDING-1 — Anonymous Access, No Authenticated Data

The system MUST render `LandingView` for unauthenticated visitors at `/`. `LandingView` MUST NOT call any authenticated API endpoint and MUST NOT render user- or budget-specific data.

#### Scenario: Anonymous visitor sees landing without auth calls
- GIVEN a visitor with no auth token
- WHEN they navigate to `/`
- THEN `LandingView` renders and no authenticated endpoint is called

#### Scenario: No redirect loop for anonymous visitor
- GIVEN a visitor with no auth token
- WHEN they navigate to `/`
- THEN they are NOT redirected to `/login`

### Requirement: LANDING-2 — Curated Feature Showcase

The system MUST present exactly one curated hero image per feature area (~9 feature areas), sourced from the existing flow screenshots, each paired with brief benefits-focused copy. The showcase MUST NOT attempt to present all 89 screenshots.

#### Scenario: Nine feature areas displayed
- GIVEN the landing page renders
- WHEN the showcase section is inspected
- THEN one hero image and one copy block exist per feature area (~9 total)

### Requirement: LANDING-3 — Signup As Primary Call-To-Action

Sign-up MUST be the visually dominant call-to-action on the landing page. Sign-in MUST also be present but styled as secondary relative to sign-up.

#### Scenario: Signup is the primary CTA
- GIVEN the landing page renders
- WHEN the CTA area is inspected
- THEN the sign-up action uses primary visual treatment and sign-in uses secondary treatment

### Requirement: LANDING-4 — Secondary Outbound Links

The landing page MUST provide outbound links to the GitHub repository, the README, and the presentation deck. These MUST be visually subordinate to the primary sign-up CTA.

#### Scenario: Outbound links present and functional
- GIVEN the landing page renders
- WHEN a visitor clicks the GitHub, README, or deck link
- THEN the corresponding resource opens

#### Scenario: Outbound links are visually secondary
- GIVEN the landing page renders
- WHEN the CTA area and outbound links are compared
- THEN outbound links do not share the sign-up button's primary styling

### Requirement: LANDING-5 — Browser-Viewable Deck Link

The deck link MUST resolve to a format viewable directly in the browser. A PDF export MUST be attempted first; linking to the existing `.pptx` file MUST be an acceptable fallback for this change if PDF fidelity is unacceptable.

#### Scenario: Deck link opens a browser-viewable format
- GIVEN the landing page renders
- WHEN a visitor clicks the deck link
- THEN a PDF opens in-browser, or the `.pptx` fallback link is served

### Requirement: LANDING-6 — i18n Coverage

All landing copy MUST exist in both `en.json` and `es.json`. `LanguageSwitcher` MUST function on the landing page.

#### Scenario: Language switch updates landing copy
- GIVEN the landing page is rendered in `en`
- WHEN the visitor switches to `es` via `LanguageSwitcher`
- THEN all landing copy updates to Spanish without a page reload

### Requirement: LANDING-7 — Responsive Layout

The landing page MUST be readable and usable at both mobile and desktop viewport widths.

#### Scenario: Mobile viewport renders without overflow
- GIVEN a mobile viewport width
- WHEN the landing page renders
- THEN the showcase and CTAs render without horizontal overflow

### Requirement: LANDING-8 — Shared Public Visual Shell

The landing page MUST render with the shared `PublicBackdrop` visible behind its content, establishing one visual language with the authentication pages. The backdrop is mounted directly within `LandingView` (not within `PublicLayout`) to enable full-width landing content while maintaining the same palette and visual consistency as the centered-card auth views.

#### Scenario: Backdrop visible behind landing content
- GIVEN the landing page renders
- WHEN the page tree is inspected
- THEN `PublicBackdrop` renders behind the landing content, matching the auth pages' palette

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
