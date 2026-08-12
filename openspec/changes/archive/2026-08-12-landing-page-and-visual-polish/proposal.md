# Proposal: Landing Page & Visual Polish

## Intent

MyBudget is deployed and public, but `/` redirects anonymous visitors straight to `/login`. A first-time visitor — TFM evaluator, recruiter, or prospective user — must create an account before seeing what the product does or why it matters. There is no page that sells the product, no way to reach the repo/README/deck from the app, and no brand identity: auth pages are a bare centered card on stock daisyUI, and no shell shows authorship. This change adds a public landing page and gives the public surface (landing + auth) one shared visual language, plus a global footer, so the deployed app can be evaluated before signup.

## Scope

### In Scope

- **Landing page** at `/` for unauthenticated visitors: "train tracks" showcase of the 9 feature flows using `docs/slides/flows/*.png` (framed/masked — inconsistent aspect ratios), benefits-focused ES/EN copy, and outbound links (GitHub repo, README, presentation deck, sign in, sign up).
- **Root route gate**: `/` renders the landing for anonymous users and today's `BudgetSelectionView` for authenticated users. `/` stays the authenticated home.
- **Shared public backdrop**: one `PublicBackdrop` component behind both the landing and the 4 auth views via `PublicLayout`.
- **Global footer** ("© {year} · Powered by ARAS Systems", final wording at spec) in both shells via one `AppFooter` component.
- **Brand tokens**: first formal palette/typography, defined once and reused by landing, backdrop, and footer.
- **Deck link target**: trial a PowerPoint→PDF export so the deck opens in a browser; fall back to today's `.pptx` download link if the trial fails.

### Out of Scope

- Renaming the authenticated home to `/home`.
- Re-shooting or re-styling the 89 flow screenshots.
- Rebuilding the deck as HTML/reveal.js; any CI-automated deck export.
- Restyling authenticated views, navbar, or in-app content.
- Marketing analytics, SEO/meta program, cookie banner, contact form.
- Re-theming the whole app (see Open Question 1).

## Capabilities

### New Capabilities

- `landing-page`: public landing at `/` — flow showcase, value copy, outbound links, ES/EN, responsive, anonymous access.

### Modified Capabilities

- `app-layout`: `LAYOUT-2` (public shell gains shared backdrop behind the card), `LAYOUT-3` (root route renders public or authenticated tree by auth state; `App.vue` still only a `<RouterView>`), `BUDSEL-1`/`BUDSEL-2` (scoped to authenticated users at `/`), plus a new footer requirement covering both shells.
- `frontend-scaffold`: **only if** brand tokens change the daisyUI `light`/`dark` themes (see Open Question 1). If tokens are additive CSS variables in `main.css`, no delta.

## Approach

Carried from exploration `sdd/explore/landing-page-and-visual-polish` (lowest-risk options, not open questions):

| Decision | Choice | Why |
|---|---|---|
| Routing | Gate component at `/` (A.1) | `goHome()`, the deleted-budget redirect, and BUDSEL-1/2 keep working; smallest diff |
| Auth visuals | Shared `PublicBackdrop` (B.1) | One source of truth; the 4 auth views stay untouched; `LAYOUT-2`'s card contract preserved |
| Footer | Two shell insertions of one `AppFooter` (C.1) | Respects `LAYOUT-3`'s "App.vue only a RouterView"; footer inherits each shell's background |
| Deck | Trial PDF via PowerPoint COM `ppSaveAsPDF` (D.1) | Cheap, reuses the existing PNG-export pattern; decide HTML deck only if it fails |

Frontend-only. New files: `LandingView.vue`, `PublicBackdrop.vue`, `AppFooter.vue`, root gate; brand tokens in `src/assets/main.css`.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `frontend/src/views/LandingView.vue` (+ landing components) | New | Landing page, flow showcase, links |
| `frontend/src/components/PublicBackdrop.vue`, `AppFooter.vue` | New | Shared backdrop + footer |
| `frontend/src/router/index.ts` | Modified | `/` public/authenticated gate; guard no longer forces `/` → `/login` |
| `frontend/src/layouts/PublicLayout.vue`, `AppLayout.vue` | Modified | Backdrop + footer insertion |
| `frontend/src/assets/main.css` | Modified | Brand tokens |
| `frontend/src/i18n/locales/{en,es}.json` | Modified | Landing + footer copy (both locales) |
| `frontend/public/` or `docs/slides/flows/**` | New/Reused | Web-served copies of showcase images |
| `docs/slides/presentation/`, `README.md:285` | Modified | Browser-viewable deck artifact + link |
| `openspec/specs/app-layout/spec.md` | Modified | Delta specs |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Root-route change breaks `goHome()`, deleted-budget redirect, or e2e assumptions about `/` | Med | Gate keeps `/` stable; explicit unit + e2e regression coverage for anon vs. authed `/` |
| Auth-state flash (landing briefly shown to an authenticated user on reload) | Med | Resolve auth state before render; spec an explicit loading state |
| 89 screenshots at variable aspect ratio look ragged in the showcase | High | Fixed framing/mask or SVG track frame decided at design; no native-size drop-in |
| Landing image payload hurts first-load performance | Med | Web-optimized subset, lazy-load, responsive sizes; not all 89 images |
| `ppSaveAsPDF` is untested here and Windows/PowerPoint-only | Med | Trial before committing; document as a manual local step; fall back to `.pptx` link |
| Brand palette forces a wider re-theme than intended | Med | Open Question 1 answered before spec |
| Professional design bar not met on a first pass | Med | Landing is additive and revertible; iterate behind one component tree |

## Rollback Plan

Additive and frontend-only — no migration, no backend, no data. Revert `feat/landing-page-and-visual-polish`: delete the new components/view, restore `/` as an auth-only route in `router/index.ts`, restore `PublicLayout.vue`/`AppLayout.vue`, drop brand tokens and new i18n keys. Deck export is a separate local script + artifact; deleting it restores today's `.pptx` link.

## Dependencies

- Branch `feat/landing-page-and-visual-polish` created before the cycle (branch-before-cycle convention).
- Existing assets: `docs/slides/flows/` (89 PNGs), `docs/slides/presentation/MyBudget.pptx`.
- PDF trial requires Windows + PowerPoint (local, manual).

## Security & i18n Notes

- `/` becomes reachable without authentication; the landing MUST render no user or budget data and MUST NOT call authenticated APIs. All other `requiresAuth` routes keep their current guard.
- All landing and footer copy MUST exist in `en.json` and `es.json`; `LanguageSwitcher` MUST work on the landing.

## Success Criteria

- [x] Anonymous visitor at `/` sees the landing; authenticated user at `/` sees budget selection/auto-redirect exactly as today. (PR 1 router unit tests + PR 4 `e2e/landing/landing.spec.ts` full-stack confirmation, zero `/api/*` calls, no `/login` bounce)
- [x] The 9 feature flows are presented as a coherent showcase, readable on mobile and desktop. (`FlowShowcase`/`ShowcaseTile`, responsive `grid-cols-1 sm:grid-cols-2 lg:grid-cols-3`, LANDING-2/LANDING-7 tests)
- [x] GitHub repo, README, deck, sign in, and sign up links all resolve from the landing. (`LandingLinks` + `LandingCta`, `config/links.ts`)
- [x] The 4 auth pages share the landing's visual language via one backdrop component. (PR 2 `PublicBackdrop`, reused by `PublicLayout` and `LandingView`)
- [x] Footer renders on every authenticated and public page. (PR 2 `AppFooter` in `AppLayout`/`PublicLayout`/`LandingView`; confirmed public + authenticated in E2E)
- [x] Landing and footer copy render in EN and ES. (`landing.*`/`footer.*` keys in `en.json`/`es.json`, `i18n/__tests__/locales.spec.ts`)
- [x] No unauthenticated data exposure; existing auth-guarded routes unchanged. (LANDING-1 unit + E2E network-log assertion; PR 1 regression coverage for all `requiresAuth` routes)
- [x] Deck opens in-browser, or the fallback download link is documented. (PR 0 PDF trial passed — `docs/slides/presentation/MyBudget.pdf` committed and linked from `README.md:285` and `config/links.ts`)

## Open Questions (need sign-off before spec)

1. **Brand palette blast radius** — do brand tokens apply only to landing/auth/footer (additive CSS variables, no spec delta), or replace the app-wide daisyUI `light`/`dark` primaries (requires a `frontend-scaffold` delta and a visual re-check of every authenticated view)?
2. **Footer wording and links** — is "© {year} · Powered by ARAS Systems" final, and does the footer carry links (repo, deck, version/commit) or text only?
3. **Showcase depth** — a curated subset (one hero image per feature area, ~9) or a deeper walkthrough (more of the 89)? Drives both design effort and page weight.
4. **Deck trial gate** — if the PDF export loses fidelity, is keeping the `.pptx` download acceptable for this change, with the HTML deck deferred?
5. **Signup posture** — should the landing push signup as the primary CTA, or is this primarily an evaluation/portfolio surface where "view the code / view the deck" ranks equally?
