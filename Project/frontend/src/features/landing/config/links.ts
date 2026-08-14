// Outbound evaluation links for the landing page. Plain string consts, not
// i18n keys — URLs aren't translated (design.md decision #11).
//
// Deck link points at the PDF: PR 0's trial (tasks.md 0.8, export-pptx-pdf.mjs)
// succeeded — docs/slides/presentation/MyBudget.pdf is committed. If that ever
// regresses, fall back to the .pptx path instead (LANDING-5).
import type { SupportedLocale } from '@/stores/locale.store'

const GITHUB_REPO_URL = 'https://github.com/arasman/MyBudget'
const GITHUB_DEFAULT_BRANCH = 'main'

export const REPO_URL = GITHUB_REPO_URL
export const README_URL = `${GITHUB_REPO_URL}/blob/${GITHUB_DEFAULT_BRANCH}/README.md`
export const DECK_URL = `${GITHUB_REPO_URL}/blob/${GITHUB_DEFAULT_BRANCH}/docs/slides/presentation/MyBudget.pdf`

// ADR-UGD-09: scoped exception to the "URLs are not translated" convention above (design.md
// decision #11). The user guide is the first landing target that genuinely ships two distinct
// localized artifacts (public/guide/en/ vs public/guide/es/), so — and only for the guide —
// the URL itself depends on locale. Kept as a pure function of locale, not a store import, so
// this config module stays trivially unit-testable without a Pinia instance; the reactive
// binding to the active locale lives in LandingLinks.vue via storeToRefs.
//
// Explicit `index.html` filename (not a bare `/guide/en/` directory): Caddy's `file_server`
// resolves a directory request to its `index.html` in production, but `vite dev`'s static
// middleware does not — an unmatched directory path falls through to the SPA's own
// `index.html` instead, rendering a blank screen (no matching Vue route). An explicit filename
// resolves identically as a real static file in every environment (dev, preview, Caddy).
const GUIDE_PATH_BY_LOCALE: Record<SupportedLocale, string> = {
  en: '/guide/en/index.html',
  es: '/guide/es/index.html',
}

export function guideUrl(locale: SupportedLocale): string {
  return GUIDE_PATH_BY_LOCALE[locale] ?? GUIDE_PATH_BY_LOCALE.en
}
