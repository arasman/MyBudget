// Curated 9-tile flow showcase for the landing page. One representative
// screenshot per feature area — the count is fixed at 9 for the grid rhythm
// (design.md decision #9); the specific source PNG may be re-curated without
// changing the count.
//
// `source` mirrors scripts/build-showcase.mjs's ITEMS list (docs/slides/flows/
// relative path). `slug` resolves the generated asset at
// public/showcase/{slug}-{640,1280}.webp. Keep both lists in sync by hand —
// neither is generated from the other.
export interface ShowcaseItem {
  /** public/showcase/{slug}-{640,1280}.webp */
  slug: string
  /** docs/slides/flows/{source} — the curated source PNG */
  source: string
  /** i18n key prefix for this tile's title + caption, e.g. 'landing.showcase.dashboard' */
  i18nKey: string
}

export const SHOWCASE_ITEMS: ShowcaseItem[] = [
  { slug: 'auth', source: 'auth/08-logout-menu.png', i18nKey: 'landing.showcase.auth' },
  {
    slug: 'bank-accounts',
    source: 'bank-accounts/03-create-success.png',
    i18nKey: 'landing.showcase.bankAccounts',
  },
  {
    slug: 'budget-execution',
    source: 'budget-execution/06-matrix-updated.png',
    i18nKey: 'landing.showcase.budgetExecution',
  },
  {
    slug: 'budget-management',
    source: 'budget-management/01-budget-list.png',
    i18nKey: 'landing.showcase.budgetManagement',
  },
  {
    slug: 'budget-structure-categories',
    source: 'budget-structure-categories/06-create-category-success.png',
    i18nKey: 'landing.showcase.budgetStructureCategories',
  },
  {
    slug: 'budget-structure-cycles',
    source: 'budget-structure-cycles/07-set-active-success.png',
    i18nKey: 'landing.showcase.budgetStructureCycles',
  },
  {
    slug: 'budget-structure-periods-lines',
    source: 'budget-structure-periods-lines/14-line-edit-success.png',
    i18nKey: 'landing.showcase.budgetStructurePeriodsLines',
  },
  {
    slug: 'current-situation',
    source: 'current-situation/04-save-success.png',
    i18nKey: 'landing.showcase.currentSituation',
  },
  { slug: 'dashboard', source: 'dashboard/01-lifetime-trend.png', i18nKey: 'landing.showcase.dashboard' },
]
