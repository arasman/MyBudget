# Apply Progress: user-guide-docs (PR1 of 5, PR2 of 5, PR3 of 5, PR4 of 5, PR5 of 5 — FINAL)

## Status: PR1 DONE — implemented, tested (real), committed, reviewed. PR2 DONE — implemented, tested (real, 30/30), committed, user-confirmed. PR3 IMPLEMENTED, NOT COMMITTED (status at PR4 apply time — see PR3 section below for whether it has since been committed). PR4 IMPLEMENTED, NOT COMMITTED (per this session's launch context, PR1-PR4 are now committed on `feat/user-guide-docs-pr4`). PR5 (FINAL) IMPLEMENTED, NOT COMMITTED — see PR5 section at the bottom of this file.

Chain: `feat/user-guide-docs` (tracker) ← PR1 `feat/user-guide-docs-pr1` ← PR2
`feat/user-guide-docs-pr2` ← PR3 `feat/user-guide-docs-pr3` ← PR4 `feat/user-guide-docs-pr4`
(current) ← PR5.
PR1's scope (guide infra + `auth` pilot chapter) is committed and reviewed. PR2's scope
(`budget-management`, `budget-structure-cycles`, `budget-structure-categories`) is now committed on
`feat/user-guide-docs-pr2` and user-confirmed for real (30/30 `pnpm vitest`, `pnpm run build`
clean) — this section of the file previously described PR2 as uncommitted; that has since been
resolved outside this apply batch and is corrected here. PR3's scope
(`budget-structure-periods-lines`, `budget-execution`) was implemented and left uncommitted as of
its own apply batch, on `feat/user-guide-docs-pr3` (base `feat/user-guide-docs-pr2`). PR4's scope
(`bank-accounts`, `current-situation`, `dashboard`) is implemented in this batch, uncommitted, on
`feat/user-guide-docs-pr4` (base `feat/user-guide-docs-pr3`). PR5 tasks were not touched.

**Commits on `feat/user-guide-docs-pr1`:**
- `1e2d45b` — PR1 implementation (infra + `auth` chapter), 28 files, ~1185 authored lines
  (accepted as `size:exception` against the 800-line budget — user-approved, infra-heavy first PR).
- `87adfcb` — fixes for the 4 WARNING findings from the native `gentle-ai` 4R review (title dedup,
  shell-metacharacter guard in `render-diagrams.mjs`, atomic write in `guide:build`, wired
  `localeToggleHref` into production code paths). 7 files, +168/-14.

**Verification (real, not substitute):** the human user ran both commands directly in their own
terminal (the agent's Bash tool hits a Windows pnpm-store/symlink resolution issue — see
Infrastructure Blocker below, still present):
- `pnpm vitest run scripts/__tests__/build-guide.spec.ts scripts/__tests__/render-diagrams.spec.ts`
  → **30/30 passed** (post-fix; was 22/22 pre-fix).
- `pnpm run build` → **succeeded**, `dist/guide/**` present, no Caddyfile/compose touched.

**Native code review:** `gentle-ai review` ran full 4R (risk/resilience/readability/reliability,
tier `high`, 2623 changed lines) against commit `1e2d45b` — lineage `review-d8d9a16d3721d4ab`,
**approved** (all 4 findings WARNING-severity, none blocking), receipt validated at the
`pre-commit` gate (`allow`). Findings were then fixed in `87adfcb`. Formally re-reviewing that fix
via the native recovery mechanism would have re-run all 4 lenses against the *entire* PR1+fix diff
again (its `recover` operation re-diffs against the original base, not incrementally) — the user
judged that disproportionate for an already-tested, already-approved-once fix and explicitly chose
to skip it. **`87adfcb` therefore has no separate formal review receipt** — its correctness rests
on the real 30/30 test run + real build + the original 4R review of the code it's patching + user
sign-off. The abandoned recovery attempts (`review-user-guide-docs-pr1-fix`,
`review-user-guide-docs-pr1-fix2`) are quarantined in `.git/gentle-ai/review-transactions/quarantine/`,
harmless.

---

## PR1 — Guide Infra + Pilot Chapter (`auth`): IMPLEMENTED, NOT COMMITTED

### Branch: `feat/user-guide-docs-pr1` (base `feat/user-guide-docs`)

### Mode: Strict TDD (RED → GREEN → REFACTOR)

### Files created
| File | Lines | Notes |
|------|------:|-------|
| `Project/frontend/scripts/guide/chapters.mjs` | 82 | Manifest: `GUIDE_TITLE`, `LOCALES`, `OTHER_LOCALE`, `LOCALE_LABEL`, `UI_STRINGS`, `CHAPTERS` (10 entries, ADR-UGD-05 order). Only `auth.published = true`. `title` is optional and defaults to `label` (all 10 entries currently share the same text — kept the manifest DRY instead of duplicating every label as a literal `title` copy). |
| `Project/frontend/scripts/guide/template.html` | 28 | Page shell, all 11 `{{...}}` placeholders per design.md's exhaustive list. |
| `Project/frontend/scripts/guide/index-body.html` | 5 | Guide-home body fragment (`{{GUIDE_TITLE}}`, `{{INDEX_INTRO}}`, `{{CHAPTER_LIST}}`). |
| `Project/frontend/scripts/guide/content/en/auth.html` | 72 | Authored EN prose — register/login/forgot-password/reset-password/logout. |
| `Project/frontend/scripts/guide/content/es/auth.html` | 74 | Same headings/images, ES voseo register. |
| `Project/frontend/scripts/build-guide.mjs` | 335 | Generator + curated asset copier + validation (both directions) + `guide:check` regenerate-and-diff engine. |
| `Project/frontend/scripts/__tests__/build-guide.spec.ts` | 167 | Covers tasks 2.1–2.8 (13 test cases). |
| `Project/frontend/scripts/__tests__/render-diagrams.spec.ts` | 79 | Covers tasks 3.1–3.3 (7 test cases). |
| `Project/frontend/public/guide/assets/guide.css` | 243 | Hand-written, standalone (no Tailwind/DaisyUI), one `@media` breakpoint. |
| `Project/frontend/public/guide/{en,es}/{index,auth}.html` | 322 (generated) | Committed generated output — excluded from authored count per ADR-UGD-01 goldens accounting, reproducible via `pnpm guide:check`. |
| `Project/frontend/public/guide/assets/auth/*.png` (6 files) | — (copied binaries) | Curated subset of `docs/slides/flows/auth/`. |

### Files modified
| File | Diff | Notes |
|------|------|-------|
| `Project/frontend/scripts/render-diagrams.mjs` | +93/-22 | Exported `parseArgs`, `resolveOptions`, `buildMermaidArgv`; entry guard; SVG format support. Zero-arg behavior preserved exactly. |
| `Project/frontend/package.json` | +3/-1 | Added `guide:build` / `guide:check` scripts. **Not** wired into `build` (non-negotiable per ADR-UGD-01 — verified in the diff). |

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 2.1–2.8 | `build-guide.spec.ts` | Unit (pure fn) | N/A (new file; `build-guide.mjs` did not exist yet — import itself guaranteed RED) | ✅ Written first | ✅ 13/13 assertions pass (see Infra Blocker below for execution method) | ✅ Multiple cases per function (happy path + edge case: e.g. `../assets/` vs `../../assets/`) | ✅ Comments condensed, `title ?? label` dedup added post-green |
| 3.1–3.3 | `render-diagrams.spec.ts` | Unit (pure fn) | ✅ entry-guard-only prerequisite edit made first (structural, zero behavior change for CLI invocation — logged as "Triangulation skipped: purely structural") | ✅ Written first (exports didn't exist yet) | ✅ 7/7 assertions pass | ✅ png+svg cases, short+long flags, cwd resolution | ✅ Kept `buildMermaidArgv` ordering identical to original hardcoded argv |

### Test Summary
- **Total tests written**: 20 (13 in `build-guide.spec.ts`, 7 in `render-diagrams.spec.ts`)
- **Total tests passing**: 20/20 — verified via direct `node` execution of the spec files' exact assertions against the real module exports (see Infrastructure Blocker below for why `pnpm vitest` itself could not run)
- **Layers used**: Unit (20), Integration (0 — reserved for PR5's `guide-links.spec.ts` walker), E2E (0)
- **Approval tests**: None — no refactoring-of-existing-behavior tasks in this batch (render-diagrams.mjs's entry-guard change was structural-only, verified to preserve default argv byte-for-byte via the RED/GREEN test itself)
- **Pure functions created**: 8 (`escapeHtml`, `renderSidebar`, `localeToggleHref`, `fillTemplate`, `extractImgSrcs`, `validateAssetPath`, `validateManifest`, `validateLocaleParity`, `diffDirs` in `build-guide.mjs`; `parseArgs`, `resolveOptions`, `buildMermaidArgv` in `render-diagrams.mjs`)

### Work Unit Evidence (WU1)

| Evidence | Value |
|---|---|
| Focused test command and exact result | `pnpm vitest scripts/__tests__/build-guide.spec.ts scripts/__tests__/render-diagrams.spec.ts` — **could not execute** (pnpm/node_modules blocked). Substitute: ran the identical assertions via `node -e "import(...).then(...)"` against the real committed modules — 20/20 pass. |
| Runtime harness command/scenario and exact result | `node scripts/build-guide.mjs` → `build-guide: wrote 1 chapter(s) x 2 locale(s) + index pages.` — succeeded. `node scripts/build-guide.mjs --check` → `guide:check: clean — committed public/guide/** matches the manifest + fragments.` — succeeded, and its failure path was independently verified (tamper-inject → correctly reported `content differs: en/auth.html`, exit 1 → reverted → clean again). Manifest validation failure paths (missing source PNG, unlisted asset reference) also independently verified against the real repo tree. |
| Rollback boundary | Revert `Project/frontend/scripts/guide/**`, `Project/frontend/scripts/build-guide.mjs`, the `render-diagrams.mjs` diff, `Project/frontend/public/guide/**`, and the two `package.json` script lines. All isolated to this batch; nothing outside PR1's declared scope was touched. |

---

## Infrastructure Blocker: `pnpm`/`vitest`/`vite` unusable in this session

`node_modules` in this working copy has **pre-existing filesystem-level corruption**, unrelated to
this change's code:

- `fs.existsSync('node_modules/vitest/vitest.mjs')` → `false`, despite the real target file
  existing at its `.pnpm` store path.
- `fs.lstatSync()` on `node_modules/@tailwindcss/vite/node_modules` throws `UNKNOWN` with garbage
  metadata (`Blocks: 33554419`, 1600-era timestamps) — classic NTFS/MFT-corruption signature, not
  a permissions or antivirus-lock issue.
- Confirmed via: full `rm -rf node_modules` + fresh `pnpm install` (`node-linker=hoisted` and
  default symlink linker both tried, multiple retries) — the corruption reproduces deterministically
  on the exact same nested path (`@tailwindcss/vite/node_modules`) every time, meaning it is not
  transient/random.
- Result: `pnpm vitest run`, `pnpm build`, and even `pnpm install` itself all fail before doing any
  real work, with `[UNKNOWN] UNKNOWN: unknown error, ...`.
- This blocks **only** the pnpm/npm-dependent toolchain (vitest runner, `vue-tsc`, `vite build`).
  It does **not** block `scripts/build-guide.mjs` or `scripts/render-diagrams.mjs` themselves —
  both are pure Node ESM with zero npm dependencies, and were run and verified directly via `node`.
- **Not fixable from this session** (would need `chkdsk` / admin-level disk repair on the host).
- Recommendation for next session/reviewer: run `pnpm install` fresh (ideally after a host-level
  filesystem check) and then run `pnpm vitest run scripts/__tests__/build-guide.spec.ts
  scripts/__tests__/render-diagrams.spec.ts` and `pnpm build` to get first-party confirmation. Given
  the substitute verification performed here (exact same assertions, executed against the exact
  same committed modules, via plain `node`), a regression is considered unlikely but not
  100%-guaranteed absent a real `vitest`/`vite build` run.

### Manual gates status
- `pnpm guide:build` — ✅ done (via direct `node scripts/build-guide.mjs`)
- `pnpm guide:check` — ✅ clean (via direct `node scripts/build-guide.mjs --check`), both
  pass/fail directions independently verified
- `pnpm build` (confirms `dist/guide/` + no Caddyfile/compose touched) — ❌ **BLOCKED**, not run
- `pnpm render-diagrams` (no-args byte-identical to current deck) — ⚠️ **PARTIALLY VERIFIED**: the
  argv-level contract design.md names as the actual source of truth ("asserted by a unit test, not
  by re-reading the code") is confirmed; the real `npx`/mermaid-cli subprocess was not spawned.

---

## Deviations from Design / Tasks

1. **`guide.css` is 243 lines, not ~150.** A real accessible two-column responsive layout (sidebar
   + content + locale toggle + disabled-state styling + one breakpoint) came out larger than the
   design's estimate. Trimmed once already (originally 257) by merging duplicate selectors. Further
   compression would trade real readability for cosmetic line count.
2. **`build-guide.mjs` is 335 lines, not folded into the original ~520-total PR1 estimate.** It's a
   full generator + both-direction manifest validator + a real regenerate-and-diff (`guide:check`)
   engine — all three pieces are explicitly required by tasks.md/design.md, not scope creep.
3. **PR1 authored-line total is ~1,204** (package.json 4 + guide.css 243 + build-guide.spec.ts 167 +
   render-diagrams.spec.ts 79 + build-guide.mjs 335 + chapters.mjs 82 + auth en 72 + auth es 74 +
   index-body.html 5 + template.html 28 + render-diagrams.mjs diff 115), **against the confirmed
   800-line ceiling** (tasks.md's own project-specific budget, not the generic 400) and well above
   the ~520 estimate. Generated `public/guide/**` HTML (322 lines) and the 6 copied PNGs are
   correctly excluded as goldens per ADR-UGD-01. This is flagged as a risk for the user/orchestrator
   to decide on: accept as an infra-heavy first PR of a chain (`size:exception`), or split further
   (e.g. hold the `guide:check` diff-engine for a follow-up, or move the `auth` chapter's content to
   its own micro-PR). Not silently deviated — chapters.mjs was already compacted (124→82 lines,
   removing the redundant `title` field duplication) as a good-faith reduction before reporting.
4. **`chapters.mjs`'s `title` field made optional**, defaulting to `label` when absent — every one
   of the 10 chapters currently has identical `title`/`label` text, so a literal duplicate `title`
   key on all 10 entries was pure redundancy. `build-guide.mjs` reads `(chapter.title ?? chapter.label)[locale]`.
   A future chapter needing a longer `<title>`/`<h1>` than its short sidebar `label` can still set
   `title` explicitly.
5. **`auth` chapter content covers `/forgot-password` and `/reset-password`**, not just
   register/login as the launch prompt's phrasing suggested. These are real, live routes
   (`router/index.ts`) with real views (`ForgotPasswordView.vue`, `ResetPasswordView.vue`) — no
   screenshots exist for them (not in `docs/slides/flows/auth/`, which only has the 9
   register/login/logout captures), so they're documented as text-only sections within the chapter.
   This is a deliberate scope decision, not a hallucination of nonexistent app behavior — flagging
   since the launch prompt didn't explicitly ask for them.
6. **`localeToggleHref` is exported but not called by `buildChapterPage`/`buildIndexPage`.**
   `template.html` follows design.md's literal snippet (composing `../{{OTHER_LANG}}/{{FILENAME}}`
   inline) rather than a single derived href placeholder, to keep the template's placeholder set
   exactly matching design's "exhaustive" list. `localeToggleHref` stands as the canonical reference
   implementation of the same construction rule — task 2.2 explicitly asks for this convention to be
   unit-tested via a `build-guide.mjs` export, and PR5's `guide-links.spec.ts` integration walker
   (design.md Testing Strategy table) is the natural future consumer.

## Issues Found
- Caught and fixed during implementation (not left as a bug): `CHAPTER_TITLE`/`GUIDE_TITLE` values
  containing `&` (e.g. "Account & sign-in") were being interpolated into `<title>`/text nodes
  without HTML-escaping. Fixed by escaping all plain-text placeholder values (`GUIDE_TITLE`,
  `CHAPTER_TITLE`, `NAV_LABEL`, `SKIP_LABEL`, `BACK_TO_APP`, `OTHER_LANG_LABEL`, `INDEX_INTRO`)
  while leaving `SIDEBAR`/`BODY` unescaped (they carry pre-built/authored HTML by design).
  Re-verified after the fix; `public/guide/en/auth.html` now correctly shows
  `<title>Account &amp; sign-in · MyBudget User Guide</title>`.

## Remaining Tasks (PR3–PR5, not started, out of this batch's scope)
- PR3: `budget-structure-periods-lines`, `budget-execution`
- PR4: `bank-accounts`, `current-situation`, `dashboard`
- PR5: `members` chapter + `guide-links.spec.ts` integration walker + locale-aware landing link

## Status (PR1)
20/20 PR1 tasks complete. Committed (`1e2d45b`, then fixes in `87adfcb`), tested for real
(30/30 vitest, user-run), built for real (`pnpm run build` clean), and reviewed (native 4R,
approved). `render-diagrams.mjs`'s real `npx mermaid-cli` subprocess still hasn't been spawned
end-to-end (only its argv-construction is unit-tested) — worth a real `pnpm render-diagrams` run
whenever a guide chapter actually needs a diagram (none do yet).

---

## PR2 — Core Structure Chapters (`budget-management`, `budget-structure-cycles`,
`budget-structure-categories`): IMPLEMENTED, NOT COMMITTED

### Branch: `feat/user-guide-docs-pr2` (base `feat/user-guide-docs-pr1`)

### Mode: Content authoring against PR1's already-tested generator — no new production logic,
no TDD cycle required per skill guidance (structural/content task, single possible output per
manifest entry). PR1's existing `build-guide.spec.ts` suite (13 test cases covering
`renderSidebar`, `localeToggleHref`, `fillTemplate`, `validateManifest`, `validateAssetPath`,
`validateLocaleParity`) is the regression net this batch runs against — no test changes were
needed, confirming tasks.md's prediction that PR2 wouldn't touch the generator or its tests.

### Files created
| File | Lines | Notes |
|------|------:|-------|
| `scripts/guide/content/en/budget-management.html` | 78 | EN prose — budget list, create, rename/delete, invite-send + invite-accept (success/error) cross-covering the hard PR5 dependency. |
| `scripts/guide/content/es/budget-management.html` | 83 | Same headings/images, ES voseo register. |
| `scripts/guide/content/en/budget-structure-cycles.html` | 67 | EN prose — cycle list, create, edit, set-active, delete/restore. |
| `scripts/guide/content/es/budget-structure-cycles.html` | 71 | Same headings/images, ES voseo register. |
| `scripts/guide/content/en/budget-structure-categories.html` | 61 | EN prose — category tree, create group, create category, reorder/rename/delete/restore. |
| `scripts/guide/content/es/budget-structure-categories.html` | 62 | Same headings/images, ES voseo register. |
| `public/guide/{en,es}/{budget-management,budget-structure-cycles,budget-structure-categories}.html` | generated | Committed generated output — excluded from authored count per ADR-UGD-01 goldens accounting, reproducible via `pnpm guide:check`. |
| `public/guide/assets/{budget-management,budget-structure-cycles,budget-structure-categories}/*.png` (18 files, 6 per chapter) | — (copied binaries) | Curated subset per chapter; `budget-management` includes the two invite-acceptance captures (`09-invite-accept-success.png`, `10-invite-accept-error.png`) as the hard dependency PR5's `members` chapter will cross-link to. |

### Files modified
| File | Diff | Notes |
|------|------|-------|
| `scripts/guide/chapters.mjs` | +39/-3 | Flipped `published: true` for the 3 chapters, added their curated `images[]` (6 each, filenames verified against both `docs/slides/flows/<slug>/index.md` and the actual PNGs on disk before writing the manifest). |
| `public/guide/{en,es}/{auth,index}.html` | 6/6/12/12 lines | Regenerated sidebars now link the 3 newly-published chapters instead of rendering them as dimmed `<span class="disabled">` — expected/golden per tasks.md's Phase 4 note, verified via `git diff` to be sidebar-only (no other line changed in `auth.html`). |

### Curation decisions (ADR-UGD-06)
- `budget-management`: kept `01-budget-list`, `02-create-form`, `04-create-success`,
  `06-delete-success`, `09-invite-accept-success`, `10-invite-accept-error` (6 of 10). Skipped
  `03-create-duplicate-error`, `05-delete-confirm`, `07-show-deleted-toggle`,
  `08-restore-success` — duplicate-error/confirm-dialog/toggle-state variants covered in prose
  without a screenshot, per the curation principle already established by PR1's `auth` chapter.
- `budget-structure-cycles`: kept `01-list-empty`, `02-create-form`, `03-create-success`,
  `06-edit-success`, `07-set-active-success`, `09-delete-success` (6 of 9). Skipped
  `04-create-duplicate-error`, `05-edit-form`, `08-delete-confirm`.
- `budget-structure-categories`: kept `01-list-empty`, `02-create-group-form`,
  `03-create-group-success`, `05-create-category-form`, `06-create-category-success`,
  `10-restore-category-success` (6 of 10). Skipped `04-create-group-duplicate-error`,
  `07-create-category-duplicate-error`, `08-delete-category-confirm`,
  `09-delete-category-success` (restore-success alone tells the soft-delete/restore story
  without also needing the intermediate delete-success frame).

### Work Unit Evidence (WU2)

| Evidence | Value |
|---|---|
| Focused test command and exact result | `pnpm guide:check` — **could not execute via pnpm** (same environment blocker as PR1, see Infrastructure Blocker section below, reproduced again this session). Substitute: ran `node scripts/build-guide.mjs` then `node scripts/build-guide.mjs --check` directly — both succeeded (`build-guide: wrote 4 chapter(s) x 2 locale(s) + index pages.` / `guide:check: clean — committed public/guide/** matches the manifest + fragments.`, exit 0). Also ran PR1's exact `build-guide.spec.ts` assertions (`renderSidebar` single-`aria-current` + unpublished-no-`<a>`, `localeToggleHref`, `validateManifest`, `validateAssetPath`, `validateLocaleParity`) via direct `node -e "import(...)"` against the real committed `chapters.mjs` with the 3 new chapters published — all pass, 0 validation errors, 0 asset-path errors. |
| Runtime harness command/scenario and exact result | `node scripts/build-guide.mjs` produced all 6 new chapter pages (3 slugs × 2 locales) + regenerated `index.html`/`auth.html` sidebars + copied 18 curated PNGs into `public/guide/assets/**`. Manually verified: sidebar shows exactly one `aria-current="page"` per page and renders the 4 published chapters as `<a>` while the remaining 6 stay `<span class="disabled">`; `git diff` on `auth.html`/`index.html` confirms only the sidebar `<ol>` changed (3 lines flipped from disabled-span to anchor, both locales); heading-count and `<img>`-count parity confirmed EN==ES for all 3 new chapters (5/5, 6/6, 5/5 headings; 6/6, 6/6, 6/6 images). |
| Rollback boundary | Revert the 3 `chapters.mjs` manifest entries (restore `published: false`, drop `images[]`), delete the 6 new `content/{en,es}/*.html` fragments, delete `public/guide/assets/{budget-management,budget-structure-cycles,budget-structure-categories}/`, delete the 6 new `public/guide/{en,es}/*.html` pages, and regenerate `auth.html`/`index.html` (or revert their 4 diffs directly) to restore the PR1-only sidebar. All isolated to this batch; PR1's committed infra/generator/`auth` chapter untouched. |

### Deviations from Design / Tasks
1. **Authored total for this batch is ~464 lines** (422 across the 6 content fragments +
   `chapters.mjs`'s +39/-3 diff = 42 authored), above the design's ~290 estimate (~60% over,
   directionally consistent with PR1's own overrun — real guide-quality prose for 3 chapters with
   curated screenshots costs more than the estimate assumed). Still well inside the confirmed
   800-line per-PR ceiling (~58%), so no `size:exception` or further split is needed — flagged
   for visibility only, not as a blocking risk.
2. **Removed a planned cross-link from `budget-management` to `members.html`.** The first draft
   linked the invite-acceptance section to `<a href="members.html">`, but `members` is not
   published until PR5 and `build-guide.mjs` does not validate cross-chapter body hrefs (only
   `../assets/...` image paths and the generated sidebar are checked) — an unresolved link would
   have silently built clean but 404'd at runtime, violating the design's "no PR ships a 404"
   principle (seam #2), which the sidebar's `published` flag already protects but body prose does
   not automatically get. Changed to a plain-text mention ("The Members chapter covers how roles
   are managed...") in both locales instead of a hyperlink; PR5 can add the actual `<a>` once
   `members.html` exists. Not silently deviated — noting it here since design.md's ADR-UGD-08
   describes the reverse link (members → budget-management) but doesn't address this forward
   reference.
3. **Confirmed exact screenshot filenames from each chapter's `index.md` and the real files on
   disk before writing the manifest**, per the launch instructions — `09-invite-accept-success.png`
   / `10-invite-accept-error.png` for `budget-management` matched exactly as specified.

### Issues Found
None.

## Status (PR2)
9/9 PR2 tasks complete (6.1, 6.2, 7.1, 7.2, 8.1, 8.2, 9.1, 9.2, 9.3). Implemented, generator run
+ check verified via substitute direct-`node` execution (same pnpm/node_modules blocker as PR1 —
see Infrastructure Blocker below, reproduced again this session), **not yet committed** per
explicit instruction — all changes are uncommitted working-tree modifications on
`feat/user-guide-docs-pr2`, awaiting orchestrator/user review, the real `pnpm vitest run` / `pnpm
run build` confirmation, and explicit approval before `git add`/`commit`/`push`. Ready to move on
to PR3 once approved.

## PR 3 — Planning & Execution Chapters (`budget-structure-periods-lines`, `budget-execution`)

Branch `feat/user-guide-docs-pr3`, base `feat/user-guide-docs-pr2` (PR2 now committed/reviewed/
user-confirmed — see corrected status note above). Same content-only pattern as PR2: no generator
or test changes, `chapters.mjs` manifest diff + 4 new authored fragments (EN+ES × 2 chapters)
against PR1's already-tested infra.

### Where
- `Project/frontend/scripts/guide/chapters.mjs` — flipped `published: true` for
  `budget-structure-periods-lines` and `budget-execution`; added curated `images[]` (6 each,
  filenames verified against `docs/slides/flows/<slug>/index.md` and the real files on disk).
- `Project/frontend/scripts/guide/content/{en,es}/budget-structure-periods-lines.html` — 6
  headings each locale (Periods within a cycle; Status, deletion, and restoring a period; Budget
  lines; Editing a budget line; Adjusting the budgeted amount over time; Removing a budget line),
  grounded in `CycleDetailView.vue` (periods, `canWriteStructure` gate, `PERIOD_NAME_DUPLICATE`/
  `PERIOD_OUT_OF_CYCLE_RANGE`/`PERIOD_DATE_OVERLAP`, restore-cascade disclosure), `BudgetLinesView.vue`
  + `BudgetLineModal.vue` (lines, `canWriteLines` gate, modal + inline "+ create" row, read-only
  fields once a line exists, `BUDGET_LINE_NAME_DUPLICATE`), and `BudgetLineCustomizationsView.vue`
  (dated revisions, admin-only, `CANNOT_DELETE_ORIGINAL_REVISION`/`REVISION_HAS_ACTIVE_EXECUTIONS`
  constraints, text-only — no capture exists for revisions in the flow's screenshot set).
- `Project/frontend/scripts/guide/content/{en,es}/budget-execution.html` — 5 headings each locale
  (Reading the matrix; Recording an execution; Editing, deleting, and restoring entries; Switching
  the display currency; Collapsing groups for a denser view), grounded in `BudgetMatrixView.vue`
  (group→category→line nesting, Budgeted/Executed/Difference columns from `budgetMatrix.columns`
  i18n keys, period prev/next nav), `MatrixCell.vue` (double-click opens the execution modal),
  `ExecutionRecordForm.vue` (entry types Expense/CreditNote/DebitNote, note always required —
  `noteRequiredAlways` validation), `ExecutionRecordRow.vue` (two-step delete confirm, restore
  still available post period-close for operators+), `MatrixControls.vue` (GTQ/USD currency
  toggle, exchange-rate input locked when every visible period is closed), and
  `MatrixGroupRow.vue`/`MatrixCategoryRow.vue` (independent group/category collapse).
- `Project/frontend/public/guide/{en,es}/{budget-structure-periods-lines,budget-execution}.html`
  + `public/guide/assets/{budget-structure-periods-lines,budget-execution}/*.png` (12 files) —
  generated/copied via `node scripts/build-guide.mjs`, excluded from authored count as goldens.
- `Project/frontend/public/guide/{en,es}/{auth,budget-management,budget-structure-cycles,
  budget-structure-categories,index}.html` — regenerated sidebars only (both new chapters flipped
  from `<span class="disabled">` to `<a href="...">`); confirmed via `git diff` on `auth.html`
  (+4/-4, sidebar `<ol>` only, no other content changed).
- `openspec/changes/user-guide-docs/tasks.md` — PR3's 4 tasks (10.1, 10.2, 11.1, 11.2) marked
  `[x]`. Tasks 12.1/12.2 (regenerate + `guide:check` gate) are covered by the verification below
  but left unchecked on disk pending the user's own confirmation run, matching PR1/PR2's pattern of
  reserving the final manual-gate checkbox for the human-run command.
- `openspec/changes/user-guide-docs/apply-progress.md` (this file) — corrected the stale PR2
  "NOT COMMITTED" status (PR2 was committed/verified outside this apply batch, confirmed by the
  launch context) and appended this PR3 section; PR1/PR2 sections kept intact above.

### Curation decisions
- `budget-structure-periods-lines` (16 source captures, kept 6): `01-period-list-empty`,
  `03-period-create-success`, `06-period-status-success`, `11-line-create-success`,
  `13-line-edit-inline`, `16-line-delete-success`. Skipped both duplicate-name-error captures
  (`04-period-create-duplicate-error`, `12-line-create-duplicate-error` — described in prose
  instead, following PR2's precedent of not needing an image for every validation rule),
  `02-period-create-form`/`10-line-create-form` (the *-success frame already shows the filled
  state's result), `05-period-status-form`, `07/08-period-delete-confirm/success` and
  `09-line-list-empty`/`14-line-edit-success`/`15-line-delete-confirm` (redundant with the kept
  frames). Prioritized `13-line-edit-inline` specifically because inline-edit-without-a-modal is
  the one interaction pattern in this chapter that differs from every prior chapter's
  pencil-icon-opens-modal pattern and is worth showing, not just describing.
- `budget-execution` (11 source captures, kept 6 — largest source set of any chapter per
  design.md, curated hardest here): `01-matrix-view`, `03-create-validation-error`,
  `04-create-form-filled`, `06-matrix-updated`, `07-currency-toggle-usd`, `09-collapse-group`.
  Skipped `02-open-execution-modal` (a mostly-empty modal frame, less informative than the
  validation-error frame that immediately follows it), `05-create-success` (redundant with
  `06-matrix-updated`, which shows the same result already reflected in the matrix — the more
  useful of the two), `08-currency-toggle-gtq` (near-duplicate of `07`, "toggle back" tells no new
  story), and `10-delete-confirm`/`11-delete-success` (the two-step delete UX is fully covered in
  prose; every other new-vs-skipped tradeoff in this chapter favored a novel interaction over a
  standard CRUD step, and delete here is the same soft-delete-behind-confirm pattern already shown
  with images in three earlier chapters).

### Work Unit Evidence (WU3)

| Evidence | Value |
|---|---|
| Focused test command and exact result | `pnpm guide:check` — **could not execute via pnpm** (same environment blocker as PR1/PR2, see Infrastructure Blocker section below, reproduced again this session). Substitute: ran `node scripts/build-guide.mjs` then `node scripts/build-guide.mjs --check` directly from `Project/frontend` — both succeeded (`build-guide: wrote 6 chapter(s) x 2 locale(s) + index pages.` / `guide:check: clean — committed public/guide/** matches the manifest + fragments.`, exit 0, 0 validation errors). The real generator run internally exercises `validateManifest`/`validateAssetPath`/`validateLocaleParity` against the actual new `chapters.mjs` entries and fragments (not fixtures), which is the load-bearing check for a content-only batch — same evidence bar PR2 used. |
| Runtime harness command/scenario and exact result | `node scripts/build-guide.mjs` produced the 4 new chapter pages (2 slugs × 2 locales) + regenerated `index.html`/`auth.html`/`budget-management.html`/`budget-structure-cycles.html`/`budget-structure-categories.html` sidebars + copied 12 curated PNGs into `public/guide/assets/**`. Verified via `git diff --stat` + `git diff` on `auth.html`: only the sidebar `<ol>` changed (+4/-4, both new chapters flipped from disabled-span to anchor, both locales) — no other content touched. Manually confirmed every `images[]` entry for both new chapters has a matching PNG under `docs/slides/flows/<slug>/` and every `../assets/...` reference in the authored fragments is listed in that chapter's `images[]` (the generator's own validation pass, which ran clean, is the authoritative check here). EN/ES parity manually confirmed via `grep -c`: `budget-structure-periods-lines` 6/6 `<h2>`, 6/6 `<img>`; `budget-execution` 5/5 `<h2>`, 6/6 `<img>`. |
| Rollback boundary | Revert the 2 `chapters.mjs` manifest entries (restore `published: false`, drop `images[]`), delete the 4 new `content/{en,es}/*.html` fragments, delete `public/guide/assets/{budget-structure-periods-lines,budget-execution}/`, delete the 4 new `public/guide/{en,es}/*.html` pages, and regenerate (or revert directly) the 5 previously-published pages' sidebars to restore the PR1+PR2-only state. All isolated to this batch; PR1's committed infra/generator and PR2's committed 3 chapters are untouched. |

### Deviations from Design / Tasks
None — implementation matches design.md's ADR-UGD-06 curation approach and PR3's task scope in
tasks.md exactly. Task 12.1/12.2 (the two manual-gate checkboxes) are intentionally left `[ ]` on
disk pending the human-run `pnpm guide:build`/`pnpm guide:check`, matching how PR1/PR2 reserved
their equivalent manual gates.

### Issues Found
None. `pnpm`/`node_modules` remains blocked in this session by the same pre-existing Windows
filesystem-level corruption documented in PR1's Infrastructure Blocker section — reproduced again,
not caused by this PR's code.

## Status (PR3)
4/4 PR3 authoring tasks complete (10.1, 10.2, 11.1, 11.2). Generator run + check verified via
substitute direct-`node` execution (same pnpm/node_modules blocker as PR1/PR2). Left uncommitted
per explicit instruction — all PR3 changes are working-tree modifications on
`feat/user-guide-docs-pr3`, awaiting orchestrator/user review, the real `pnpm vitest run`/`pnpm run
build` confirmation, and explicit approval before `git add`/`commit`/`push`. Ready to move on to
PR4 once approved.

## PR 4 — Reporting Chapters (`bank-accounts`, `current-situation`, `dashboard`)

Branch `feat/user-guide-docs-pr4`, base `feat/user-guide-docs-pr3`. Same content-only pattern as
PR2/PR3: no generator or test changes, `chapters.mjs` manifest diff + 6 new authored fragments
(EN+ES × 3 chapters) against PR1's already-tested infra.

### Mode: Standard (content-only chapters against an already-tested generator require no new
production logic and no new TDD cycle — same precedent as PR2/PR3).

### Where
- `Project/frontend/scripts/guide/chapters.mjs` — flipped `published: true` for `bank-accounts`,
  `current-situation`, and `dashboard`; added curated `images[]` (6 each, filenames verified
  against `docs/slides/flows/<slug>/index.md` and the real files on disk before writing the
  manifest). Diff: +39/-3 (42 authored lines).
- `Project/frontend/scripts/guide/content/{en,es}/bank-accounts.html` — 4 headings each locale
  (The account list; Creating an account; Editing an account; Deleting and restoring an account),
  grounded in `BankAccountListView.vue` (admin-only create/edit/delete/restore via `useRoleGate`,
  Adds/Subtracts type badge, currency locked once created, `ALIAS_DUPLICATE` dedicated error,
  soft-delete + "Show deleted" toggle + restore).
- `Project/frontend/scripts/guide/content/{en,es}/current-situation.html` — 5 headings each locale
  (Navigating between cut dates; Entering balances and the exchange rate; Saving a cut; Reading
  the totals and execution summary; Deleting a cut record), grounded in `CurrentSituationView.vue`
  (`CutDateNavigator`, Draft badge, `LoadStrategyModal`'s blank/clone/from-date seeding options,
  operator-gated save, `noActivePeriod` 422 rejection, `CutTotalsPanel`'s totals + execution
  summary + snapshot-notice semantics, `DeleteCutModal`'s type-exact-date-to-confirm pattern).
- `Project/frontend/scripts/guide/content/{en,es}/dashboard.html` — 6 headings each locale
  (Lifetime trend; Average behavior band; Budget line behavior — within a cycle; Budget line
  behavior — across cycles; Currency mismatch guard; Responsive layout), grounded in
  `DashboardView.vue` (3 self-contained sections, no tabs), `LifetimeTotalsChart.vue`/
  `TotalsBandChart.vue`/`BudgetLineSeriesChart.vue`, `ComparisonModeSwitch.vue` (within-cycle:
  1 Cycle + 2+ Periods; cross-cycle: 2+ Cycles, mode switch clears the other mode's selection),
  `InsufficientDataState.vue` (band needs 2+ periods), `CurrencyMismatchWarning.vue` (mismatched
  Cycle currencies block the chart entirely), and the `conversionBasis` i18n distinction
  (`cut-frozen` for lifetime/band vs `transaction-time` for the line-series chart — a real,
  non-obvious nuance worth documenting since the two charts can show slightly different converted
  totals for the same data).
- `Project/frontend/public/guide/{en,es}/{bank-accounts,current-situation,dashboard}.html` +
  `public/guide/assets/{bank-accounts,current-situation,dashboard}/*.png` (18 files, 6 per
  chapter) — generated/copied via `node scripts/build-guide.mjs`, excluded from authored count as
  goldens.
- `Project/frontend/public/guide/{en,es}/{auth,budget-management,budget-structure-cycles,
  budget-structure-categories,budget-structure-periods-lines,budget-execution,index}.html` —
  regenerated sidebars only (all three new chapters flipped from `<span class="disabled">` to
  `<a href="...">`); confirmed via `git diff` on `auth.html` (+6/-6, sidebar `<ol>` only, no other
  content changed — verified diff shows exactly the 3 chapters flipped from disabled-span to
  anchor in both EN and ES).
- `openspec/changes/user-guide-docs/tasks.md` — PR4's 8 tasks (13.1, 13.2, 14.1, 14.2, 15.1, 15.2,
  16.1, 16.2) marked `[x]`. 16.1/16.2 (regenerate + `guide:check` gate) were run and verified via
  the same substitute direct-`node` evidence PR2/PR3 used, then marked complete (following PR2's
  precedent of marking these `[x]` with the substitute-evidence caveat noted, rather than PR3's
  stricter reservation of the equivalent checkboxes) — the human user's own `pnpm` run remains the
  first-party confirmation.
- `openspec/changes/user-guide-docs/apply-progress.md` (this file) — appended this PR4 section;
  PR1/PR2/PR3 sections kept intact above.

### Curation decisions
- `bank-accounts` (10 source captures, kept 6): `01-list-empty`, `02-create-form`,
  `03-create-success`, `06-edit-success`, `08-delete-success`, `10-restore-success`. Skipped
  `04-create-duplicate-error` (described in prose instead, following PR2/PR3's precedent of not
  needing an image for every validation rule), `05-edit-form` (the edit-success frame already
  shows the result), `07-delete-confirm` (a bare confirmation dialog, less informative than the
  delete-success frame that follows it), and `09-show-deleted-toggle` (the toggle state is
  describable in prose; the restore-success frame that follows it tells the more complete story —
  same tradeoff PR2's `budget-structure-categories` made by skipping its own toggle-adjacent
  frame).
- `current-situation` (7 source captures, kept 6): `01-draft-form`, `02-form-filled`,
  `03-save-error`, `04-save-success`, `06-delete-confirm-typed`, `07-delete-success`. Skipped only
  `05-delete-confirm-empty` — a near-duplicate of `06-delete-confirm-typed` differing solely in a
  disabled-vs-enabled button state, and the enabled/typed frame is the more instructive of the
  pair (mirrors PR3's `budget-execution` reasoning for skipping `02-open-execution-modal` in favor
  of the more informative frame that follows it). `03-save-error` was kept deliberately — an
  actual 422 validation error grounded in real backend behavior (`noActivePeriod`), not a
  generic/uninstructive error state, so it clears the "genuinely instructive" bar ADR-UGD-06 sets
  for keeping an error-state capture.
- `dashboard` (7 source captures, kept 6 — one per distinct chart mode plus the responsive check,
  per the design.md density lesson from the slides deck build): `01-lifetime-trend`,
  `03-budget-line-empty`, `04-budget-line-selected`, `05-cross-cycle-mode`,
  `06-insufficient-history`, `07-mobile-viewport`. Skipped only `02-series-picker-empty` — the
  `SeriesPicker` component it demonstrates is shared by both the lifetime and band charts and its
  "clearing selection empties the chart" behavior is already implied by `03-budget-line-empty`'s
  parallel empty state on the line-series chart; keeping it would have meant a 7th image purely to
  re-demonstrate a picker-driven empty state already shown once. This is the only PR4 chapter
  whose curated set intentionally maps 1:1 onto "distinct interaction modes" (lifetime trend, band
  with its insufficient-history guard, budget-line within-cycle empty+selected, budget-line
  cross-cycle, and the responsive check) rather than a purely CRUD-lifecycle narrative, per the
  design.md launch-prompt instruction to curate the dashboard around its chart modes specifically.

### Work Unit Evidence (WU4)

| Evidence | Value |
|---|---|
| Focused test command and exact result | `pnpm guide:check` — **could not execute via pnpm** (same environment blocker as PR1/PR2/PR3, see Infrastructure Blocker section above, reproduced again this session). Substitute: ran `node scripts/build-guide.mjs` then `node scripts/build-guide.mjs --check` directly from `Project/frontend` — both succeeded (`build-guide: wrote 9 chapter(s) x 2 locale(s) + index pages.` / `guide:check: clean — committed public/guide/** matches the manifest + fragments.`, exit 0, 0 validation errors). The real generator run internally exercises `validateManifest`/`validateAssetPath`/`validateLocaleParity` against the actual new `chapters.mjs` entries and fragments (not fixtures), which is the load-bearing check for a content-only batch — same evidence bar PR2/PR3 used. |
| Runtime harness command/scenario and exact result | `node scripts/build-guide.mjs` produced the 6 new chapter pages (3 slugs × 2 locales) + regenerated `index.html`/`auth.html`/`budget-management.html`/`budget-structure-cycles.html`/`budget-structure-categories.html`/`budget-structure-periods-lines.html`/`budget-execution.html` sidebars + copied 18 curated PNGs into `public/guide/assets/**`. Verified via `git status --short` + `git diff` on `auth.html`: only the sidebar `<ol>` changed (+6/-6, all three new chapters flipped from disabled-span to anchor, both locales) — no other content touched. Manually confirmed every `images[]` entry for the 3 new chapters has a matching PNG under `docs/slides/flows/<slug>/` (verified via direct `ls` on each new `public/guide/assets/<slug>/` directory) and every `../assets/...` reference in the authored fragments is listed in that chapter's `images[]` (the generator's own validation pass, which ran clean, is the authoritative check here). EN/ES parity confirmed via `grep -c`: `bank-accounts` 4/4 `<h2>`, 6/6 `<img>`; `current-situation` 5/5 `<h2>`, 6/6 `<img>`; `dashboard` 6/6 `<h2>`, 6/6 `<img>`. |
| Rollback boundary | Revert the 3 `chapters.mjs` manifest entries (restore `published: false`, drop `images[]`), delete the 6 new `content/{en,es}/*.html` fragments, delete `public/guide/assets/{bank-accounts,current-situation,dashboard}/`, delete the 6 new `public/guide/{en,es}/*.html` pages, and regenerate (or revert directly) the 7 previously-published pages' + index's sidebars to restore the PR1+PR2+PR3-only state. All isolated to this batch; PR1's committed infra/generator, PR2's committed 3 chapters, and PR3's chapters are untouched. |

### Deviations from Design / Tasks
1. **Authored total for this batch is ~502 lines** (460 across the 6 content fragments +
   `chapters.mjs`'s +39/-3 diff = 42 authored), above the design's ~280 estimate (~79% over,
   directionally consistent with PR2's ~60% and PR1's larger overrun — real guide-quality prose
   for 3 chapters with curated screenshots, including the dashboard's extra "why do the two charts
   show different totals" nuance paragraph, costs more than the estimate assumed). Still well
   inside the confirmed 800-line per-PR ceiling (~63%), so no `size:exception` or further split is
   needed — flagged for visibility only, not as a blocking risk.
2. **`dashboard.html` documents the `cut-frozen` vs `transaction-time` conversion-basis
   distinction between the lifetime/band charts and the budget-line chart**, sourced from the
   `conversion-basis` prop passed to `BaseChart` in each chart component and the corresponding
   `dashboard.conversionBasis.*` i18n keys. This wasn't explicitly requested in tasks.md/design.md
   but is a real, non-obvious behavior a reader would otherwise find confusing (two dashboard
   charts disagreeing on a converted total for the same underlying data) — flagging as a
   deliberate, grounded addition, not scope creep or invention.
3. **No cross-links were added between the three new chapters and `members`** (not yet published)
   or between `current-situation` and `dashboard` (the natural "cuts feed the dashboard" relation
   is mentioned in dashboard's intro paragraph in prose only, not as an `<a href>`), following
   PR2's established rule: `build-guide.mjs` does not validate cross-chapter body hrefs, so an
   unresolved link could build clean but 404 at runtime. All chapter cross-references in this
   batch stay as plain-text mentions.

### Issues Found
None. `pnpm`/`node_modules` remains blocked in this session by the same pre-existing Windows
filesystem-level corruption documented in PR1's Infrastructure Blocker section — reproduced again,
not caused by this PR's code.

## Status (PR4)
8/8 PR4 tasks complete (13.1, 13.2, 14.1, 14.2, 15.1, 15.2, 16.1, 16.2). Generator run + check
verified via substitute direct-`node` execution (same pnpm/node_modules blocker as PR1/PR2/PR3).
Left uncommitted per explicit instruction — all PR4 changes are working-tree modifications on
`feat/user-guide-docs-pr4`, awaiting orchestrator/user review, the real `pnpm vitest run`/`pnpm run
build` confirmation, and explicit approval before `git add`/`commit`/`push`. Ready to move on to
PR5 once approved. Per launch-prompt instructions, PR5 (`members` chapter + landing-page link
integration touching `src/`) was intentionally NOT started in this batch.

## Cumulative task status (all PRs)
46/67 tasks complete as of PR4 (PR1: 25/27; PR2: 9/9; PR3: 4/4 authoring tasks (12.1/12.2 manual
gates pending); PR4: 8/8). PR5 (17 tasks) not started as of PR4. See the PR5 section below for
this batch's result: **61/67 cumulative** (15/17 of PR5's tasks complete; 20.3/20.4 blocked on the
human's real `pnpm build`/`pnpm lint && pnpm test`).

---

## PR 5 — Members Chapter + Landing Integration (FINAL): IMPLEMENTED, NOT COMMITTED

### Branch: `feat/user-guide-docs-pr5` (base `feat/user-guide-docs-pr4`, which per this session's
launch context has PR1-PR4 committed and previously user-confirmed: 30/30 vitest, `pnpm build`
clean).

### Mode: Mixed. Part A (`members` chapter content) is standard mode, same content-only pattern as
PR2/PR3/PR4 — no new production logic, no TDD cycle. Parts C and D (locale-aware landing link,
`guide-links.spec.ts` integration walker) are **Strict TDD** (RED → GREEN → REFACTOR) per this
session's launch instructions, since they are the only parts of the entire `user-guide-docs`
change (besides PR1) that touch real application `src/` and test infrastructure.

### Part A — `members` chapter (content-only, no TDD)

#### Where
- `Project/frontend/scripts/guide/chapters.mjs` — flipped `members.published` from `false` to
  `true`. No `images` key (text-only chapter, per ADR-UGD-06 — the generator's own validation
  treats a missing `images` key as valid, confirmed again by this batch's safety-net run).
- `Project/frontend/scripts/guide/content/{en,es}/members.html` — 6 sections each locale (Who can
  manage members; Viewing the member list; Changing a member's role; Removing access; Restoring
  access; Inviting someone), grounded in:
  - `src/features/budget-structure/views/BudgetMembersView.vue` — `canActOn()` gate (transcribed
    verbatim into prose: admin/owner AND not your own row AND not the owner's row AND — unless
    you're the owner — not another admin's row; the same gate covers both the role-change control
    and Remove/Restore), `visibleMembers` (owner row filtered out entirely), the `showDeleted`
    toggle, and the remove-confirmation dialog copy.
  - `src/features/budget-structure/composables/useRoleGate.ts` — `isAdmin`/`isOwner` definitions.
  - `src/features/budget-structure/components/BudgetTabs.vue` — confirmed the Members tab itself
    is `v-if="isAdmin"`, i.e. invisible in the tab bar (not just inert) for non-admins.
  - **Non-obvious discovery, corrected from the launch prompt's assumption**: `InviteUserModal` is
    NOT opened from `BudgetMembersView.vue`. It is opened from
    `src/features/budget-structure/views/BudgetSelectionView.vue` — the budget *selection* screen,
    via a "UserPlus" icon button next to each budget's name, gated by that view's own `canEdit(m)`
    (`m.role === 'owner' || m.role === 'admin'`). Section 6 ("Inviting someone") documents this
    correctly — inviting happens from the budget list, not from inside the Members tab — rather
    than inventing an invite button on `BudgetMembersView.vue` that does not exist. Confirmed via
    `i18n/locales/en.json`'s real key text (`budgetStructure.selection.inviteUser`,
    `invitation.modal.*`) matching the actual component wiring.
  - Section 6 stops at "the invitation is sent" per UG-6/ADR-UGD-08's scope fence; the last
    paragraph in both locales cross-links to the now-published `budget-management` chapter for
    invite *acceptance*.
  - Zero `<img>` elements in either locale (UG-6 requirement) — confirmed by the integration
    walker (Part D) and by direct `node` inspection.
- `Project/frontend/scripts/guide/content/{en,es}/budget-management.html` — small forward-reference
  fix: PR2 had deliberately left "The Members chapter covers how roles are managed..." as
  plain text (not a real `<a>`) because `members.html` didn't exist yet and an unresolved link
  would have built clean but 404'd at runtime (PR2's own documented deviation #2). Now that
  `members` is published, converted that sentence to a real `<a href="members.html">` in both
  locales — closing the loop PR2 explicitly left open for this PR.
- `Project/frontend/public/guide/{en,es}/members.html` + all 9 previously-published chapter
  pages' + `index.html`'s sidebars — regenerated via `node scripts/build-guide.mjs` (all 10
  chapters × 2 locales now published; sidebars go from 9-published+1-disabled to 10-published,
  0-disabled everywhere).

#### Deviation from the launch prompt worth flagging
The launch prompt's own description of section 6 ("Inviting someone") implicitly assumed the
invite flow lives inside the Members tab. Real source reading found it lives on the budget
*selection* screen instead (`BudgetSelectionView.vue`, not `BudgetMembersView.vue`). The chapter
documents the real wiring, not the assumed one — flagging per this skill's "if the design is
wrong or incomplete, note it" rule, since ADR-UGD-08's table cites `InviteUserModal.vue` as a
"source of truth" without naming which view opens it, and the launch prompt's phrasing read as
if it were opened from within the Members tab.

### Part B — `render-diagrams.mjs` end-to-end (deferred, per instructions)
Not exercised — per the launch prompt, this remains low-priority since no chapter across all 10
needs a diagram. Still true after `members` (text-only, no diagram). Unchanged from PR1's status:
argv-construction is unit-tested; the real `npx`/mermaid-cli subprocess has never been spawned
end-to-end in this session (same pnpm blocker as everything else that needs `npx`).

### Part C — Locale-aware landing link: Strict TDD

#### RED
Extended `Project/frontend/src/features/landing/__tests__/LandingView.spec.ts`:
- Added a new test, `'guide link resolves per active locale and updates without a page reload
  (LANDING-4)'`, asserting `[data-testid="link-guide"]`'s `href` is `/guide/en/` by default, is
  not styled `btn-primary`, and updates to `/guide/es/` after calling `useLocaleStore().setLocale('es')`
  — with no re-render/navigation trigger, just `await nextTick()`.
- **Non-trivial RED fix required in the test's own mock, not just a new test case.** The file's
  existing `vi.mock('@/stores/locale.store', ...)` returned a plain object
  `{ locale: 'en', setLocale: vi.fn() }`. Pinia's real `storeToRefs()` only wraps store
  properties that are already `isRef`/`isReactive` — a plain string property is silently *skipped*,
  so `const { locale } = storeToRefs(useLocaleStore())` in `LandingLinks.vue` would have destructured
  to `undefined` against the old mock, regardless of whether the component code was correct. Rewrote
  the mock to a singleton store object built with a real `ref<'en'|'es'>('en')`, with `setLocale`
  mutating that ref — matching how a real per-app Pinia store instance is shared by every component
  that calls `useLocaleStore()`. Added a `beforeEach` reset (`useLocaleStore().setLocale('en')`)
  since the ref's mutated value — unlike `vi.fn()` call-tracking — is not cleared by
  `vi.clearAllMocks()` and would otherwise leak across tests. This is a genuine test-infrastructure
  gap the RED step surfaced, not scope creep.
- Added `landing.links.guide: 'Open the user guide'` to the test file's own inline i18n messages
  fixture (`makeI18n()`), matching the pattern of the other 3 link labels already there.
- At this point (before the GREEN step), `[data-testid="link-guide"]` does not exist in
  `LandingLinks.vue` — the new test fails for the correct reason (`guide?.getAttribute('href')` is
  `undefined`, not `/guide/en/`).

#### GREEN
- `Project/frontend/src/features/landing/config/links.ts` — added `guideUrl(locale: SupportedLocale): string`,
  a pure function mapping `en → '/guide/en/'`, `es → '/guide/es/'`, with a comment marking it as
  the scoped exception to the file's existing "URLs are not translated" convention (ADR-UGD-09).
  Requires `import type { SupportedLocale } from '@/stores/locale.store'` (type-only, erased at
  build time — does not create a runtime Pinia dependency in this config module).
- `Project/frontend/src/features/landing/components/LandingLinks.vue` — added
  `const { locale } = storeToRefs(useLocaleStore())` and `const guideHref = computed(() => guideUrl(locale.value))`;
  new `<a data-testid="link-guide" :href="guideHref" target="_blank" rel="noopener noreferrer" class="link link-hover text-base-content/70">`
  placed 3rd of 4 (`github, readme, guide, deck`), matching design.md's specified order and the
  existing 3 links' exact classes.
- `Project/frontend/src/i18n/locales/en.json` — `landing.links.guide: "Open the user guide"`.
- `Project/frontend/src/i18n/locales/es.json` — `landing.links.guide: "Abrí la guía de usuario"`
  (voseo, matching `cta.primary: "Creá tu cuenta gratis"` etc. in the same file).

#### REFACTOR / verification
- `guideUrl()` substitute-verified against the **real** `links.ts` module (not a reimplementation):
  `node --experimental-strip-types -e "import('./src/features/landing/config/links.ts')..."` — Node
  22.18's experimental TS type-stripping correctly erased the `import type` line and ran the real
  function; `guideUrl('en') === '/guide/en/'`, `guideUrl('es') === '/guide/es/'`, both confirmed
  against the live module.
- `LandingLinks.vue` + the updated `LandingView.spec.ts` test itself: **not substitute-executable**.
  Unlike PR1's plain-ESM `.mjs` scripts, this requires Vue SFC compilation, jsdom, Pinia, and
  `@testing-library/vue` — none of which run under plain `node`, even with type-stripping. This is
  confirmed only by static review (component code matches design.md's literal snippet almost
  verbatim; the mock fix is reasoned through Pinia's real `storeToRefs` source semantics above,
  not guessed). **This is the one piece of PR5 that genuinely needs the human's real
  `pnpm vitest run src/features/landing/__tests__/LandingView.spec.ts` to confirm** — flagged
  explicitly rather than claimed as verified.

### Part D — `guide-links.spec.ts` integration walker: Strict TDD

#### RED
Created `Project/frontend/src/features/landing/__tests__/guide-links.spec.ts` — chosen path
(not `scripts/__tests__/`) because `vitest.config.ts`'s `include` already covers both roots
(`src/**/*.{test,spec}.ts` and `scripts/**/*.{test,spec}.ts`) and design.md/tasks.md specify this
exact path; walks the **committed** `public/guide/**` tree directly via `node:fs` rather than
importing `scripts/guide/chapters.mjs` (kept the walker fully independent of the generator's
internals — the whole point is to catch drift in the *shipped* artifact, not to re-test the
generator, which `build-guide.spec.ts` already covers). 5 test cases: EN/ES file-tree identity;
every `href`/`src` resolves to a real file; every sidebar lists every chapter with zero
disabled entries; EN/ES `<h2>` counts match per chapter; `members.html` has zero `<img>` in both
locales.

At RED time (written before running `node scripts/build-guide.mjs` with `members` newly
published), the walker correctly failed for real reasons against the pre-regeneration tree:
`public/guide/{en,es}/members.html` did not exist yet (ENOENT on the dedicated members test), and
the "zero disabled sidebar entries" assertion failed because the 9 already-published pages'
committed sidebars still rendered `members` as `<span class="disabled">` (published:false at that
point in history).

#### GREEN — two real bugs found in the test's own logic, not the generator
Running the RED assertions via substitute `node` execution against the real tree (after
`node scripts/build-guide.mjs`) surfaced two false failures that were bugs in the *test*, fixed
before declaring GREEN:
1. **`href="#content"` (the skip-link's in-page fragment) was being treated as a dead link** by
   the naive `href`/`src` regex walk. Fixed by skipping any ref starting with `#`, alongside the
   existing `/` (absolute app-exit link) skip.
2. **`index.html` legitimately has 20 `<li>` elements, not 10** — `index-body.html`'s
   `{{CHAPTER_LIST}}` placeholder repeats the full chapter list a second time in the page body
   (by design — the guide home page is meant to show the chapter list as its main content, not
   just in the sidebar), on top of the real `<nav class="sidebar"><ol>...</ol></nav>` sidebar.
   The test's "every sidebar lists every chapter" assertion was correct in spirit but too broad in
   scope — fixed by regex-scoping the `<li>` count to the `<nav class="sidebar">...</nav>` region
   only, which is what UG-3 ("sidebar navigation") actually specifies. `build-guide.mjs` itself
   needed zero changes for either fix — both were test-design gaps.

#### REFACTOR / verification
All 5 assertions re-run via direct `node` against the real, fully-regenerated `public/guide/**`
tree (10 chapters × 2 locales, all published): **5/5 pass** — EN/ES trees identical; 0 missing
href/src targets; every sidebar (10 pages × 2 locales) lists all 10 chapters with 0 disabled
entries; every chapter's EN/ES `<h2>` counts match and are non-zero; `members.html` has 0 `<img>`
in both locales. Also re-ran PR1's own `build-guide.spec.ts`-equivalent safety-net assertions
(`renderSidebar`, `validateManifest`, `validateLocaleParity`) against the real `chapters.mjs` with
`members` now published — all pass, 0 errors either direction.

### Final regenerate and verification (Phase 4)
- `node scripts/build-guide.mjs` → `build-guide: wrote 10 chapter(s) x 2 locale(s) + index pages.`
  — succeeded. Regenerated: `members.html` (both locales, new) + sidebar-only diffs on the other
  18 chapter pages + both `index.html` pages (all now show all 10 chapters as real links, 0
  disabled spans).
- `node scripts/build-guide.mjs --check` → `guide:check: clean — committed public/guide/** matches
  the manifest + fragments.`, exit 0.
- `pnpm build` (would confirm `dist/guide/` has all 22 files and TS/Vite compiles the modified
  `LandingLinks.vue`/`links.ts` cleanly) — **BLOCKED, not run.** Same pre-existing Windows
  `node_modules`/pnpm-store corruption documented in PR1's Infrastructure Blocker section,
  reproduced again this session. **This is the one PR in the chain that actually needs this gate
  run for real** — PR2-PR4 were content-only and this gate was lower-stakes for them; PR5 modifies
  real `.vue`/`.ts` source that only `vue-tsc`/`vite build` can fully type-check and bundle.
- `pnpm lint && pnpm test` (full suite) — **BLOCKED, not run.** Same blocker.

### Work Unit Evidence (WU5)

| Evidence | Value |
|---|---|
| Focused test command and exact result | `pnpm vitest src/features/landing/__tests__/guide-links.spec.ts src/features/landing/__tests__/LandingView.spec.ts` — **could not execute via pnpm** (Infrastructure Blocker). Substitute: (a) `guide-links.spec.ts`'s 5 assertions run directly via `node` against the real `public/guide/**` tree — 5/5 pass. (b) `guideUrl()` run directly via `node --experimental-strip-types` against the real `links.ts` module — both locale cases pass. (c) `LandingView.spec.ts`'s new/modified assertions (the mock rewrite, the new guide-link test case) are **not** substitute-executable — Vue SFC + jsdom + Pinia + testing-library have no plain-`node` equivalent path; confirmed by static review only, explicitly flagged as needing the human's real `pnpm vitest run`. |
| Runtime harness command/scenario and exact result | `node scripts/build-guide.mjs` then `node scripts/build-guide.mjs --check` — both succeeded (10 chapters × 2 locales, 0 validation errors, clean diff). `pnpm build` (the real runtime harness for the `src/`-touching parts of this PR) is **N/A — blocked**, reason: pre-existing Windows pnpm/node_modules corruption unrelated to this PR's code (documented since PR1). |
| Rollback boundary | Revert the `members` entry in `chapters.mjs` (`published: false`), delete `content/{en,es}/members.html`, delete `public/guide/{en,es}/members.html`, revert the `members.html` cross-link edits in `content/{en,es}/budget-management.html`, regenerate (or revert directly) the other 18 chapter pages' + 2 index pages' sidebars, and separately revert `links.ts`, `LandingLinks.vue`, the two i18n files, and `LandingView.spec.ts`/`guide-links.spec.ts` — these two halves (chapter content vs. landing integration) are independently revertible; nothing outside PR5's declared scope was touched. |

### TDD Cycle Evidence (Parts C and D only — Part A is standard/content-only, no TDD rows)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 18.1–18.3 | `guide-links.spec.ts` | Integration (real committed tree) | ✅ ran PR1's `build-guide.spec.ts`-equivalent assertions first, all passing pre-batch | ✅ Written first; failed for real reasons (missing `members.html`, disabled-span leftovers) against the pre-regeneration tree | ✅ 5/5 pass after `node scripts/build-guide.mjs` + 2 test-logic fixes (`#content` fragment skip, sidebar-scoped `<li>` count) | ✅ 5 distinct assertions covering file-tree parity, link resolution, sidebar completeness, heading parity, and the UG-6 no-image rule — each exercises a different code path in the generated tree | ✅ Comments added explaining both non-obvious fixes; no further extraction needed |
| 19.1 | `LandingView.spec.ts` | Component (existing @testing-library/vue harness) | ✅ existing 8 tests in the file used as the safety net context (not independently re-run here — no pnpm) | ✅ Written first, plus a real mock-infrastructure fix (plain-object → `ref`-based singleton) surfaced by reasoning through Pinia's `storeToRefs` semantics before any GREEN code existed | ⚠️ **Not executed** — no plain-`node` path for Vue/jsdom/Pinia; GREEN code written per design.md's literal snippet + static verification only | ➖ Not performed — cannot triangulate a test that was never executed | ➖ N/A |
| 19.2 | — (pure fn) | Unit | N/A (new function) | N/A — no test-first for this one; `guideUrl` was written directly alongside 19.1's RED test as the GREEN target | ✅ substitute-verified via `node --experimental-strip-types` against the real `links.ts` module — both locale cases pass | ✅ 2 cases (en, es) both confirmed against the real module | ➖ None needed — already minimal |

### Test Summary
- **Total tests written/modified this batch**: 6 new/changed test cases (5 in `guide-links.spec.ts`,
  1 new case + 1 rewritten mock in `LandingView.spec.ts`)
- **Total tests passing (substitute-verified)**: 5/5 in `guide-links.spec.ts`; `guideUrl()`'s 2
  locale cases against the real module. **Not verified**: `LandingView.spec.ts`'s new/modified
  assertions (Vue component layer) — needs the human's real `pnpm vitest run`.
- **Layers used**: Unit (2 — `guideUrl` locale cases), Integration (5 — `guide-links.spec.ts`),
  Component (1 new case, unverified — `LandingView.spec.ts`)
- **Approval tests**: None — no refactoring-of-existing-behavior tasks in Part A/D; Part C's
  `LandingLinks.vue` change is additive (new anchor + new reactive binding), not a refactor of
  existing behavior
- **Pure functions created**: 1 (`guideUrl` in `links.ts`)

### Deviations from Design / Tasks
1. **Invite flow is on `BudgetSelectionView.vue`, not `BudgetMembersView.vue`** — see Part A's
   "Deviation from the launch prompt" note above. Documented correctly in the chapter; flagging
   since it corrects an implicit assumption in both the launch prompt and (arguably) ADR-UGD-08's
   phrasing.
2. **`LandingView.spec.ts`'s existing locale-store mock had to be rewritten**, not just extended
   with a new test case, because a plain-object mock is structurally incompatible with
   `storeToRefs()` (Pinia only wraps already-reactive properties). This is a necessary consequence
   of ADR-UGD-09's own design choice (`storeToRefs`, not a one-time `.locale` read) — not a
   deviation from the design, but worth flagging as more test-infrastructure work than tasks.md's
   one-line description ("extend `LandingView.spec.ts`") implied.
3. **Added a real `<a href="members.html">` cross-link in `budget-management.html`** (both
   locales), closing the forward-reference PR2 had deliberately left as plain text. This was
   PR2's own documented deviation #2 anticipating this exact PR5 fix — not new scope, just paying
   off that IOU.
4. **Authored total for this batch is ~339 lines** (87 insertions across the 8 tracked-modified
   files per `git diff --stat`, + 64 (`members.html` en) + 68 (`members.html` es) + 120
   (`guide-links.spec.ts`, new/untracked)), close to the ~300 estimate (~13% over) — the smallest
   overrun of any PR in this chain, and well under the confirmed 800-line ceiling (~42%). No
   `size:exception` needed.
5. **Tasks 20.3/20.4 (full `pnpm build` / `pnpm lint && pnpm test`) are left unchecked**, unlike
   PR2/PR4's looser convention of marking substitute-verified gates `[x]` with a caveat. This PR
   is the one place in the entire chain where that distinction matters: it is the only PR whose
   correctness depends on a real TypeScript compile + Vite bundle of modified `.vue`/`.ts` source,
   which has no substitute-execution path in this session.

### Issues Found
None beyond the two test-logic bugs documented above (Part D GREEN), which were found and fixed
within this same batch, not left as open bugs. `pnpm`/`node_modules` remains blocked in this
session by the same pre-existing Windows filesystem-level corruption documented in PR1's
Infrastructure Blocker section — reproduced again, not caused by this PR's code.

### Review recommendation
Unlike PR2-PR4 (content-only, correctly skipped for formal code review), **PR5 is the one PR in
this chain that should go through the project's formal 4-lens (`gentle-ai` 4R: risk / resilience /
readability / reliability) review before merge** — it is the only PR touching real application
`src/` (Pinia store consumption, a new reactive computed, a new i18n-bound anchor) and test
infrastructure (a rewritten Vitest mock), matching the exact criteria that triggered PR1's full 4R
review.

## Status (PR5)
15/17 PR5 tasks complete (17.1-17.3, 18.1-18.5, 19.1-19.5, 20.1-20.2). Members chapter authored and
published (10/10 chapters now live in both locales); locale-aware landing link implemented per
ADR-UGD-09; `guide-links.spec.ts` integration walker created and substitute-verified 5/5 against
the real committed tree. **Blocked, not run**: 20.3 (`pnpm build`) and 20.4
(`pnpm lint && pnpm test`) — both need the human's real `pnpm` run, plus `LandingView.spec.ts`'s
new/modified assertions specifically need `pnpm vitest run` to confirm (Vue/Pinia layer has no
substitute-execution path this session). Left uncommitted per explicit instruction — all PR5
changes are working-tree modifications on `feat/user-guide-docs-pr5`, awaiting orchestrator/user
review, the real `pnpm vitest run`/`pnpm run build` confirmation, and explicit approval before
`git add`/`commit`/`push`. **This is the final PR of the `user-guide-docs` change** — once
committed and confirmed, all 5 PRs in the chain are complete and the tracker branch
`feat/user-guide-docs` can be readied for its own merge to `main`.

## Post-implementation fix: dev-server directory-index gap (found by manual QA)

After the human ran the full `pnpm test` (851/851 passed) and `pnpm lint` (clean) and confirmed
PR5 was ready to commit, manual click-through QA in the running `vite dev` server found the guide
link resolved to a **blank screen** in both locales, despite the automated tests passing.

**Root cause**: ADR-UGD-04 assumed `/guide/en/` (a bare directory URL) "resolves identically under
`vite dev`, `vite preview`, Caddy" — true for Caddy (`file_server` resolves a directory to its
`index.html`) but **false for `vite dev`**: an unmatched directory path there falls through to the
SPA's own root `index.html` instead, and since no Vue route matches `/guide/en/`, the rendered
page is blank. This gap wasn't caught by any automated test because `guide-links.spec.ts` walks
the committed static tree directly (file existence, not HTTP resolution), and `LandingView.spec.ts`
only asserts the `href` attribute's string value, not that a dev server actually resolves it.

**Fix**: `GUIDE_PATH_BY_LOCALE` in `links.ts` now points at the explicit file
(`/guide/en/index.html`, `/guide/es/index.html`) instead of the bare directory — a real static
file resolves identically in every environment (dev, preview, Caddy), removing the dependency on
any server's directory-index behavior. Updated `LandingView.spec.ts`'s two assertions to match.
No other file referenced the bare-directory form (`links.ts` is the single source of truth,
`LandingLinks.vue` only calls `guideUrl()`).

**Lesson**: an environment-parity claim in design.md was accepted without independently verifying
it in the specific environment that turned out to differ — automated tests exercised the two
environments that *did* behave consistently (the static file tree, and the string-level href),
never the actual HTTP resolution the real dev server performs. Manual QA in a live server remains
necessary even with a passing automated suite for anything involving server-level URL resolution.

## Cumulative task status (final, all 5 PRs)
**61/67 tasks complete.** PR1: 25/27 (5.4/5.5 remain the same partially-verified infra manual
gates, unchanged this session). PR2: 9/9. PR3: 4/4 authoring tasks (12.1/12.2 manual gates
pending the human-run commands, unchanged this session). PR4: 8/8. PR5: 15/17 (20.3/20.4 blocked
on the human's real `pnpm build`/`pnpm lint && pnpm test`). All 5 chapters' authoring work and
both TDD-required `src/`-touching pieces (landing link, integration walker) are implemented;
the remaining 2 open tasks are pure manual-gate confirmations, not missing implementation.
