# Apply Progress: user-guide-docs (PR1 of 5, PR2 of 5)

## Status: PR1 DONE — implemented, tested (real), committed, reviewed. PR2 IMPLEMENTED, NOT COMMITTED.

Chain: `feat/user-guide-docs` (tracker) ← PR1 `feat/user-guide-docs-pr1` ← PR2
`feat/user-guide-docs-pr2` (current) ← PR3 ← PR4 ← PR5.
PR1's scope (guide infra + `auth` pilot chapter) is committed and reviewed. PR2's scope
(`budget-management`, `budget-structure-cycles`, `budget-structure-categories`) is implemented in
this batch, uncommitted. PR3–PR5 tasks were not touched.

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

## Cumulative task status (all PRs)
34/67 tasks complete (PR1: 25/27 — 5.4/5.5 remain the same partially-verified infra manual gates
as before; PR2: 9/9 — fully complete). PR3–PR5 (33 tasks) not started.
