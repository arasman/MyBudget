# landing-page Specification

## Purpose

Public landing page at `/` presenting MyBudget's value proposition to anonymous visitors: a curated feature showcase, benefits-focused copy, and outbound links, before requiring signup. Renders inside shared PublicLayout/PublicBackdrop shell (see `app-layout` LAYOUT-2/LAYOUT-3).

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

The landing page MUST render inside `PublicLayout` with the shared `PublicBackdrop` visible behind its content, establishing one visual language with the authentication pages.

#### Scenario: Backdrop visible behind landing content
- GIVEN the landing page renders
- WHEN the page tree is inspected
- THEN `PublicBackdrop` renders behind the landing content, matching the auth pages' palette
