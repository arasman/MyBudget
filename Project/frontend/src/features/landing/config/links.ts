// Outbound evaluation links for the landing page. Plain string consts, not
// i18n keys — URLs aren't translated (design.md decision #11).
//
// Deck link points at the PDF: PR 0's trial (tasks.md 0.8, export-pptx-pdf.mjs)
// succeeded — docs/slides/presentation/MyBudget.pdf is committed. If that ever
// regresses, fall back to the .pptx path instead (LANDING-5).
const GITHUB_REPO_URL = 'https://github.com/arasman/MyBudget'
const GITHUB_DEFAULT_BRANCH = 'main'

export const REPO_URL = GITHUB_REPO_URL
export const README_URL = `${GITHUB_REPO_URL}/blob/${GITHUB_DEFAULT_BRANCH}/README.md`
export const DECK_URL = `${GITHUB_REPO_URL}/blob/${GITHUB_DEFAULT_BRANCH}/docs/slides/presentation/MyBudget.pdf`
