# Exploration: user-guide-docs

Bilingual (EN default, ES alternate) static HTML user guide for MyBudget, linked from the
landing page. Phase 1 of a 3-phase plan toward the TFM demo video (phase 2: collect
narrative/architecture material for the video script; phase 3: script + recording).

## Current State

**Deploy pipeline**: `Project/Caddyfile` has one `handle {}` block serving `frontend/dist`
(bind-mounted read-only) via `root * /srv/frontend; try_files {path} /index.html; file_server`.
`docker-compose.prod.yml` mounts `./frontend/dist:/srv/frontend:ro` into `caddy` — nothing else
is served statically today.

**Vite `public/` passthrough already established**: `Project/frontend/public/` holds
`favicon.svg`, `icons.svg`, and `showcase/*.webp` (used by the landing page). Anything under
`frontend/public/` is copied verbatim into `dist/` at build time — zero-config precedent already
in production.

**Screenshot locale — confirmed EN-only**: `Project/frontend/e2e/screenshots/helpers.ts`
`seedOwnerAndLogin()` hardcodes `preferredLocale: 'en'` (line 26). No spec overrides it. All 89
screenshots in `docs/slides/flows/**/*.png` are EN.

**Landing page**: `LandingLinks.vue`
(`Project/frontend/src/features/landing/components/LandingLinks.vue`) is the existing
"secondary outbound links" row (GitHub/README/deck), sourced from plain untranslated string
consts in `Project/frontend/src/features/landing/config/links.ts` (design.md decision #11).
`AppFooter.vue` is explicitly documented "Plain text only — no outbound/internal links"
(LAYOUT-4) — not a valid location. `LanguageSwitcher.vue` exposes
`useLocaleStore().locale` (`'en'|'es'`) reactively — needed to build a locale-aware guide URL.

**`render-diagrams.mjs`** (`Project/frontend/scripts/render-diagrams.mjs`): hardcoded single
source (`docs/slides/presentation/flows.md`) and output dir, and **currently emits `.png`, not
`.svg`** — contradicts the confirmed "pre-rendered to SVG" decision. Needs generalizing (path
args + extension) before any guide diagram work.

**Members chapter**: confirmed no `docs/slides/flows/members/` folder exists. Real scope:
`Project/frontend/src/features/budget-structure/views/BudgetMembersView.vue`
(list/role-change/remove/restore, admin-gated), `.../components/BudgetTabs.vue` (Members tab
`v-if="isAdmin"`), `Project/frontend/src/components/budget/InviteUserModal.vue`
(send-invite, opened from `BudgetSelectionView.vue`), `.../composables/useRoleGate.ts`
(owner/admin/operator/read-only). Note: `docs/slides/flows/budget-management/index.md`
slides #9-10 already cover *accepting* an invite — the members chapter's net-new scope is
administration + sending, not re-describing acceptance.

**Content quality**: sampled `dashboard/index.md` and `budget-management/index.md` — terse
one-sentence captions per image, usable as a factual skeleton but needing light-to-moderate
rewriting into guide prose, not full authoring from scratch.

## Affected Areas

- `Project/Caddyfile`, `Project/docker-compose.prod.yml` — only touched if Option B (below) is
  chosen.
- `Project/frontend/public/` (or new `docs/guide/`) — guide source location decision.
- `Project/frontend/scripts/render-diagrams.mjs` — needs generalizing (path args, `.svg` output)
  if the guide has diagrams.
- `Project/frontend/src/features/landing/config/links.ts` + `LandingLinks.vue` — natural link
  insertion point, but breaks the existing "URLs aren't translated" convention.
- `Project/frontend/src/components/AppFooter.vue` — confirmed NOT a valid location (LAYOUT-4).
- `docs/slides/flows/**/index.md` (9 files) — reusable factual skeleton, needs prose rewriting.
- `docs/slides/flows/members/` — absent; chapter is text-only from source code.

## Approaches

### 1. Vite `public/` passthrough — `frontend/public/guide/{en,es}/*.html`

- Pros: zero Caddyfile/docker-compose changes; ships automatically with existing `pnpm build`;
  deep links resolve correctly (Caddy's `try_files {path}` serves the real file before falling
  back to `index.html`, no SPA-router conflict); reuses the proven `showcase/*.webp` precedent.
- Cons: guide source (HTML + reused PNGs) lives inside the `frontend/` package; images need a
  one-time copy (not symlink — fragile on Windows) from `docs/slides/flows/` into
  `frontend/public/guide/assets/`.
- Effort: Low.

### 2. Repo-root `docs/guide/` + new Caddy `handle_path` block + new bind mount

- Pros: keeps guide source alongside its natural sibling `docs/slides/`; no image duplication
  needed if referenced via relative path within `docs/`.
- Cons: first production Caddyfile/compose change since the `SITE_DOMAIN` fix; adds a moving
  part to the redeploy checklist with no staging environment to validate against first.
- Effort: Low-Medium.

## Recommendation

Option 1 (Vite `public/` passthrough) — reuses an already-proven zero-infra-touch pattern,
appropriate for a solo TFM deploy with no staging environment. The "growing the frontend
package" downside is cosmetic. Recommend copying the needed screenshot subset per chapter into
`frontend/public/guide/assets/<area>/` at authoring time.

## Risks

- `render-diagrams.mjs` emits PNG today, not SVG as decided — needs an explicit fix task, not an
  assumption.
- Guide URL must be locale-aware, breaking the landing page's "URLs aren't translated"
  convention (design.md #11) — needs an explicit call-out in proposal/design, not a silent
  deviation.
- Members chapter scope could creep into re-describing invite-acceptance, which is already
  illustrated in `budget-management` screenshots.
- Duplicated sidebar nav across ~20 static files (10 chapters × 2 locales) is accepted by the
  user, but maintenance cost should be flagged in design.

## Open Questions for Proposal

1. Guide placement: `frontend/public/guide/` (recommended) vs `docs/guide/` + new Caddy route.
2. Is generalizing `render-diagrams.mjs` (SVG output, path args) in-scope now, or deferred until
   a guide diagram is actually authored?
