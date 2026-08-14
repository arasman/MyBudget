# user-guide Specification

## Purpose

Bilingual (EN default, ES alternate) static HTML user guide, 1:1 with the app's 10 real nav
areas, served via Vite's `public/` passthrough with no SPA framework or client-side JS required
to render.

## Requirements

### Requirement: UG-1 — Chapter Set and Locale Parity

The guide MUST contain exactly 10 chapters — `auth`, `bank-accounts`, `budget-execution`,
`budget-management`, `budget-structure-categories`, `budget-structure-cycles`,
`budget-structure-periods-lines`, `current-situation`, `dashboard`, `members` — each authored as
a standalone `.html` page under both `public/guide/en/` and `public/guide/es/`. Both locale
versions of a chapter MUST share the same headings and image set.

#### Scenario: All chapters exist in both locales

- GIVEN the guide is built
- WHEN `public/guide/en/` and `public/guide/es/` are listed
- THEN each contains one `.html` file per of the 10 chapters, matching headings and images

### Requirement: UG-2 — Standalone Chapter Pages

Each chapter MUST be its own static HTML page reachable at its own URL, MUST render fully
without an iframe, and MUST NOT require JavaScript to display its content.

#### Scenario: Chapter renders via direct deep link

- GIVEN a visitor navigates directly to `/guide/en/budget-execution.html` (no prior navigation)
- WHEN the page loads with JavaScript disabled
- THEN the chapter content, sidebar, and locale toggle all render correctly

### Requirement: UG-3 — Sidebar Navigation

Every chapter page MUST render a sidebar linking to all 10 chapters in the same locale, with the
current chapter visually marked as active.

#### Scenario: Sidebar links to all chapters with current one marked

- GIVEN any chapter page is open
- WHEN the sidebar is inspected
- THEN it lists all 10 chapters as links and the currently open chapter is marked distinctly

### Requirement: UG-4 — Locale Toggle

Every chapter page MUST offer a locale toggle linking to the sibling page of the same chapter in
the other language.

#### Scenario: Locale toggle switches to the sibling chapter

- GIVEN `/guide/en/dashboard.html` is open
- WHEN the visitor activates the locale toggle
- THEN `/guide/es/dashboard.html` loads

### Requirement: UG-5 — Screenshot Reuse and Asset Placement

Chapters other than `members` MUST reference screenshots copied from the existing
`docs/slides/flows/<area>/*.png` captures into `public/guide/assets/<area>/`. Both locale
versions of a chapter MUST reference the same (EN-only) images — no new or ES-locale captures
are taken.

#### Scenario: EN and ES chapter share the same images

- GIVEN a non-`members` chapter exists in both locales
- WHEN the `<img>` sources of each version are compared
- THEN both reference the identical files under `public/guide/assets/<area>/`

### Requirement: UG-6 — Members Chapter Scope

The `members` chapter MUST be text-only (no `<img>` elements) and MUST document only
administration actions — list, role change, remove, restore — and invite-sending. It MUST NOT
describe invite *acceptance* and MUST link out to the `budget-management` chapter for that flow.

#### Scenario: Members chapter has no images

- GIVEN `/guide/en/members.html` is open
- WHEN the page is inspected
- THEN it contains no `<img>` elements

#### Scenario: Members chapter links to budget-management for acceptance

- GIVEN `/guide/en/members.html` is open
- WHEN the invite flow is discussed
- THEN only invite-sending is described, with a link to the `budget-management` chapter for
  acceptance

### Requirement: UG-7 — Build and Hosting

The guide MUST be placed under `Project/frontend/public/guide/` so that `pnpm build` copies it
into `dist/` via Vite's `public/` passthrough, with no Caddyfile or `docker-compose.prod.yml`
change required.

#### Scenario: pnpm build ships the guide with no infra change

- GIVEN `public/guide/` contains the chapter tree
- WHEN `pnpm build` runs
- THEN `dist/guide/` contains the same tree, and no Caddyfile/compose file was modified

### Requirement: UG-8 — Diagram Renderer Generalization

`render-diagrams.mjs` MUST accept source and output path arguments and emit `.svg` output. When
invoked with no arguments, it MUST still produce its existing default deck output (PNG, current
source), unchanged.

#### Scenario: New invocation renders SVG to a custom path

- GIVEN a source diagram file and a target output path are passed as arguments
- WHEN `render-diagrams.mjs` runs
- THEN an `.svg` file is written to the given output path

#### Scenario: No-args invocation keeps existing deck behavior

- GIVEN `render-diagrams.mjs` is invoked with no arguments
- WHEN it runs
- THEN it produces the same default deck output (source and PNG format) as before this change
