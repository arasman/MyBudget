# Verification Report: user-guide-docs

**Verdict**: PASS WITH WARNINGS (0 CRITICAL, 2 WARNING, 1 SUGGESTION — both WARNINGs fixed
post-verify, see Resolution below)

## Scope

Verified all 5 chained PRs (`1e2d45b`, `87adfcb`, `d87988b`, `181705b`, `df76169`, `390455d`,
`0662147` — 7 commits on `feat/user-guide-docs-pr5`, based on `main`), 118 files changed,
+7158/-31.

## What Was Verified

- All 6 planning artifacts read in full (`proposal.md`, `design.md`, `tasks.md`,
  `apply-progress.md`, `specs/user-guide/spec.md`, `specs/landing-page/spec.md`).
- Git history clean: nothing out of scope for this change.
- 22 real guide files exist (`Project/frontend/public/guide/{en,es}/` = 11 each: 10 chapters +
  index). Zero `<script>`/`<iframe>` tags across generated pages — genuinely static.
- Every guide-related test file read and counted directly: `build-guide.spec.ts` (20 cases),
  `render-diagrams.spec.ts` (10 cases), `guide-links.spec.ts` (5 cases), `LandingView.spec.ts`
  (9 `it()` blocks, 1 new) — all counts match `apply-progress.md`'s claims exactly.
- Assertion Quality Audit on all four test files: 0 CRITICAL, 0 WARNING — no tautologies, ghost
  loops, or trivial assertions; every assertion checks a concrete real value against real modules
  or the real committed `public/guide/**` tree.
- `package.json`'s `"build"` script untouched (`vue-tsc -b && vite build`); `guide:build`/
  `guide:check` exist but are unwired (ADR-UGD-01 honored).
- `git diff --stat main feat/user-guide-docs-pr5` shows zero Caddyfile, docker-compose, or
  README changes (non-goals honored).
- `links.ts`/`LandingLinks.vue` match ADR-UGD-04's documented post-implementation fix (explicit
  `/guide/{locale}/index.html`, not a bare directory) and ADR-UGD-09's pure-function design.
- `content/en/members.html` spot-checked against UG-6: 6 sections, zero images, invite-sending
  only, real cross-link both directions to/from `budget-management.html`.
- ADR-UGD-06 curation spot-check: `auth` has 9 source PNGs in `docs/slides/flows/`, 6 curated —
  matches the ~4-6 target; all 9 non-`members` chapters have exactly 6 curated images.

## Test Evidence

This verify pass could not execute `pnpm` directly (same pre-existing Windows `node_modules`
filesystem corruption documented throughout `apply-progress.md`) and relied on real terminal
output the human user ran and pasted back, plus direct source inspection. Final confirmed
results:
- `pnpm test` (full suite) → **851/851 passed**
- `pnpm lint` → clean
- `pnpm run build` → succeeded
- Manual click-through QA in both locales (`en`/`es`) in `vite dev` → confirmed working after the
  directory-index fix

## Requirements Coverage

All 9 requirements / 13 scenarios across `UG-1`–`UG-8` (new `user-guide` capability) and the
`LANDING-4` delta (`landing-page` capability) are implemented, source-verified, and covered by
real (non-tautological) tests.

## Findings

**W1 (fixed)**: `specs/landing-page/spec.md`'s LANDING-4 scenario said the guide href resolves to
a bare directory (`/guide/en/`), but the shipped implementation correctly resolves to the explicit
`/guide/en/index.html` (the dev-server directory-index bug fix). Spec text was stale relative to
the implementation. **Resolution**: updated the scenario text to match the shipped behavior.

**W2 (fixed)**: `tasks.md` still showed `[ ]` on 20.3 (`pnpm build`) and 20.4
(`pnpm lint && pnpm test`) even though the human had already run both for real (851/851, clean
lint, successful build). **Resolution**: marked both `[x]` with the real evidence noted inline.

**S1 (accepted, not a defect)**: this verify pass, like every apply pass in this change, could not
run `pnpm` directly due to the pre-existing Windows environment issue — relied on human-relayed
real terminal output instead of direct execution. Documented throughout `apply-progress.md`, not
a new blocker.

## Known Accepted Deviations (verified as accurately documented, not re-flagged)

- PR1 exceeded its 800-line authored-line estimate but stayed under the confirmed budget ceiling
  (`size:exception`, user-approved).
- PR2–PR4 skipped formal `gentle-ai` 4-lens code review (content-only, user's explicit call).
- PR1 got a full 4R review (approved, 4 WARNING findings, all fixed in follow-up commit `87adfcb`
  without a separate formal re-review receipt — user's explicit call, documented in
  `apply-progress.md`).
- `render-diagrams.mjs`'s real `npx mermaid-cli` subprocess was never spawned end-to-end (only
  unit-tested) — acceptable since no chapter ended up needing a diagram.

## Next Recommended

`sdd-archive`.
