# Design: User Guide Docs

## Technical Approach

A bilingual static docs site served straight out of `Project/frontend/public/guide/`. The **served artifact** is exactly what the proposal describes: ~20 plain `.html` files, zero client-side JS, one duplicated sidebar per page, Vite `public/` passthrough, no Caddyfile change.

The **authoring surface** is not those 20 files. A committed, manually-invoked generator (`scripts/build-guide.mjs`) stamps a shared shell + sidebar around per-chapter body fragments, following the exact posture already established by `build-showcase.mjs`, `render-diagrams.mjs`, `build-pptx.mjs` and `generate-slide-index.mjs`: **generator committed, output committed, never run in CI or `pnpm build`, re-run by hand when sources change.** This keeps the "no new build step" constraint intact (`pnpm build` still only copies `public/`) while removing the sidebar-drift risk at the source rather than mitigating it with discipline.

Presentation is a single hand-written stylesheet at `public/guide/assets/guide.css`. Tailwind and DaisyUI do **not** process `public/` — those files bypass the Vite pipeline entirely — so the guide cannot reuse the app's utility classes and must not try.

Chapter order, EN/ES sidebar labels, per-chapter curated image lists, and publication state all live in one manifest (`scripts/guide/chapters.mjs`). That manifest is the single seam that `sdd-tasks` can slice against.

## Architecture Decisions

### ADR-UGD-01: Authoring method — build-time stamper, not hand-copied pages

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Hand-write 20 full pages from a canonical reference snippet | Zero tooling; but ~1,400 lines of *authored* HTML, sidebar drift is a discipline problem forever, and every future chapter/label edit is a 20-file find-and-replace | Rejected |
| Client-side JS partial (`fetch` the sidebar) | Explicitly out of scope; breaks "renders standalone, no JS required" | Rejected (proposal) |
| SSG (11ty / Astro / VitePress) | Solves everything, but adds a dependency, a second build system, and a real build step — all out of scope | Rejected |
| **Committed `scripts/build-guide.mjs` stamper, committed HTML output** | One more manual-regenerate script in a repo that already has four; sidebar/shell/labels have exactly one source; the shipped pages stay dumb static HTML | **Chosen** |

**Rationale.** Three independent wins:

1. **Risk elimination, not mitigation.** Proposal risk *"sidebar duplicated across ~20 files drifts out of sync"* is mitigated in the hand-written option only by "copy verbatim" discipline. With a stamper it is structurally impossible — the sidebar exists once, in `chapters.mjs` + `template.html`.
2. **Review budget.** The proposal already flags high risk of blowing the 800-line budget. Hand-written pages put ~1,400 lines of HTML into the *authored* count. Generated output is a golden: deterministic, reproducible from committed inputs, and verifiable by regenerate-and-diff (`pnpm guide:check`). That moves ~1,400 lines out of the authored surface and leaves ~1,400 authored lines of prose fragments + infra — still needing chained PRs, but with a far better signal-to-noise ratio per review.
3. **Nothing is given up.** The generated pages are byte-for-byte the same dumb static HTML the hand-written option would produce, sidebar duplication included. The proposal's accepted tradeoff (duplication in the *artifact*) is honoured exactly; only the duplication in the *authoring workflow* is removed.

**Non-negotiable constraint**: `build-guide.mjs` is never referenced from the `build` script. `pnpm build` must remain `vue-tsc -b && vite build`.

### ADR-UGD-02: Where templates and generator sources live

Nothing that is not a shipped asset may sit under `public/` — Vite copies `public/` verbatim into `dist/`, so a stray `template.html` or `chapters.mjs` would be published and, worse, `template.html` would be a reachable half-rendered URL.

```
Project/frontend/
├── scripts/
│   ├── build-guide.mjs                  # generator + curated asset copier (authoring-time only)
│   ├── render-diagrams.mjs              # generalized (ADR-UGD-07)
│   ├── guide/
│   │   ├── chapters.mjs                 # THE manifest: order, slugs, labels, images, published
│   │   ├── template.html                # page shell with {{PLACEHOLDER}} slots
│   │   ├── index-body.html              # body fragment for the per-locale landing page
│   │   └── content/
│   │       ├── en/<slug>.html           # authored body fragment, EN  (10 files)
│   │       └── es/<slug>.html           # authored body fragment, ES  (10 files)
│   └── __tests__/
│       ├── build-guide.spec.ts
│       └── render-diagrams.spec.ts
└── public/guide/                        # GENERATED + committed; the only thing that ships
    ├── en/index.html, <slug>.html       # 11 files
    ├── es/index.html, <slug>.html       # 11 files
    └── assets/
        ├── guide.css                    # hand-written, shipped
        └── <area>/*.png                 # copied subset of docs/slides/flows/<area>/
```

**Rationale**: `scripts/` is already the home of every manual-regenerate pipeline in this frontend, and it is outside the Vite root, so nothing there can leak into `dist/`. `guide.css` is the one hand-written file that intentionally lives under `public/` because it *is* a shipped asset.

### ADR-UGD-03: Page shell — plain CSS, no framework, no JS

**Choice**: One `template.html`, one `guide.css`, semantic markup only.

```html
<!doctype html>
<html lang="{{LANG}}">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>{{CHAPTER_TITLE}} · {{GUIDE_TITLE}}</title>
    <link rel="stylesheet" href="../assets/guide.css" />
    <link rel="alternate" hreflang="{{OTHER_LANG}}" href="../{{OTHER_LANG}}/{{FILENAME}}" />
  </head>
  <body>
    <a class="skip" href="#content">{{SKIP_LABEL}}</a>
    <div class="layout">
      <nav class="sidebar" aria-label="{{NAV_LABEL}}">
        <a class="brand" href="./index.html">{{GUIDE_TITLE}}</a>
        <p class="locale-toggle">
          <a href="../{{OTHER_LANG}}/{{FILENAME}}" hreflang="{{OTHER_LANG}}"
             lang="{{OTHER_LANG}}">{{OTHER_LANG_LABEL}}</a>
        </p>
        <ol>
          <!-- one <li> per published chapter; current chapter is a
               <span aria-current="page">, not an <a> -->
        </ol>
        <p class="back"><a href="/">{{BACK_TO_APP}}</a></p>
      </nav>
      <main id="content" class="content">
        {{BODY}}
      </main>
    </div>
  </body>
</html>
```

**Constraints this encodes**:
- **No Tailwind/DaisyUI.** `public/` bypasses Vite; utility classes would render unstyled. `guide.css` is standalone (~150 lines), targeting a light theme close to the app's `base-100`/`base-content`.
- **No hamburger, no JS.** Responsive behaviour is a single `@media (max-width: 48rem)` rule that stacks the sidebar above the content and turns the chapter list into a wrapped inline list. A JS drawer would violate "no client-side JS for the guide content".
- **Current chapter is not a link.** `<span aria-current="page">` rather than a self-referencing `<a>` — satisfies "current one marked" and is the accessible form.
- **`<link rel="alternate" hreflang>`** costs one line and makes the EN/ES pair machine-discoverable.

### ADR-UGD-04: URL and locale-toggle mechanics

**Confirmed**: Vite's `publicDir` copy preserves directory structure verbatim and does **not** apply its HTML transform to files other than the configured Rollup HTML inputs. `public/guide/en/dashboard.html` therefore lands at `dist/guide/en/dashboard.html` byte-identical and is served at `/guide/en/dashboard.html`. Same mechanism as the already-shipping `public/showcase/*.webp`.

**Convention**: identical filename, sibling locale directory. Every cross-reference is **relative**, never absolute, so the guide works identically under `vite dev`, `vite preview`, Caddy, and a `file://` open.

| From a page at `/guide/en/<slug>.html` | Href | Resolves to |
|---|---|---|
| Locale toggle | `../es/<slug>.html` | `/guide/es/<slug>.html` |
| Sibling chapter | `<other-slug>.html` | `/guide/en/<other-slug>.html` |
| Guide home | `./index.html` | `/guide/en/index.html` |
| Stylesheet | `../assets/guide.css` | `/guide/assets/guide.css` |
| Screenshot | `../assets/<area>/<file>.png` | `/guide/assets/<area>/<file>.png` |
| Back to app | `/` | app root (absolute on purpose — this one leaves the guide) |

**Correction worth recording**: the asset prefix is `../assets/`, **not** `../../assets/`. From `/guide/en/x.html` a single `..` already reaches `/guide/`. Using `../../` would resolve to `/assets/`, which does not exist.

**Per-locale `index.html` is required, not optional.** Success criterion *"the landing link resolves to `/guide/en/` or `/guide/es/`"* only holds if those directories have an index document. The Caddyfile is `try_files {path} /index.html` + `file_server`: a request for `/guide/en/` matches an existing directory, so `file_server` serves that directory's `index.html` — no Caddyfile change needed, **provided the file exists**. Without it the request falls through to the SPA shell and the user lands on the Vue app instead of the guide.

So the guide is **22 files, not 20**: 10 chapters + 1 index per locale. The index is deliberately thin — guide title, a two-sentence intro, and the chapter list — generated from `index-body.html` through the same template. It is not an 11th chapter and does not appear in the sidebar `<ol>` (the brand link points at it).

### ADR-UGD-05: Sidebar order and labels

Order mirrors the app's real navigation, which is `BudgetTabs.vue` (there is no left-nav menu; `AppLayout.vue` has only the budget switcher), prefixed by the pre-login flow. Verified against `BudgetTabs.vue` tab order and `router/index.ts`.

| # | slug | EN label | ES label | App anchor |
|---|------|----------|----------|------------|
| 1 | `auth` | Account & sign-in | Cuenta e inicio de sesión | `/register`, `/login` — precedes every tab |
| 2 | `budget-management` | Budgets | Presupuestos | `nav.budgets` → `BudgetSelection` |
| 3 | `budget-structure-cycles` | Cycles | Ciclos | tab 1 → `CycleList` |
| 4 | `budget-structure-categories` | Categories | Categorías | tab 2 → `CategoryTree` |
| 5 | `budget-structure-periods-lines` | Periods & budget lines | Periodos y líneas de presupuesto | tab 3 → `BudgetLines` |
| 6 | `budget-execution` | Matrix & execution | Matriz y ejecución | tab 4 → `BudgetMatrix` |
| 7 | `bank-accounts` | Bank accounts | Cuentas bancarias | tab 5 → `BankAccounts` |
| 8 | `current-situation` | Current situation | Situación actual | tab 6 → `CurrentSituation` |
| 9 | `dashboard` | Dashboard | Panel | tab 7 → `Dashboard` |
| 10 | `members` | Members | Miembros | tab 8 → `BudgetMembers` (admin-only tab) |

ES labels are taken from the app's own ES strings where one exists (`nav.budgets` = "Presupuestos", `budgetStructure.cycles.title` = "Ciclos", `budgetStructure.budgetLines.title` = "Líneas de presupuesto", `budgetMatrix.title` = "Matriz", `bankAccount.title` = "Cuentas Bancarias", `currentSituation.tabTitle` = "Situación Actual", `dashboard.tabTitle` = "Panel", `budgetStructure.members.tabTitle` = "Miembros") so a Spanish reader sees the same words in the guide and in the UI. Sentence case is normalised for the sidebar.

**Rejected orderings**: (a) alphabetical by slug — puts `auth` first by accident and `members` before `dashboard`, matching nothing the user sees; (b) the `docs/slides/flows/` directory order — that is alphabetical, i.e. the same problem; (c) difficulty/tutorial order — invented, unverifiable, and diverges from the app the moment a tab moves.

**ES prose register**: the existing ES landing copy uses voseo ("Guardá", "Mirá", "podés"). ES chapters follow the same register for consistency with the shipped UI copy — this is a stated convention, not author discretion.

### ADR-UGD-06: Screenshot assets — curated manifest, not bulk copy

**Choice**: `chapters.mjs` carries an explicit `images: []` array per chapter, listing filenames relative to `docs/slides/flows/<slug>/`. `build-guide.mjs` copies exactly those files into `public/guide/assets/<slug>/`. The array is filled by author judgment while writing the chapter — the generator never globs the source directory.

**Rationale**: there are 89 captures; most are duplicate-error and empty-state variants that serve a slide deck's completeness, not a reader's comprehension. Target ~4–6 per chapter (~45–55 total). A blind copy would nearly double the repo's shipped image weight for no reader benefit and would guarantee unreferenced files in `dist/`.

**Enforcement**: `build-guide.mjs` fails (non-zero exit) if (a) a manifest entry has no matching source PNG, or (b) an authored body fragment references an `../assets/...` path that is not in the manifest. Both directions checked — no dangling `<img>`, no orphan copy.

**Sharing**: `assets/` sits above the locale directories precisely so both `en/` and `es/` reference the same physical PNG. The EN-only screenshots in ES pages are the proposal's explicitly accepted tradeoff; the ES `alt` text and captions are translated even though the pixels are not.

**`members` has no `images` entry.** It is the one text-only chapter (no captures exist) — the manifest simply omits the key, and the generator must treat that as valid rather than an error.

### ADR-UGD-07: `render-diagrams.mjs` CLI signature

**Choice**: opt-in flags with defaults identical to today's hardcoded constants.

```
node scripts/render-diagrams.mjs
  [-i | --input    <markdown-file>]   default docs/slides/presentation/flows.md
  [-o | --out-dir  <directory>]       default docs/slides/presentation/diagrams
  [-f | --format   png|svg]           default png
  [-w | --width    <px>]              default 1400
  [-s | --scale    <n>]               default 2   (png only; ignored for svg)
```

Relative paths resolve against **cwd**; the defaults resolve against the script location exactly as today (`path.resolve(__dirname, '../../../docs/...')`), preserving `pnpm render-diagrams` from any directory.

**Default-preservation constraint (proposal risk mitigation)**: with zero arguments, the `execFileSync` argv handed to `@mermaid-js/mermaid-cli` must be *identical* to the current one — `-i <mmd> -o <png> -c <config> -b white -s 2 -w 1400`. This is asserted by a unit test, not by re-reading the code.

**SVG handling**: mermaid-cli infers the renderer from the `-o` extension, so `--format svg` only changes the output filename extension. `-s/--scale` is a raster-only concept and is **omitted** from the argv when the format is `svg`; `-b white` and `-w` are kept (both are meaningful for SVG). Output naming is unchanged otherwise: `NN-<slug>.<ext>`.

**Deliberately not added**: `--config <path>` for the Mermaid theme. `MERMAID_CONFIG` stays an in-file constant; the proposal asks for path args and SVG, nothing more. YAGNI, and it keeps the diff small.

**Testability**: extract `parseArgs(argv)` and `resolveOptions(args)` as named exports and keep `main()` behind an `import.meta.url` entry guard, so `scripts/__tests__/render-diagrams.spec.ts` can assert defaults and flag parsing without spawning `npx`. `scripts/__tests__/export-pptx-pdf.spec.ts` is the existing precedent for testing a `.mjs` script this way.

### ADR-UGD-08: `members` chapter structure

Text-only, six sections, grounded in the shipped components rather than in captions. Sources of truth: `features/budget-structure/views/BudgetMembersView.vue`, `components/budget/InviteUserModal.vue`, `composables/useRoleGate.ts`, `i18n/locales/{en,es}.json` under `budgetStructure.members.*` and `invitation.modal.*`.

| § | Heading (EN / ES) | Grounded in |
|---|---|---|
| 1 | Who can manage members / Quién puede administrar miembros | `useRoleGate` (`isAdmin` = owner\|admin, `isOwner`); the Members tab only renders `v-if="isAdmin"` |
| 2 | Viewing the member list / Ver la lista de miembros | Columns Name / Email / Role / Joined; the owner row is filtered out entirely (`visibleMembers`); "Show deleted" toggle |
| 3 | Changing a member's role / Cambiar el rol de un miembro | Role select → `updateMemberRole`; roles admin / operator / read-only; success and failure toasts |
| 4 | Removing access / Quitar el acceso | "Remove" → confirmation dialog ("They will lose access to this budget") → soft delete, row disappears unless "Show deleted" is on |
| 5 | Restoring access / Restaurar el acceso | "Show deleted" reveals removed members; "Restore" reinstates them with their prior role |
| 6 | Inviting someone / Invitar a alguien | `InviteUserModal`: email field (client-side validation), role select defaulting to **operator**, `POST /api/budgets/{id}/invitations`; documented errors — already a member, owner role not assignable, not permitted |

**Section 1 carries the action matrix**, because it is the single most confusing behaviour and it is the reason buttons vanish:

> An action control is rendered only when **all** of: you are an admin or the owner; the row is not your own; the row is not the owner's; and — unless you are the owner — the row is not another admin's. The same rule applies to removed rows, so a role you cannot change is also a role you cannot restore. The server enforces this independently; the UI only hides what would be refused.

(Frontend gate: `canActOn()` in `BudgetMembersView.vue`; server-side `MemberActionPolicy` is authoritative.)

**Scope fence (proposal risk 1)**: section 6 stops at "the invitation is sent". Invite *acceptance* is a one-sentence cross-link to the `budget-management` chapter, which already carries captures `09-invite-accept-success.png` and `10-invite-accept-error.png`. The `budget-management` chapter must include those two images so the cross-link has a real target — this is a **hard dependency between chapters** that `sdd-tasks` must not split across unmergeable PRs.

**Rejected**: a single flat "Members" page with no subheadings — the parity verification (same headings in both locales) needs headings to compare, and six sections give `sdd-tasks` six natural sub-deliverables.

### ADR-UGD-09: Locale-aware landing link (scoped exception to landing design decision #11)

`features/landing/config/links.ts` currently exports three plain URL constants with an explicit comment that URLs are not translated. The guide is the first target with two genuinely distinct localized artifacts, so the exception the proposal already approved is implemented as narrowly as possible.

**Choice**: `links.ts` gains a *pure function of locale*, not a store dependency.

```ts
import type { SupportedLocale } from '@/stores/locale.store'

// Scoped exception to decision #11 (URLs are not translated): the user guide is
// the first landing target that genuinely ships two localized artifacts. The
// exception is the guide only — REPO_URL / README_URL / DECK_URL stay single-URL.
const GUIDE_PATH_BY_LOCALE: Record<SupportedLocale, string> = {
  en: '/guide/en/',
  es: '/guide/es/',
}

export function guideUrl(locale: SupportedLocale): string {
  return GUIDE_PATH_BY_LOCALE[locale] ?? GUIDE_PATH_BY_LOCALE.en
}
```

**Rationale**: keeping `links.ts` free of Pinia leaves it a trivially unit-testable pure module and keeps the reactivity concern in the component layer, where every other store binding in this feature already lives. Importing `useLocaleStore` into a `config/` module would invert the dependency direction and make the config file untestable without a Pinia instance.

**`LandingLinks.vue`** adds a `<script setup>` block binding and one anchor:

```ts
import { computed } from 'vue'
import { storeToRefs } from 'pinia'
import { useLocaleStore } from '@/stores/locale.store'
import { REPO_URL, README_URL, DECK_URL, guideUrl } from '../config/links'

const { locale } = storeToRefs(useLocaleStore())
const guideHref = computed(() => guideUrl(locale.value))
```

```html
<a data-testid="link-guide" :href="guideHref" target="_blank"
   rel="noopener noreferrer" class="link link-hover text-base-content/70">
  {{ $t('landing.links.guide') }}
</a>
```

`storeToRefs` (not `useLocaleStore().locale`) is what preserves reactivity when the user flips the language switcher without navigating.

**Placement**: third of four — `github, readme, guide, deck`. README and guide are the documentation pair; the deck stays last as the tribunal-specific artifact. `target="_blank"` is kept even though the guide is same-origin, for consistency with its three siblings and because leaving the SPA in place is the desirable behaviour.

**i18n keys** — one key per locale:

| Key | EN | ES |
|---|---|---|
| `landing.links.guide` | `Open the user guide` | `Abrí la guía de usuario` |

EN deliberately avoids "Read the user guide": `landing.links.readme` is already "Read the docs", and two adjacent links both starting "Read the" reads as a duplicate. ES uses voseo to match the surrounding landing copy ("Guardá", "Mirá").

**Existing test impact**: `LandingView.spec.ts:145` queries the three link test-ids individually and does not assert a count, so it stays green. A new case asserts `link-guide` resolves to `/guide/en/` by default and flips to `/guide/es/` after `localeStore.setLocale('es')`.

## Data Flow

Authoring-time (manual, never in CI):

```
scripts/guide/chapters.mjs ── order, slugs, EN/ES labels, images[], published
        │
        ├──► build-guide.mjs ──┬─► for each locale × published chapter:
        │                      │     template.html
        │                      │       + sidebar <ol> rendered from the manifest
        │                      │       + content/<locale>/<slug>.html  (authored prose)
        │                      │       + locale-toggle href ../<other>/<slug>.html
        │                      │     └─► public/guide/<locale>/<slug>.html   [generated, committed]
        │                      │
        │                      ├─► index-body.html ─► public/guide/<locale>/index.html
        │                      │
        │                      └─► copy chapter.images from
        │                            docs/slides/flows/<slug>/ ─► public/guide/assets/<slug>/
        │
        └──► validation pass: every manifest image exists at source;
             every ../assets/ reference in a fragment is in the manifest;
             EN and ES fragment sets are identical → non-zero exit on any failure
```

Runtime (nothing dynamic happens at all):

```
pnpm build → vite copies public/** verbatim → dist/guide/**
           → docker bind-mount /srv/frontend
Browser GET /guide/en/dashboard.html
           → Caddy try_files {path} (file exists) → file_server → static HTML
Browser GET /guide/en/
           → Caddy try_files {path} (directory exists) → file_server → index.html
Landing page → <a href="/guide/en/"> resolved from useLocaleStore().locale
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `scripts/guide/chapters.mjs` | Create | Chapter manifest: order, slug, EN/ES title + label, `images[]`, `published` |
| `scripts/guide/template.html` | Create | Page shell with `{{...}}` slots |
| `scripts/guide/index-body.html` | Create | Body fragment for the per-locale guide home |
| `scripts/guide/content/en/*.html` | Create | 10 authored EN body fragments |
| `scripts/guide/content/es/*.html` | Create | 10 authored ES body fragments |
| `scripts/build-guide.mjs` | Create | Stamper + curated asset copier + validation |
| `scripts/render-diagrams.mjs` | Modify | `parseArgs`/`resolveOptions` exports, CLI flags, SVG output, entry guard |
| `scripts/__tests__/build-guide.spec.ts` | Create | Template/sidebar/toggle/asset-path invariants, manifest validation |
| `scripts/__tests__/render-diagrams.spec.ts` | Create | Default-argv preservation + flag parsing |
| `public/guide/assets/guide.css` | Create | Hand-written shipped stylesheet (~150 lines) |
| `public/guide/{en,es}/index.html` | Create (generated) | Guide home per locale |
| `public/guide/{en,es}/*.html` | Create (generated) | 10 chapters × 2 locales |
| `public/guide/assets/<slug>/*.png` | Create (copied) | Curated subset, ~45–55 files |
| `package.json` | Modify | `"guide:build"`, `"guide:check"` scripts — **not** wired into `build` |
| `src/features/landing/config/links.ts` | Modify | `guideUrl(locale)` + scoped-exception comment |
| `src/features/landing/components/LandingLinks.vue` | Modify | `storeToRefs` binding + guide anchor |
| `src/i18n/locales/{en,es}.json` | Modify | `landing.links.guide` |
| `src/features/landing/__tests__/LandingView.spec.ts` | Modify | Guide-link + locale-reactivity case |
| `src/features/landing/__tests__/guide-links.spec.ts` | Create | Link/parity walker over committed `public/guide/**` |

**Totals**: ~50 authored files (28 of them small prose/config), ~24 generated files, ~55 copied binaries, 5 modified source files.

## Interfaces / Contracts

```js
// scripts/guide/chapters.mjs — the single seam everything else reads
export const GUIDE_TITLE = { en: 'MyBudget User Guide', es: 'Guía de usuario de MyBudget' }
export const LOCALES = ['en', 'es']

/**
 * @typedef {Object} Chapter
 * @property {string}   slug        file name (without .html) AND docs/slides/flows/<slug> dir
 * @property {{en:string, es:string}} label   sidebar text
 * @property {{en:string, es:string}} title   <title> + <h1>
 * @property {string[]} [images]    filenames under docs/slides/flows/<slug>/; omit for text-only
 * @property {boolean}  published   false ⇒ rendered as a dimmed non-link (chained-PR safety)
 */
export const CHAPTERS = [ /* 10 entries, in the ADR-UGD-05 order */ ]
```

```js
// scripts/render-diagrams.mjs — testable surface
export function parseArgs(argv)        // → { input?, outDir?, format?, width?, scale? }
export function resolveOptions(args)   // → { input, outDir, format, width, scale } with defaults
export function buildMermaidArgv(o, mmdPath, outPath, configPath)  // → string[]
```

```ts
// src/features/landing/config/links.ts
export function guideUrl(locale: SupportedLocale): string
```

**Template placeholders** (exhaustive — the generator must reject an unresolved `{{`):
`{{LANG}}`, `{{OTHER_LANG}}`, `{{OTHER_LANG_LABEL}}`, `{{FILENAME}}`, `{{GUIDE_TITLE}}`, `{{CHAPTER_TITLE}}`, `{{NAV_LABEL}}`, `{{SKIP_LABEL}}`, `{{BACK_TO_APP}}`, `{{SIDEBAR}}`, `{{BODY}}`.

## Testing Strategy

| Layer | What to test | Approach |
|-------|--------------|----------|
| Unit — generator | Sidebar marks exactly one `aria-current`; toggle href is `../<other>/<same-file>`; asset refs use `../assets/`; unpublished chapters emit no `<a>`; unresolved placeholder throws | Vitest against `build-guide.mjs` exports with a fixture manifest |
| Unit — manifest validation | Missing source PNG → non-zero exit; fragment referencing an unlisted image → non-zero exit; EN/ES fragment set mismatch → non-zero exit | Vitest, temp dirs |
| Unit — diagrams | Zero-arg argv byte-identical to today's `-b white -s 2 -w 1400`; `--format svg` drops `-s`, changes extension; relative paths resolve against cwd | Vitest, no `npx` spawn |
| Integration — committed output | Walk `public/guide/**`: EN and ES file sets identical; every `<img src>`, sidebar `href`, and toggle target resolves to an existing file; every page has all 10 chapters in the sidebar; heading counts match across locales | Vitest `guide-links.spec.ts` reading the repo tree |
| Component — landing | `link-guide` href is `/guide/en/` by default and `/guide/es/` after `setLocale('es')`; not styled as a button | @testing-library/vue, existing `LandingView.spec.ts` harness |
| Manual gate | `pnpm guide:check` (regenerate to temp, diff against committed) is clean; `pnpm render-diagrams` reproduces the current deck diagrams; `pnpm build` puts `dist/guide/` in place | Pre-merge checklist |

The integration walker is what actually discharges proposal risks 2 and 3 and four of the eight success criteria — it is not optional polish.

## Migration / Rollout

No migration. No schema, no infra, no deploy-config change. `public/guide/` appears in `dist/` on the next `pnpm build`; the existing bind-mount serves it. Rollback is a revert of the merge commits, after which the next build simply omits the tree — matching the proposal's rollback plan exactly.

One operational note for redeploy: the guide is static content inside the frontend image/mount, so it follows the normal `pnpm build` + redeploy path with no extra step.

## PR Slice Forecast

Authored-line estimate (generated `public/guide/**` HTML and copied PNGs excluded as goldens — reproducible from committed inputs and verified by `pnpm guide:check`):

| PR | Scope | Est. authored lines |
|----|-------|---------------------|
| PR1 — Guide infra + pilot chapter | `chapters.mjs` (all 10 entries, only `auth` published), `template.html`, `index-body.html`, `guide.css`, `build-guide.mjs`, `guide:build`/`guide:check` scripts, `render-diagrams.mjs` generalization + its test, `build-guide.spec.ts`, `auth` EN+ES fragments | ~520 |
| PR2 — Core structure chapters | `budget-management` (incl. the two invite-acceptance captures), `budget-structure-cycles`, `budget-structure-categories` — EN+ES | ~290 |
| PR3 — Planning & execution chapters | `budget-structure-periods-lines`, `budget-execution` — EN+ES (largest capture sets) | ~260 |
| PR4 — Reporting chapters | `bank-accounts`, `current-situation`, `dashboard` — EN+ES | ~280 |
| PR5 — Members + landing integration | `members` EN+ES (6 sections, text-only), `links.ts`, `LandingLinks.vue`, i18n keys, `LandingView.spec.ts` case, `guide-links.spec.ts` walker | ~300 |

**Explicit slicing seams** (this is the part `sdd-tasks` should act on):

1. **Infra ⟂ content.** Everything in PR1 is mechanical and reviewable without reading prose. Everything after it is prose + one manifest line. Never mix a template/CSS change into a content PR.
2. **`published` flag is the chaining mechanism.** All 10 chapters exist in the manifest from PR1; unpublished ones render as dimmed non-links. No intermediate PR ever ships a 404 in the sidebar, so each PR is independently mergeable and demoable.
3. **Chapter granularity is the atom.** One chapter = one EN fragment + one ES fragment + one manifest edit + its asset entries, always in the same PR. Splitting EN from ES would defeat the parity verification and is forbidden.
4. **`members` has six sub-sections** (ADR-UGD-08) if PR5 needs further splitting, but its cross-link target (`budget-management` invite-acceptance captures) must already be merged — hence PR2 before PR5.
5. **Landing integration is deliberately last.** `main` should not advertise a guide that is 3 chapters deep. It is also the only PR that touches `src/`, so it is the only one that can break the app build — keeping it isolated makes that failure mode obvious.

**800-line budget risk**: PR1 is the only one near the ceiling (~520 authored) and it is the least ambiguous diff. **High confidence all five PRs stay under 800 authored lines** — but *only* under the generated-goldens accounting of ADR-UGD-01. If a reviewer counts generated HTML as authored, PR1 alone lands near 1,400 and every content PR roughly doubles; in that accounting the change needs ~9 PRs.

**Chained PRs recommended**: Yes — `feat/user-guide-docs` as the integration branch, five child PRs in the order above.

**Decision needed before apply**: One — confirm that `public/guide/**` is treated as generated goldens for review-budget purposes. Everything else in this design is settled.

## Known Decisions

- Build-time stamper chosen over hand-copied pages (ADR-UGD-01); the shipped artifact is unchanged, only the authoring surface differs. This is *not* the "JS partials" fix the proposal ruled out.
- The guide is **22 files, not 20** — a per-locale `index.html` is required for `/guide/en/` to resolve under the existing Caddyfile (ADR-UGD-04).
- Asset references are `../assets/<area>/<file>.png` (one `..`, not two).
- Tailwind/DaisyUI cannot be used — `public/` bypasses the Vite pipeline; `guide.css` is standalone.
- `build-guide.mjs` must never be added to the `build` script; manual-regenerate posture, same as `build-showcase.mjs`.
- Chapter order mirrors `BudgetTabs.vue`, prefixed by `auth`; ES sidebar labels reuse the app's own ES strings.
- ES prose uses voseo, matching the existing ES landing copy.
- `guideUrl(locale)` is a pure function in `links.ts`; the Pinia binding lives in `LandingLinks.vue` via `storeToRefs`.
- EN link label is "Open the user guide" to avoid colliding with the existing "Read the docs".
- `--config` for the Mermaid theme is explicitly deferred; only path args and SVG are in scope.
