# Tasks: Landing Page & Visual Polish

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1000 (120+200+180+150+350, per design's Migration/Rollout table) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 0 → PR 1 → PR 2 → PR 3 → PR 4 |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | PDF export trial in isolation | PR 0 | `pnpm test -- export-pptx-pdf` | Manual local Windows run: `pnpm build-pptx && pnpm export-pptx-pdf`, compare 3 slide types | Delete `scripts/export-pptx-pdf.mjs` + package.json entry; no other file depends on it |
| 2 | Router restructure + RootGate | PR 1 | `pnpm test -- router` | Playwright anon-`/`-no-API-calls E2E (`pnpm exec playwright test`) | Revert `router/index.ts`, `AppLayout.vue`, `auth.store.ts`, delete `RootGate.vue`; landing stub has no other consumer |
| 3 | Brand tokens + backdrop + footer | PR 2 | `pnpm test -- AppFooter PublicBackdrop` | `pnpm build` visual smoke on `/login` and an authed route | Revert `main.css` tokens, delete `PublicBackdrop.vue`/`AppFooter.vue`, revert shell insertions |
| 4 | Showcase asset pipeline | PR 3 | `pnpm build-showcase` (manual output review, no vitest) | N/A — build script, no runtime UI wired yet | Delete `scripts/build-showcase.mjs`, `public/showcase/*.webp`, `config/showcase.ts` |
| 5 | Landing page components + wiring | PR 4 | `pnpm test -- landing` | Playwright full landing E2E (signup CTA, language switch, footer on public+authed) | Delete `features/landing/`, restore `RootGate.vue`'s PR1 placeholder stub |

## PR 0: PDF Export Trial (Windows-only, isolated)

- [x] 0.1 RED: write `Project/frontend/scripts/__tests__/export-pptx-pdf.spec.ts` — asserts the exported guard function returns failure on `process.platform !== 'win32'` and writes nothing (threat matrix: Subprocess — PowerPoint COM, case "non-Windows platform")
- [x] 0.2 RED: extend the same spec — asserts failure when the input `.pptx` path does not exist (threat matrix case "missing input")
- [x] 0.3 RED: extend the same spec — asserts failure when output is absent or zero-byte after the (mocked) COM call, never reported as success (threat matrix case "zero-byte/absent output")
- [x] 0.4 Add `scripts/**/*.{test,spec}.ts` to `include` in `Project/frontend/vitest.config.ts` so 0.1-0.3 run under `pnpm test`
- [x] 0.5 GREEN: implement `Project/frontend/scripts/export-pptx-pdf.mjs` — export a pure `checkPreconditions()`/`checkOutput()` pair (testable without spawning PowerShell) plus the `execFileSync('powershell', [...])` COM body (`Open(readOnly)` → `SaveAs(out, 32)` → `Close()`/`Quit()` in try/finally) reusing the `build-pptx.mjs`/`render-diagrams.mjs` script conventions
- [x] 0.6 Add `"export-pptx-pdf": "node scripts/export-pptx-pdf.mjs"` to `Project/frontend/package.json` scripts
- [x] 0.7 REFACTOR: run `pnpm test -- export-pptx-pdf`, confirm 0.1-0.3 pass; run `pnpm lint`
- [x] 0.8 Manual (gated, Windows-only, not part of `pnpm test`): run `pnpm build-pptx && pnpm export-pptx-pdf`, visually compare a screenshot-heavy slide, a diagram slide, and a text slide at 100% zoom against the `.pptx`; record pass/fail — this decides PDF vs `.pptx` fallback for PR 4's deck link (LANDING-5)

## PR 1: Router Restructure + RootGate (behavioral-risk slice)

- [x] 1.1 RED: extend `Project/frontend/src/router/__tests__/` (create if absent) — anonymous visitor at `/` renders `RootGate`'s landing branch and issues zero `/api/*` calls; no redirect to `/login` (LANDING-1, LAYOUT-3)
- [x] 1.2 RED: same suite — authenticated single-membership user at `/` still auto-redirects to `/budgets/:budgetId` (BUDSEL-1 regression)
- [x] 1.3 RED: same suite — authenticated multi-membership user at `/` still sees the selection list (BUDSEL-2 regression)
- [x] 1.4 RED: same suite — `forcePasswordChange` still redirects to `/forgot-password?reason=force` ahead of the `/` gate
- [x] 1.5 RED: same suite — anonymous visit to each of the 7 `/budgets/:budgetId` children (`cycles`, `cycles/:cycleId`, `categories`, `lines`, `lines/:lineId/customizations`, `cycles/:cycleId/matrix`, `bank-accounts`, `current-situation`, `dashboard`) redirects to `/login` (threat matrix: Routing — anonymous surface)
- [x] 1.6 RED: same suite — `fetchMe()` failure at `/` calls `authStore.clearSession()` and renders the landing (not `/login`); `fetchMe()` failure at `/budgets/x/cycles` still redirects to `/login` (Decision 5 regression split)
- [x] 1.7 RED: same suite — deleted-budget redirect still lands on `/` (existing `router/index.ts:185` guard, regression)
- [x] 1.8 RED: extend `Project/frontend/src/layouts/__tests__/AppLayout.spec.ts` — default slot renders `<RouterView />` fallback when no slot content is passed (LAYOUT-3 slot regression, do not duplicate existing cases)
- [x] 1.9 GREEN: add `clearSession()` to `Project/frontend/src/stores/auth.store.ts` — thin wrapper over existing `_clearTokens()`, no network call
- [x] 1.10 GREEN: create `Project/frontend/src/layouts/RootGate.vue` — branches on `authStore.isAuthenticated`: renders `LandingView` (placeholder stub this PR) or `<AppLayout><BudgetSelectionView /></AppLayout>`
- [x] 1.11 GREEN: modify `Project/frontend/src/router/index.ts` — promote the budget subtree to its own top-level record `path: 'budgets/:budgetId'` with `meta: { requiresAuth: true }`; keep `/` as a sibling record with `component: RootGate`, `name: 'BudgetSelection'` preserved, `meta: { public: true }`
- [x] 1.12 GREEN: modify the guard in `router/index.ts` — compute `needsAuth = to.meta.requiresAuth === true || (to.name === 'BudgetSelection' && authStore.isAuthenticated)`; on `/` only, a `fetchMe()` failure calls `authStore.clearSession()` and `return true` instead of `/login`
- [x] 1.13 GREEN: modify `Project/frontend/src/layouts/AppLayout.vue` — `<main><slot><RouterView /></slot></main>`, add `flex flex-col` to the root element so `RootGate`'s `<AppLayout><BudgetSelectionView /></AppLayout>` usage works
- [x] 1.14 GREEN: create placeholder `Project/frontend/src/features/landing/views/LandingView.vue` stub (minimal markup, no showcase/CTA yet — full build in PR 4)
- [x] 1.15 REFACTOR: run `pnpm test`, `pnpm build`; confirm 1.1-1.8 pass and no other route/guard test regresses (fixed a `mockResolvedValue` type error found during this verification pass — not present in the original agent output)

## PR 2: Brand Tokens + Backdrop + Footer

- [ ] 2.1 RED: create `Project/frontend/src/components/__tests__/AppFooter.spec.ts` — renders `© {currentYear} · Powered by ARAS Systems`, no anchor/link elements (LAYOUT-4)
- [ ] 2.2 RED: create `Project/frontend/src/components/__tests__/PublicBackdrop.spec.ts` — renders its default slot content
- [ ] 2.3 RED: extend `Project/frontend/src/i18n/__tests__/locales.spec.ts` — add `footer.*` key coverage check for `en.json` and `es.json` (LANDING-6 scope, footer half)
- [ ] 2.4 GREEN: add `@theme { --color-brand-* }` palette (seed `#7C3AED`/`#10B981` from `build-pptx.mjs`) plus semantic vars (`--brand-backdrop-from`, `--brand-footer-fg`) in `:root` and `[data-theme='dark']` to `Project/frontend/src/assets/main.css`
- [ ] 2.5 GREEN: create `Project/frontend/src/components/PublicBackdrop.vue` — `fixed inset-0 -z-10` decorative layer + `relative` slot wrapper
- [ ] 2.6 GREEN: create `Project/frontend/src/components/AppFooter.vue` — plain-text `© {year} · Powered by ARAS Systems`, no links, inherits shell background
- [ ] 2.7 GREEN: modify `Project/frontend/src/layouts/PublicLayout.vue` — wrap content in `<PublicBackdrop>`, append `<AppFooter />`, keep the centered-card contract for `/login`/`/register`/`/forgot-password`/`/reset-password`/`/invitations/accept` (LAYOUT-2), add a header bar rendering `LanguageSwitcher`
- [ ] 2.8 GREEN: modify `Project/frontend/src/layouts/AppLayout.vue` — append `<AppFooter />` after `<main>`
- [ ] 2.9 GREEN: add `footer.*` i18n keys to `Project/frontend/src/i18n/locales/en.json` and `es.json`
- [ ] 2.10 REFACTOR: run `pnpm test -- AppFooter PublicBackdrop locales`, `pnpm build`; visual smoke on `/login` and one authenticated route

## PR 3: Showcase Asset Pipeline

- [ ] 3.1 GREEN: create `Project/frontend/scripts/build-showcase.mjs` — `sharp` resize/WebP of the 9 curated `docs/slides/flows/*` PNGs into `Project/frontend/public/showcase/{slug}-{640,1280}.webp`, following the manual-regenerate posture of `build-pptx.mjs`/`render-diagrams.mjs` (no vitest coverage — a generated-artifact script, per design's testing strategy)
- [ ] 3.2 GREEN: add `"build-showcase": "node scripts/build-showcase.mjs"` to `Project/frontend/package.json` scripts
- [ ] 3.3 GREEN: create `Project/frontend/src/features/landing/config/showcase.ts` — `ShowcaseItem[]` const, 9 entries (`slug`, `source`, `i18nKey`) covering `auth`, `bank-accounts`, `budget-execution`, `budget-management`, `budget-structure-categories`, `budget-structure-cycles`, `budget-structure-periods-lines`, `current-situation`, `dashboard`
- [ ] 3.4 GREEN: create `Project/frontend/src/features/landing/config/links.ts` — GitHub repo/README/deck outbound URL consts (plain strings, not i18n keys)
- [ ] 3.5 Run `pnpm build-showcase`, review the 9 generated WebP outputs for framing/quality, commit `Project/frontend/public/showcase/*.webp`

## PR 4: Landing Page Components + Wiring

- [ ] 4.1 RED: extend `Project/frontend/src/i18n/__tests__/locales.spec.ts` — add `landing.*` key coverage check (hero, showcase captions per the 9 `i18nKey`s, CTA, links) for `en.json` and `es.json` (LANDING-6)
- [ ] 4.2 RED: create `Project/frontend/src/features/landing/__tests__/LandingView.spec.ts` — renders exactly 9 showcase tiles (LANDING-2), primary `/register` CTA styled `btn-primary` and secondary `/login` styled `btn-ghost` (LANDING-3), GitHub/README/deck links present and visually secondary (LANDING-4)
- [ ] 4.3 RED: extend the same spec — `ShowcaseTile` renders `<picture>` with srcset, `loading="lazy"`, explicit `width`/`height`
- [ ] 4.4 RED: extend the same spec — mobile viewport (e.g. 375px) renders showcase and CTAs without horizontal overflow (LANDING-7)
- [ ] 4.5 GREEN: build `Project/frontend/src/features/landing/components/LandingHero.vue`
- [ ] 4.6 GREEN: build `Project/frontend/src/features/landing/components/FlowShowcase.vue` and `ShowcaseTile.vue` — `aspect-[16/10] object-cover object-top` frame, `<picture>` srcset from `showcase.ts`, `loading="lazy"`, explicit dimensions
- [ ] 4.7 GREEN: build `Project/frontend/src/features/landing/components/LandingCta.vue` — primary `/register` `btn-primary`, secondary `/login` `btn-ghost`
- [ ] 4.8 GREEN: build `Project/frontend/src/features/landing/components/LandingLinks.vue` — GitHub/README/deck links from `links.ts`, visually subordinate to the CTA
- [ ] 4.9 GREEN: replace the PR 1 placeholder in `Project/frontend/src/features/landing/views/LandingView.vue` with `PublicBackdrop` + `LandingHero` + `FlowShowcase` + `LandingCta` + `LandingLinks`
- [ ] 4.10 GREEN: add `landing.*` i18n keys (hero, per-showcase-item title+caption, CTA, links) to `en.json` and `es.json`
- [ ] 4.11 GREEN: wire the deck link target per PR 0's trial result (0.8): PDF if the trial passed, `.pptx` fallback otherwise; update `README.md:285` accordingly
- [ ] 4.12 RED: add a Playwright E2E case — anonymous `/` shows the landing with zero authenticated API calls in the network log, no `/login` bounce (threat matrix: Routing — anonymous surface, full-stack confirmation of 1.1)
- [ ] 4.13 RED: extend the same E2E — `LanguageSwitcher` works on the landing without a page reload (LANDING-6); signup CTA navigates to `/register`; footer visible on both a public route and an authenticated route (LAYOUT-4)
- [ ] 4.14 GREEN: implement whatever landing markup is still missing to satisfy 4.12-4.13
- [ ] 4.15 REFACTOR: run `pnpm test`, `pnpm build`, `pnpm exec playwright test`; confirm all RED tests across PR 1-4 are GREEN; update `openspec/changes/landing-page-and-visual-polish/` success-criteria checklist in `proposal.md`
