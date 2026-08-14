# Proposal: User Guide Docs

## Intent

MyBudget ships with no end-user documentation. The README targets developers, and the slide deck targets the TFM tribunal — neither tells a real user how to run a budget cycle, invite a member, or read the dashboard. This change delivers a bilingual (EN default, ES alternate), navigable static HTML user guide, linked from the public landing page.

It is phase 1 of a 3-phase plan toward the TFM demo video (phase 2: collect narrative/architecture material; phase 3: script + recording), but it is a standalone deliverable: a functional reference a real user could follow, not just video prep.

## Scope

### In Scope
- **10 chapters**, 1:1 with the app's real nav structure: `auth`, `bank-accounts`, `budget-execution`, `budget-management`, `budget-structure-categories`, `budget-structure-cycles`, `budget-structure-periods-lines`, `current-situation`, `dashboard`, `members`
- **Bilingual**: each chapter authored in EN (`/guide/en/`) and ES (`/guide/es/`) — ~20 static `.html` files
- **Docs-site layout**: left sidebar chapter tree + right content pane; each chapter is its own real page with its own URL (deep-linkable, shareable, no iframe)
- **Placement**: `Project/frontend/public/guide/{en,es}/*.html` — Vite `public/` passthrough, ships with the existing `pnpm build`, zero Caddyfile/compose change (same pattern as `public/showcase/*.webp`)
- **Screenshots**: reuse the existing EN captures from `docs/slides/flows/<area>/*.png`, copied into `public/guide/assets/<area>/`, shared by both locales
- **`members` chapter**: text-only (no existing captures), scoped to administration + invite-sending — `BudgetMembersView.vue` (list, role change, remove, restore) and `InviteUserModal.vue`
- **Generalize `Project/frontend/scripts/render-diagrams.mjs`**: accept source/output path arguments and emit `.svg` (today: hardcoded single source, PNG output)
- **Landing page link**: locale-aware guide entry in `LandingLinks.vue` / `features/landing/config/links.ts`

### Out of Scope
- Replacing or restructuring the README (developer-facing, stays as is)
- Any new build step, SPA framework, or client-side JS for the guide content
- New screenshot captures or ES-locale captures — EN captures are reused verbatim
- Solving the duplicated-sidebar maintenance cost (accepted tradeoff; flag it in design, do not fix it with JS partials)
- Re-describing invite *acceptance* (already covered by `budget-management` slides #9–10)
- Caddyfile / `docker-compose.prod.yml` changes
- Phase 2/3 video work (narrative material, script, recording)

## Capabilities

### New Capabilities
- `user-guide`: bilingual static HTML user guide — chapter set, navigation model, hosting path, asset reuse, and locale parity rules

### Modified Capabilities
- `landing-page`: LANDING-4 (Secondary Outbound Links) gains a guide link whose URL is locale-aware

## Approach

Author plain static HTML per chapter. Each page carries a duplicated sidebar (`<nav>` of the 10 chapters, current one marked) and a content pane; a locale toggle links the sibling page in the other language. Chapter prose is rewritten from the existing terse `docs/slides/flows/**/index.md` captions plus source-code behavior for `members`. Images are referenced with paths relative to `/guide/`. `render-diagrams.mjs` gains CLI path args and SVG output so any future chapter diagram is a script invocation, not a rework.

### Explicit decision: the guide link IS locale-aware

`features/landing/config/links.ts` currently holds untranslated URL constants by design (prior change, design.md decision #11) — GitHub, README, and deck all resolve to a single URL regardless of locale. The guide breaks that: the link MUST resolve to `/guide/en/` or `/guide/es/` based on `useLocaleStore().locale`. This is an intentional, accepted deviation, not an oversight — the guide is the first landing-page target that genuinely has two localized artifacts. Record it as a scoped exception to the convention rather than silently reverting the convention itself.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Project/frontend/public/guide/{en,es}/*.html` | New | ~20 chapter pages |
| `Project/frontend/public/guide/assets/<area>/` | New | Copied EN screenshots |
| `Project/frontend/scripts/render-diagrams.mjs` | Modified | Path args + SVG output |
| `Project/frontend/src/features/landing/config/links.ts` | Modified | Locale-aware guide URL |
| `Project/frontend/src/features/landing/components/LandingLinks.vue` | Modified | Render the guide link |
| `Project/frontend/src/i18n/locales/{en,es}.json` | Modified | Guide link label |
| `docs/slides/flows/**/index.md` | Read-only | Factual source, unchanged |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| `members` chapter creeps into invite-acceptance (already illustrated elsewhere) | Medium | Chapter scope fixed at administration + invite-sending; cross-link `budget-management` for acceptance |
| Sidebar duplicated across ~20 files drifts out of sync | Medium | Author the sidebar once as a fixed block, copy verbatim; verification checks link parity across all pages |
| ES text drifts from EN as chapters are edited | Medium | Chapter-parity verification: same headings, same image set, both locales in the same task |
| Change exceeds the 800-line review budget | High | Auto-forecast in `sdd-tasks`; chained PRs sliced by chapter group |
| `render-diagrams.mjs` change breaks the existing deck pipeline | Low | Keep current defaults when no args are passed; re-run the deck render once |
| Screenshots are EN-only in the ES guide | Accepted | Explicit and user-confirmed; ES captions describe the EN UI |

## Rollback Plan

Fully additive except three small edits. Revert the merge commit(s): the `public/guide/` tree disappears from `dist/` on the next `pnpm build`, and `links.ts` / `LandingLinks.vue` / locale JSON return to the pre-change link row. `render-diagrams.mjs` reverts to its hardcoded form. No database migration, no infra change, no deploy-config change.

## Dependencies

- Existing EN screenshots in `docs/slides/flows/**/*.png` (89 files, present)
- `budget-member-administration` feature shipped 2026-08-13 — source of truth for the `members` chapter
- `useLocaleStore()` reactive locale (already available)

## Success Criteria

- [ ] 10 chapters exist in both `en` and `es` with matching headings and image sets
- [ ] Each chapter is reachable at its own URL and renders standalone (no iframe, no JS required)
- [ ] Every page's sidebar links to all 10 chapters with the current one marked
- [ ] Each page offers a working locale toggle to its sibling page
- [ ] `pnpm build` copies the whole guide into `dist/` with no Caddyfile or compose change
- [ ] Landing page shows a guide link that resolves to `/guide/en/` or `/guide/es/` per the active locale
- [ ] `render-diagrams.mjs` accepts source/output arguments and emits `.svg`, with the existing deck render still working
- [ ] The `members` chapter documents list, role change, remove, restore, and send-invite — and links out, rather than re-describing, invite acceptance
