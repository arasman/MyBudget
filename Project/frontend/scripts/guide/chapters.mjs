// The single manifest for the bilingual static user guide: chapter order, slugs,
// EN/ES labels, curated screenshot lists, and publication state. `build-guide.mjs`
// is the only consumer; nothing here is imported by the Vue app (design.md
// "Technical Approach" — chapters.mjs is the one seam sdd-tasks slices against).
//
// `published: false` chapters render in the sidebar as a dimmed, non-clickable
// entry (never a 404) so every PR in the chained-PR delivery stays independently
// mergeable and demoable (design.md ADR-UGD-01 / PR Slice Forecast, seam #2).
// Order mirrors BudgetTabs.vue, prefixed by the pre-login `auth` flow (ADR-UGD-05).

export const GUIDE_TITLE = {
  en: 'MyBudget User Guide',
  es: 'Guía de usuario de MyBudget',
}

export const LOCALES = ['en', 'es']

export const OTHER_LOCALE = { en: 'es', es: 'en' }

export const LOCALE_LABEL = { en: 'English', es: 'Español' }

// Small localized UI strings for the shell (nav/skip/back-link/index intro).
// Lives here, not in template.html, because chapters.mjs is the single seam.
export const UI_STRINGS = {
  en: {
    navLabel: 'Guide navigation',
    skipLabel: 'Skip to content',
    backToApp: 'Back to the app',
    indexIntro:
      'This guide walks through every area of MyBudget, one page per feature. Start with ' +
      'account access, then move through budgets, cycles, categories, execution, accounts, ' +
      'and reporting in the order you would use them.',
  },
  es: {
    navLabel: 'Navegación de la guía',
    skipLabel: 'Saltar al contenido',
    backToApp: 'Volver a la app',
    indexIntro:
      'Esta guía recorre cada área de MyBudget, una página por funcionalidad. Empezá por el ' +
      'acceso a la cuenta y seguí por presupuestos, ciclos, categorías, ejecución, cuentas y ' +
      'reportes en el orden en que los vas a usar.',
  },
}

/**
 * @typedef {Object} Chapter
 * @property {string}   slug        file name (without .html) AND docs/slides/flows/<slug> dir
 * @property {{en:string, es:string}} label   sidebar text; also <title>/<h1> unless `title` is set
 * @property {{en:string, es:string}} [title] overrides `label` for <title>/<h1> when they must differ
 * @property {string[]} [images]    filenames under docs/slides/flows/<slug>/; omit for text-only
 * @property {boolean}  published   false => rendered as a dimmed non-link (chained-PR safety)
 */

/** @type {Chapter[]} */
export const CHAPTERS = [
  {
    slug: 'auth',
    label: { en: 'Account & sign-in', es: 'Cuenta e inicio de sesión' },
    images: [
      '01-register-empty.png',
      '02-register-filled.png',
      '03-register-success.png',
      '06-login-success.png',
      '07-login-invalid-error.png',
      '09-logout-success.png',
    ],
    published: true,
  },
  {
    slug: 'budget-management',
    label: { en: 'Budgets', es: 'Presupuestos' },
    images: [
      '01-budget-list.png',
      '02-create-form.png',
      '04-create-success.png',
      '06-delete-success.png',
      '09-invite-accept-success.png',
      '10-invite-accept-error.png',
    ],
    published: true,
  },
  {
    slug: 'budget-structure-cycles',
    label: { en: 'Cycles', es: 'Ciclos' },
    images: [
      '01-list-empty.png',
      '02-create-form.png',
      '03-create-success.png',
      '06-edit-success.png',
      '07-set-active-success.png',
      '09-delete-success.png',
    ],
    published: true,
  },
  {
    slug: 'budget-structure-categories',
    label: { en: 'Categories', es: 'Categorías' },
    images: [
      '01-list-empty.png',
      '02-create-group-form.png',
      '03-create-group-success.png',
      '05-create-category-form.png',
      '06-create-category-success.png',
      '10-restore-category-success.png',
    ],
    published: true,
  },
  {
    slug: 'budget-structure-periods-lines',
    label: { en: 'Periods & budget lines', es: 'Periodos y líneas de presupuesto' },
    published: false,
  },
  { slug: 'budget-execution', label: { en: 'Matrix & execution', es: 'Matriz y ejecución' }, published: false },
  { slug: 'bank-accounts', label: { en: 'Bank accounts', es: 'Cuentas bancarias' }, published: false },
  { slug: 'current-situation', label: { en: 'Current situation', es: 'Situación actual' }, published: false },
  { slug: 'dashboard', label: { en: 'Dashboard', es: 'Panel' }, published: false },
  { slug: 'members', label: { en: 'Members', es: 'Miembros' }, published: false },
]
