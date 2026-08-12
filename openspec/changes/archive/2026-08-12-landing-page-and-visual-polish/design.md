# Design: Landing Page & Visual Polish

## Technical Approach

Frontend-only, additive. Four independent pieces:

1. **Root gate** — `/` becomes a `public` route whose component (`RootGate.vue`) renders `LandingView` for anonymous visitors and `AppLayout` + `BudgetSelectionView` for authenticated ones. The budget subtree is promoted to its own top-level record `/budgets/:budgetId` that owns `meta: { requiresAuth: true }`, so the landing is structurally unreachable from any budget path.
2. **Shared backdrop** — one `PublicBackdrop.vue` (slotted, viewport-fixed decorative layer) used by both `PublicLayout.vue` and `LandingView.vue`.
3. **Brand tokens + footer** — additive Tailwind v4 `@theme` tokens in `main.css` (no daisyUI theme mutation), one `AppFooter.vue` mounted in both shells.
4. **Deck PDF** — a manual `pnpm export-pptx-pdf` script (PowerShell + PowerPoint COM `ppSaveAsPDF = 32`), trialled before the landing link is wired, with a `.pptx` fallback.

Landing lives in `Project/frontend/src/features/landing/` (multi-component surface → follows the `features/dashboard` module convention, not flat `src/views/`). `RootGate.vue` joins `src/layouts/`; `PublicBackdrop.vue` and `AppFooter.vue` join `src/components/` next to `LanguageSwitcher.vue`.

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|---|---|---|---|
| 1 | Root-route mechanism | Component gate at `/` + sibling `/budgets/:budgetId` record carrying `requiresAuth` | (a) rename authed home to `/home`; (b) keep the nested tree and move `requiresAuth` to the inner child | `/` stays stable, so `goHome()` (`AppLayout.vue:66-69`), the deleted-budget redirect (`router/index.ts:185`), and `LoginView`/`RegisterView` `push('/')` need no change. (b) leaves `RootGate` wrapping every authed page, so a guard regression could render the landing under a budget URL; sibling records make that structurally impossible. |
| 2 | Root route **name** | Keep `name: 'BudgetSelection'` on the `/` record | New name `Root` | `BudgetTabs.vue:5` and `BudgetMatrixView.vue:380` push `{ name: 'BudgetSelection' }`, and `AppLayout.vue:103` hides the budget switcher via `route.name !== 'BudgetSelection'`. Keeping the name is a **zero-line diff** on all three. |
| 3 | Authed-user pipeline at `/` | Guard computes `needsAuth = to.meta.requiresAuth === true \|\| (to.name === 'BudgetSelection' && authStore.isAuthenticated)` | Declaring `/` as `requiresAuth` | `/` must be anonymous-reachable, but an authenticated visitor still needs `fetchMe()`, the `forcePasswordChange` redirect, and memberships loaded before `BudgetSelectionView` renders. One computed boolean preserves today's behavior for signed-in users without exposing the route. |
| 4 | Flash-of-landing on reload | None possible — no extra loading state needed | `isPending` flag / suspense wrapper | `isAuthenticated` is `!!accessToken` read **synchronously** from `localStorage` at store init (`auth.store.ts:46,51`). `beforeEach` runs and `await`s `fetchMe()` **before** first render, so `RootGate` paints the correct branch on first frame. Documented explicitly so nobody adds a redundant spinner. |
| 5 | Dead session hitting `/` | On `/` only, a `fetchMe()` failure calls a new `authStore.clearSession()` and lets the **landing** render (`return true`) instead of redirecting to `/login` | Reuse today's `return '/login'` | An evaluator with a stale token in localStorage would otherwise be bounced to a login form — the exact failure this change exists to remove. `clearSession()` (a thin public wrapper over `_clearTokens()`) avoids `logout()`'s network POST, which would 401 → refresh → fail again. All other `requiresAuth` routes keep the `/login` redirect. |
| 6 | AppLayout reuse from the gate | Add a default slot with a fallback: `<main><slot><RouterView /></slot></main>` | Duplicate the navbar in `RootGate` | One line. Slot content wins when `RootGate` renders `<AppLayout><BudgetSelectionView /></AppLayout>`; the fallback keeps `/budgets/*` behaving exactly as today. |
| 7 | Backdrop geometry | `PublicBackdrop.vue` = `fixed inset-0 -z-10` decorative layer + `relative` slot wrapper | `absolute inset-0` inside a `min-h-screen` container | An absolute gradient stretches over the landing's full scroll height and looks washed out, while it fits the short auth card. A viewport-fixed layer renders identically behind a 400px login card and a 4000px landing page — the single requirement that makes one component serve both. |
| 8 | Brand tokens layering | Two tiers in `main.css`: (a) `@theme { --color-brand-* }` palette → generates `bg-brand-500` etc.; (b) semantic vars (`--brand-backdrop-from`, `--brand-footer-fg`) in `:root` + `[data-theme='dark']` overrides | Overriding daisyUI `light`/`dark` `--color-primary` | daisyUI v5 defines its theme vars inside `[data-theme=...]` selectors; `@theme` under a `brand-` namespace never collides, so **no `frontend-scaffold` spec delta** and no visual re-check of authenticated views. Tier (b) exists because backdrop/footer surfaces must still follow the theme toggle. Seed values reuse `build-pptx.mjs` (`#7C3AED`, `#10B981`) so the app and the deck share one identity. |
| 9 | Screenshot framing | Uniform `aspect-[16/10]` window, `object-cover object-top`, rounded border + shadow | Custom SVG browser-chrome frame; native-size drop-in | The 89 PNGs are 1280px wide with variable height and are top-anchored (navbar → header → first rows), so a top-crop keeps the meaningful region. One CSS box gives the "train tracks" rhythm with zero per-image tuning; an SVG frame needs per-image alignment and degrades on mobile. |
| 10 | Showcase asset pipeline | `pnpm build-showcase` (`scripts/build-showcase.mjs`, uses the already-installed `sharp`) emits 9 curated images at 2 widths as WebP into `Project/frontend/public/showcase/` | Import PNGs directly from `docs/slides/flows/` | Those files live outside the Vite root and would need `server.fs.allow`; raw PNGs are also multi-MB. The script matches the manual-regenerate posture of `build-pptx` / `render-diagrams` and makes the curated list one source of truth. |
| 11 | Outbound link targets | GitHub blob URLs (repo, README, deck) in a plain `src/features/landing/config/links.ts` const — **not** i18n keys | Copying the PDF into `public/` and serving it from the app | URLs are not translated content; GitHub renders PDFs in-browser, so the deploy payload stays unchanged whether the deck ends up PDF or `.pptx`. |
| 12 | CTA hierarchy | One primary `btn-primary` → `/register`; `/login` as `btn-ghost`; repo/README/deck as a secondary link row | Equal-weight evaluation links | Confirmed by the user: signup is the primary call-to-action, supporting links are secondary. |
| 13 | Footer placement | `AppFooter.vue` inserted twice — in `AppLayout.vue` after `<main>` and in `PublicLayout.vue` inside the backdrop | Wrapping `<RouterView>` in `App.vue` | `LAYOUT-3` requires `App.vue` to contain only a root `<RouterView>`; two insertions of one component respect it and let the footer inherit each shell's background. Both shells become `flex flex-col` with `main`/content `flex-1` so the footer pins to the viewport bottom on short pages. |
| 14 | Deck export trigger | Manual `pnpm export-pptx-pdf`, run after `pnpm build-pptx`; never in CI | Any CI/deploy automation | PowerPoint COM is Windows + Office only; the Hetzner host and CI cannot run it. Same posture as `e2e:slides` and `render-diagrams`. |

## Data Flow

### Root route resolution

    URL "/" ──→ router.beforeEach
                     │
                     ├─ isAuthenticated == false ──────────────────────────► RootGate → PublicBackdrop → LandingView
                     │                                                                     (no API calls)
                     └─ isAuthenticated == true
                            ├─ forcePasswordChange ──► /forgot-password?reason=force
                            ├─ fetchMe() ok ─────────► RootGate → AppLayout(slot) → BudgetSelectionView
                            └─ fetchMe() fails ──────► clearSession() → RootGate → LandingView   (Decision 5)

    URL "/budgets/:id/*" ──→ beforeEach (meta.requiresAuth) ──► AppLayout → RouterView → feature view
                                       └─ anonymous ──► /login          (unchanged)

### Shared visual surfaces

    PublicBackdrop ──┬──► PublicLayout  → card → RouterView (Login | Register | ForgotPassword | ResetPassword)
                     └──► LandingView   → Hero → Showcase(9) → CtaSection → LinkRow
                                                    │
    main.css @theme --color-brand-* ────────────────┴──► AppFooter (both shells)

### Deck export (manual, local, Windows)

    pnpm build-pptx ──► MyBudget.pptx ──► pnpm export-pptx-pdf
                                              │ execFileSync('powershell', [...])
                                              ▼
                                  PowerPoint COM: Open(readOnly) → SaveAs(out, 32) → Close → Quit
                                              │
                            exists && size > 0 ? MyBudget.pdf : exit 1 (keep .pptx link)

## File Changes

| File | Action | Description |
|---|---|---|
| `Project/frontend/src/layouts/RootGate.vue` | Create | Branches on `authStore.isAuthenticated`: `<LandingView />` or `<AppLayout><BudgetSelectionView /></AppLayout>` |
| `Project/frontend/src/features/landing/views/LandingView.vue` | Create | Container: backdrop + hero + showcase + CTA + link row |
| `Project/frontend/src/features/landing/components/{LandingHero,FlowShowcase,ShowcaseTile,LandingCta,LandingLinks}.vue` | Create | Presentational; `ShowcaseTile` owns the `aspect-[16/10]` frame, `<picture>` srcset, `loading="lazy"`, explicit `width`/`height` |
| `Project/frontend/src/features/landing/config/{links.ts,showcase.ts}` | Create | Outbound URLs; curated 9-item list (`dir`, `file`, `i18nKey`) |
| `Project/frontend/src/components/PublicBackdrop.vue` | Create | Slotted `fixed inset-0 -z-10` brand layer |
| `Project/frontend/src/components/AppFooter.vue` | Create | `© {year} · Powered by ARAS Systems`, plain text, no links |
| `Project/frontend/src/router/index.ts` | Modify | `/` → `RootGate` + `meta: { public: true }`, name kept `BudgetSelection`; budget subtree promoted to `/budgets/:budgetId` with `meta: { requiresAuth: true }`; guard gains the `needsAuth` computation and the `/`-only `clearSession()` branch |
| `Project/frontend/src/layouts/AppLayout.vue` | Modify | `<main><slot><RouterView /></slot></main>`, `flex flex-col` root, `<AppFooter />` after `<main>` |
| `Project/frontend/src/layouts/PublicLayout.vue` | Modify | Wrap in `<PublicBackdrop>`, append `<AppFooter />`; centered-card contract (`LAYOUT-2`) preserved |
| `Project/frontend/src/stores/auth.store.ts` | Modify | Export `clearSession()` (wraps `_clearTokens()`) |
| `Project/frontend/src/assets/main.css` | Modify | `@theme` brand palette + `:root` / `[data-theme='dark']` semantic vars |
| `Project/frontend/src/i18n/locales/{en,es}.json` | Modify | `landing.*` + `footer.*` in both locales |
| `Project/frontend/scripts/build-showcase.mjs` | Create | `sharp` resize/WebP of the 9 curated flow PNGs |
| `Project/frontend/scripts/export-pptx-pdf.mjs` | Create | PowerShell + COM `ppSaveAsPDF` with preflight and output verification |
| `Project/frontend/package.json` | Modify | `build-showcase`, `export-pptx-pdf` scripts |
| `Project/frontend/public/showcase/*.webp` | Create | Generated output (committed, like `diagrams/`) |
| `docs/slides/presentation/MyBudget.pdf` | Create | Generated deck artifact (only if the trial passes) |
| `README.md:285` | Modify | Point at the PDF (or keep `.pptx` and label it) |
| `openspec/specs/app-layout/spec.md` | Modify | Delta specs (owned by `sdd-spec`) |

## Interfaces / Contracts

```ts
// router/index.ts — the only auth-boundary change
const needsAuth =
  to.meta.requiresAuth === true ||
  (to.name === 'BudgetSelection' && authStore.isAuthenticated)
if (!needsAuth) return                       // anonymous "/" → landing
// ...existing body; on "/" only, a fetchMe() failure does:
//   authStore.clearSession(); return true   // → landing, not /login

// features/landing/config/showcase.ts
export interface ShowcaseItem {
  slug: string        // 'dashboard'            → public/showcase/dashboard-{640,1280}.webp
  source: string      // 'dashboard/01-lifetime-trend.png' under docs/slides/flows/
  i18nKey: string     // 'landing.showcase.dashboard'  (title + caption, EN + ES)
}
```

Default curation = one hero per flow directory (`auth`, `bank-accounts`, `budget-execution`, `budget-management`, `budget-structure-categories`, `budget-structure-cycles`, `budget-structure-periods-lines`, `current-situation`, `dashboard`). Exactly 9 tiles — if the `auth` shot has no product value at apply time, swap the source, do not change the count (the grid rhythm depends on it). Captions are i18n keys, **not** the English-only `.manifest.json` `title`/`description`.

## Testing Strategy

`strict_tdd: true` — RED test before each implementation step; this only affects sequencing in `sdd-tasks`.

| Layer | What to Test | Approach |
|---|---|---|
| Unit — router | Anonymous `/` renders landing and issues no redirect; authenticated `/` renders `BudgetSelectionView`; `forcePasswordChange` still wins at `/`; `fetchMe()` failure at `/` clears the session and renders the landing; `fetchMe()` failure at `/budgets/x/cycles` still redirects to `/login`; anonymous `/budgets/x/cycles` redirects to `/login`; deleted-budget redirect still lands on `/` | Vitest + memory router, `useAuthStore` stubbed |
| Unit — components | `RootGate` branch selection; `AppFooter` renders the current year in EN and ES; `PublicBackdrop` renders its slot; `ShowcaseTile` emits `<picture>` srcset + `loading="lazy"` + explicit dimensions; `LandingView` renders 9 tiles and the primary `/register` CTA | Vitest + `@testing-library/vue` |
| Unit — regression | `AppLayout` slot fallback still renders `<RouterView>` when no slot is passed; budget switcher stays hidden at `route.name === 'BudgetSelection'` | Existing `layouts/__tests__/AppLayout.spec.ts` |
| Unit — i18n | Every new `landing.*` / `footer.*` key exists in `en.json` and `es.json` | Existing `i18n/__tests__/locales.spec.ts` |
| E2E | Anonymous `/` shows the landing (no `/login` bounce, no authenticated API call in the network log); `LanguageSwitcher` works on the landing; signup CTA reaches `/register`; after login `/` shows budget selection; footer visible on a public and an authenticated page | Playwright, full Docker stack |
| Manual (gated) | PDF trial: export, then compare a screenshot-heavy slide, a diagram slide, and a text slide at 100% against the PPTX | One-off local Windows run, result recorded before the deck link is wired |

## Threat Matrix

Two boundaries change: the vue-router auth boundary at `/`, and a new PowerShell/COM subprocess.

| Boundary | Applicability | Design response | Planned RED tests |
|---|---|---|---|
| Documentation-like paths | **N/A** — no file-type classification or execution of repo content; the export script reads one hard-coded `.pptx` path | — | — |
| Git repository selection | **N/A** — no `git` invocation | — | — |
| Commit state | **N/A** — no index/worktree operation | — | — |
| Push state | **N/A** — no push or ref resolution | — | — |
| PR commands | **N/A** — no PR automation | — | — |
| **Routing — anonymous surface** (Applicable) | `/` is the only route losing `requiresAuth`. `LandingView` MUST issue no authenticated API call and render no user/budget data. `requiresAuth` moves onto the `/budgets/:budgetId` record, which every budget child inherits; `BudgetMatrix` keeps its redundant declaration | Anonymous `/budgets/:budgetId/{cycles,categories,lines,dashboard,bank-accounts,current-situation,matrix}` → `/login`; anonymous `/` → landing with zero `/api/*` requests (asserted in E2E network log) |
| **Subprocess — PowerPoint COM** (Applicable) | `execFileSync('powershell', [...])` with an **argv array**, no shell string interpolation; paths are module-relative constants, never user input. Preflight: `process.platform === 'win32'` and input `.pptx` exists, else exit non-zero with the `.pptx`-fallback instruction. PowerShell body wraps the COM calls so `$pres.Close()` / `$ppt.Quit()` always run, avoiding orphaned `POWERPNT.EXE`. Postflight: output exists and size > 0, else exit non-zero and leave the previous artifact untouched | Non-Windows platform exits non-zero with a clear message and writes nothing; missing input exits non-zero; a zero-byte/absent output is reported as failure rather than a silent success |

## Migration / Rollout

No data migration. Sequenced so the risky decision is settled first:

| PR | Scope | Verification | Est. lines |
|---|---|---|---|
| 0 | **PDF trial only** — `scripts/export-pptx-pdf.mjs` + platform/output guards + `package.json` script. Decide PDF vs `.pptx` before any landing link exists | Local Windows run + visual comparison; unit test for the non-Windows guard | ~120 |
| 1 | Router restructure + `RootGate` + `clearSession()` + `AppLayout` slot, with a placeholder landing stub | `pnpm test` + `pnpm build` | ~200 |
| 2 | Brand tokens + `PublicBackdrop` + `AppFooter` + both shell insertions + `footer.*` i18n | `pnpm test` | ~180 |
| 3 | `build-showcase.mjs` + 9 generated WebP assets + `showcase.ts`/`links.ts` config | `pnpm build-showcase` output review | ~150 (+ binaries) |
| 4 | Landing components, EN/ES copy, responsive layout, deck/README link wiring, E2E | `pnpm build` + Playwright | ~350 |

**400-line budget risk: Medium** — the total clears 400 lines, so PR 1 (the only behavioral-risk slice) must ship and be reviewed before the purely visual slices. Rollback: revert the branch; `/` returns to an auth-only route and the generated assets are deleted with no runtime dependency left behind.

## Open Questions

- [ ] None blocking. Two decisions are deferred **to evidence, not to discussion**: the PDF-vs-`.pptx` deck target is settled by the PR 0 trial, and the `auth` showcase slot may be re-sourced at apply time if the login screenshot carries no product value (count stays 9).
