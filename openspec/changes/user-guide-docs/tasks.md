# Tasks: User Guide Docs

Strict TDD: every testable-behavior task is RED (failing test) → GREEN (minimal code) → REFACTOR
(confirm green). Chained PRs, Feature Branch Chain: `feat/user-guide-docs` is the tracker/
integration branch (only it merges to `main`); each child PR targets the immediately previous
PR's branch.

## Review Workload Forecast

Project convention for this change confirms a **800-line** review budget (not the generic 400),
with `public/guide/**` generated HTML and copied PNGs excluded as goldens (verified by
`pnpm guide:check` regenerate-and-diff — same treatment as `MyBudget.pptx` elsewhere). The literal
guard lines below keep the standard `400-line` label for downstream tooling compatibility; the
Value column states the actual 800-line ceiling this change is assessed against.

| Field | Value |
|-------|-------|
| Estimated changed lines | PR1 ~520, PR2 ~290, PR3 ~260, PR4 ~280, PR5 ~300 (authored only; goldens excluded) |
| 400-line budget risk (assessed vs. confirmed 800-line budget) | Low–Medium (PR1 nearest the ceiling at ~65%) |
| Chained PRs recommended | Yes |
| Suggested split | PR1 → PR2 → PR3 → PR4 → PR5 (Feature Branch Chain) |
| Delivery strategy | auto-forecast |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | PR | Base branch | Focused test command | Runtime harness | Rollback boundary |
|------|------|----|----|----|----|----|
| WU1 | Guide infra (manifest, template, generator, diagrams CLI) + `auth` pilot chapter | PR1 | `feat/user-guide-docs` | `pnpm vitest scripts/__tests__/build-guide.spec.ts scripts/__tests__/render-diagrams.spec.ts` | `pnpm guide:build && pnpm guide:check` | Revert `scripts/guide/**`, `scripts/build-guide.mjs`, `render-diagrams.mjs` diff, `public/guide/**`, `package.json` script entries |
| WU2 | `budget-management` (+2 invite captures), `budget-structure-cycles`, `budget-structure-categories` | PR2 | PR1 branch | `pnpm guide:check` | `pnpm guide:build` | Revert 3 manifest entries + their `content/{en,es}` fragments + regenerated `public/guide/**` |
| WU3 | `budget-structure-periods-lines`, `budget-execution` | PR3 | PR2 branch | `pnpm guide:check` | `pnpm guide:build` | Revert 2 manifest entries + fragments + regenerated output |
| WU4 | `bank-accounts`, `current-situation`, `dashboard` | PR4 | PR3 branch | `pnpm guide:check` | `pnpm guide:build` | Revert 3 manifest entries + fragments + regenerated output |
| WU5 | `members` chapter (text-only) + landing link integration | PR5 | PR4 branch | `pnpm vitest src/features/landing/__tests__/guide-links.spec.ts src/features/landing/__tests__/LandingView.spec.ts` | `pnpm guide:build && pnpm build` | Revert `members` manifest entry + fragments, `links.ts`, `LandingLinks.vue`, i18n keys, `guide-links.spec.ts` |

---

## PR 1 — Guide Infra + Pilot Chapter (`auth`) (~520 authored lines)

> Branch: `feat/user-guide-docs-pr1` — base `feat/user-guide-docs`. Mechanical/reviewable without
> reading prose; no other PR may mix template/CSS changes into content.

### Phase 1: Manifest and shell
- [x] 1.1 Create `scripts/guide/chapters.mjs` — `GUIDE_TITLE`, `LOCALES`, `CHAPTERS` (all 10 entries, ADR-UGD-05 order); only `auth.published = true`, other 9 `published = false`, no `images[]` yet.
- [x] 1.2 Create `scripts/guide/template.html` — page shell per ADR-UGD-03 with all `{{...}}` placeholders.
- [x] 1.3 Create `scripts/guide/index-body.html` — guide-home body fragment.
- [x] 1.4 Create `public/guide/assets/guide.css` — standalone stylesheet, light theme, `@media (max-width: 48rem)` rule, no Tailwind/DaisyUI. (243 lines — larger than the ~150 estimate; see apply-progress deviation note.)

### Phase 2: `build-guide.mjs` generator — TDD
- [x] 2.1 RED: create `scripts/__tests__/build-guide.spec.ts` — sidebar renders exactly one `aria-current="page"`; unpublished chapters render no `<a>`.
- [x] 2.2 RED: same file — locale toggle `href` is `../<other-lang>/<same-file>`.
- [x] 2.3 RED: same file — asset `<img>` paths use `../assets/<slug>/`, never `../../assets/`.
- [x] 2.4 RED: same file — an unresolved `{{PLACEHOLDER}}` throws / non-zero exit.
- [x] 2.5 RED: same file — a manifest `images[]` entry with no matching source PNG under `docs/slides/flows/<slug>/` → non-zero exit.
- [x] 2.6 RED: same file — a fragment `../assets/...` reference not listed in that chapter's `images[]` → non-zero exit.
- [x] 2.7 RED: same file — EN/ES fragment file-set mismatch → non-zero exit.
- [x] 2.8 RED: same file — a chapter with no `images` key (like `members`) is valid, not an error.
- [x] 2.9 GREEN: create `scripts/build-guide.mjs` implementing 2.1–2.8: stamps template+index per published chapter × locale, copies curated `images[]`, runs both validation directions.
- [x] 2.10 REFACTOR: `pnpm vitest` unavailable in this session (pre-existing `node_modules` filesystem corruption — see apply-progress "Infrastructure Blocker"). Substitute GREEN evidence: ran the exact assertions from `build-guide.spec.ts` directly against the real module via plain `node` — all passed. Also exercised `validateManifest`'s two failure directions and `guide:check`'s drift-detection against the real committed tree (inject-tamper-detect-revert) — all behaved correctly.

### Phase 3: `render-diagrams.mjs` generalization — TDD
- [x] 3.1 RED: create `scripts/__tests__/render-diagrams.spec.ts` — zero-arg `resolveOptions()`/`buildMermaidArgv()` byte-identical to today's `-i <mmd> -o <png> -c <config> -b white -s 2 -w 1400`.
- [x] 3.2 RED: same file — `--format svg` drops `-s/--scale`, changes output extension to `.svg`, keeps `-b white`/`-w`.
- [x] 3.3 RED: same file — `parseArgs` accepts `-i/--input`, `-o/--out-dir`, `-f/--format`, `-w/--width`, `-s/--scale`; relative paths resolve against `cwd`, not `__dirname`.
- [x] 3.4 GREEN: modify `scripts/render-diagrams.mjs` — export `parseArgs`, `resolveOptions`, `buildMermaidArgv`; keep `main()` behind an `import.meta.url` entry guard; preserve current defaults exactly.
- [x] 3.5 REFACTOR: same `pnpm vitest` blocker as 2.10. Substitute GREEN evidence: ran the exact assertions from `render-diagrams.spec.ts` directly against the real module via plain `node` — all passed, including the byte-identical zero-arg argv assertion.

### Phase 4: `auth` chapter content
- [x] 4.1 Author `scripts/guide/content/en/auth.html` — register/login flow, grounded in `/register`, `/login`. Also documents `/forgot-password` and `/reset-password` (real routes in `router/index.ts`, no captures exist) and logout — see apply-progress deviation note on scope.
- [x] 4.2 Author `scripts/guide/content/es/auth.html` — same headings/structure, ES voseo register.
- [x] 4.3 Curate 6 screenshots from `docs/slides/flows/auth/*.png` into `chapters.mjs`'s `auth.images[]` (register-empty/filled/success, login-success/invalid-error, logout-success).

### Phase 5: Wire scripts, generate, verify
- [x] 5.1 Modify `package.json` — add `"guide:build": "node scripts/build-guide.mjs"` and `"guide:check"` (regenerate to temp dir, diff vs. committed `public/guide/`); do NOT add either to `"build"`.
- [x] 5.2 Run `pnpm guide:build` (ran via direct `node scripts/build-guide.mjs`, pnpm itself blocked — see below); generated `public/guide/{en,es}/{index,auth}.html` + `public/guide/assets/auth/*.png`. Left uncommitted in the working tree per instructions — awaiting explicit user approval to commit.
- [x] 5.3 Manual gate: `pnpm guide:check` — ran via direct `node scripts/build-guide.mjs --check` — clean, no diff. Verified both directions: passes on the real tree, and correctly fails non-zero when the committed output is tampered (tested and reverted).
- [ ] 5.4 Manual gate: `pnpm build` — **BLOCKED**, not verified. `node_modules` in this session has pre-existing filesystem-level corruption (confirmed via `lstat`/`rename` failures with corrupted metadata — 1600-era timestamps, impossible block counts) that prevents `pnpm` from running at all, even before reaching Vite. Not caused by this PR's code; `guide.css`/`public/guide/**` structure was manually confirmed correct (file tree, hrefs, no Vite-pipeline classes).
- [ ] 5.5 Manual gate: `pnpm render-diagrams` (no args) — **PARTIALLY VERIFIED**. Could not spawn the real `npx`/mermaid-cli subprocess (same `pnpm`/`node_modules` blocker). The behavioral contract design.md specifies as the actual source of truth — "asserted by a unit test, not by re-reading the code" — IS verified: `buildMermaidArgv(resolveOptions({}), ...)` produces the exact byte-identical argv `-i <mmd> -o <png> -c <config> -b white -s 2 -w 1400` (confirmed via direct `node` execution of the spec's own assertion).

---

## PR 2 — Core Structure Chapters (~290 authored lines)

> Branch: `feat/user-guide-docs-pr2` — base PR1 branch. Chapters: `budget-management` (incl. 2
> invite-acceptance captures — hard dependency for PR5's `members` cross-link), `budget-structure-cycles`,
> `budget-structure-categories`.

### Phase 1: `budget-management`
- [x] 6.1 Modify `chapters.mjs` — `budget-management.published = true`, `images[]` incl. `09-invite-accept-success.png`, `10-invite-accept-error.png`.
- [x] 6.2 Author `content/en/budget-management.html` + `content/es/budget-management.html` — matching headings, incl. invite-acceptance success/error captures.

### Phase 2: `budget-structure-cycles`
- [x] 7.1 Modify `chapters.mjs` — `budget-structure-cycles.published = true`, `images[]`.
- [x] 7.2 Author `content/{en,es}/budget-structure-cycles.html`.

### Phase 3: `budget-structure-categories`
- [x] 8.1 Modify `chapters.mjs` — `budget-structure-categories.published = true`, `images[]`.
- [x] 8.2 Author `content/{en,es}/budget-structure-categories.html`.

### Phase 4: Regenerate and verify
- [x] 9.1 Run `pnpm guide:build`; commit regenerated output — note: sidebars of ALL previously-committed pages (incl. PR1's `auth`) also regenerate to include the 3 newly-published entries; this is expected and golden.
- [x] 9.2 Manual gate: `pnpm guide:check` clean.
- [x] 9.3 Manual gate: `pnpm vitest scripts/__tests__/build-guide.spec.ts` — confirm manifest validation still passes for the 3 new chapters. (ran via substitute direct-`node` execution — see apply-progress PR2 section for the pnpm blocker.)

---

## PR 3 — Planning & Execution Chapters (~260 authored lines)

> Branch: `feat/user-guide-docs-pr3` — base PR2 branch. Chapters: `budget-structure-periods-lines`,
> `budget-execution` (largest source capture sets — curate to ~4–6 per ADR-UGD-06 regardless).

### Phase 1: `budget-structure-periods-lines`
- [x] 10.1 Modify `chapters.mjs` — publish + `images[]`.
- [x] 10.2 Author `content/{en,es}/budget-structure-periods-lines.html`.

### Phase 2: `budget-execution`
- [x] 11.1 Modify `chapters.mjs` — publish + `images[]`.
- [x] 11.2 Author `content/{en,es}/budget-execution.html`.

### Phase 3: Regenerate and verify
- [ ] 12.1 Run `pnpm guide:build`; commit regenerated output (incl. sidebar updates on all prior pages).
- [ ] 12.2 Manual gate: `pnpm guide:check` clean.

---

## PR 4 — Reporting Chapters (~280 authored lines)

> Branch: `feat/user-guide-docs-pr4` — base PR3 branch. Chapters: `bank-accounts`,
> `current-situation`, `dashboard`.

### Phase 1: `bank-accounts`
- [x] 13.1 Modify `chapters.mjs` — publish + `images[]`.
- [x] 13.2 Author `content/{en,es}/bank-accounts.html`.

### Phase 2: `current-situation`
- [x] 14.1 Modify `chapters.mjs` — publish + `images[]`.
- [x] 14.2 Author `content/{en,es}/current-situation.html`.

### Phase 3: `dashboard`
- [x] 15.1 Modify `chapters.mjs` — publish + `images[]`.
- [x] 15.2 Author `content/{en,es}/dashboard.html`.

### Phase 4: Regenerate and verify
- [x] 16.1 Run `pnpm guide:build`; commit regenerated output (incl. sidebar updates on all prior pages). (ran via direct `node scripts/build-guide.mjs`; left uncommitted per instruction, pending human-run `pnpm` confirmation and explicit approval)
- [x] 16.2 Manual gate: `pnpm guide:check` clean. (ran via direct `node scripts/build-guide.mjs --check` — clean)

---

## PR 5 — Members Chapter + Landing Integration (~300 authored lines)

> Branch: `feat/user-guide-docs-pr5` — base PR4 branch. Only PR touching `src/`; isolated last so a
> build break is immediately attributable. Requires PR2's `budget-management` invite-acceptance
> captures already merged.

### Phase 1: `members` chapter (text-only, no `images` key)
- [x] 17.1 Modify `chapters.mjs` — `members.published = true`, no `images` key (text-only, per ADR-UGD-06).
- [x] 17.2 Author `content/en/members.html` — 6 sections per ADR-UGD-08 (who can manage, viewing the list, changing a role incl. action-matrix note, removing access, restoring access, inviting someone — stops at "invitation sent", links to `budget-management` for acceptance).
- [x] 17.3 Author `content/es/members.html` — same 6 sections, ES voseo register.

### Phase 2: Guide integration walker — TDD
- [x] 18.1 RED: create `src/features/landing/__tests__/guide-links.spec.ts` — EN/ES file sets under `public/guide/**` identical; every `<img src>`, sidebar `href`, and toggle target resolves to an existing file.
- [x] 18.2 RED: same file — every chapter page's sidebar lists all 10 chapters; heading counts match across EN/ES.
- [x] 18.3 RED: same file — `members.html` (both locales) contains zero `<img>` elements.
- [x] 18.4 GREEN: fix any gap surfaced by 18.1–18.3 (two real gaps found in the *test itself*, not the generator: the skip-link's `href="#content"` fragment was being flagged as a dead link, and `index.html`'s sidebar-count check didn't account for its body also repeating the chapter list via `index-body.html`'s `{{CHAPTER_LIST}}` — both fixed by scoping the check to fragment-refs and to the `<nav class="sidebar">` region; the generator itself needed zero changes).
- [x] 18.5 REFACTOR: `pnpm vitest src/features/landing/__tests__/guide-links.spec.ts` — could not execute via pnpm (see Infrastructure Blocker); substitute: ran the exact 5 assertions directly via `node` against the real committed `public/guide/**` tree — 5/5 pass.

### Phase 3: Locale-aware landing link — TDD
- [x] 19.1 RED: extend `src/features/landing/__tests__/LandingView.spec.ts` — `link-guide` href is `/guide/en/` by default and updates to `/guide/es/` after `localeStore.setLocale('es')`.
- [x] 19.2 GREEN: modify `src/features/landing/config/links.ts` — add `guideUrl(locale: SupportedLocale)` pure function + scoped-exception comment (ADR-UGD-09).
- [x] 19.3 GREEN: modify `src/features/landing/components/LandingLinks.vue` — `storeToRefs(useLocaleStore())`, computed `guideHref`, new anchor placed 3rd of 4 (github, readme, guide, deck).
- [x] 19.4 GREEN: modify `src/i18n/locales/en.json` and `es.json` — add `landing.links.guide` (`Open the user guide` / `Abrí la guía de usuario`).
- [x] 19.5 REFACTOR: `pnpm vitest src/features/landing/__tests__/LandingView.spec.ts` — **not executed**, `pnpm`/jsdom/Vue Test Utils cannot be substitute-run via plain `node` (unlike the pure-function scripts). `guideUrl()` itself was substitute-verified against the real `links.ts` module via `node --experimental-strip-types`; the Vue component/store-reactivity wiring is confirmed only by static review — flagged for the human's real `pnpm vitest run` to confirm 19.1 green and the existing 3-link assertions unaffected.

### Phase 4: Final regenerate and full verification
- [x] 20.1 Run `pnpm guide:build`; commit regenerated `members.html` (both locales) + final sidebar regeneration across all 10×2 chapter pages (now fully published). (ran via direct `node scripts/build-guide.mjs`; left uncommitted per instruction)
- [x] 20.2 Manual gate: `pnpm guide:check` clean. (ran via direct `node scripts/build-guide.mjs --check` — clean)
- [ ] 20.3 Manual gate: `pnpm build` — `dist/guide/` has all 22 files; verify `/guide/en/` and `/guide/es/` resolve via their `index.html`. **BLOCKED**, not run (pnpm/node_modules Windows blocker, see Infrastructure Blocker) — needs the human's real run since this PR touches `src/` and TS/Vite compilation of `LandingLinks.vue`/`links.ts` is unverified.
- [ ] 20.4 Manual gate: `pnpm lint && pnpm test` (full suite) — zero regressions outside this change's files. **BLOCKED**, not run (same pnpm blocker) — needs the human's real run.
