// Resizes the 9 curated docs/slides/flows/* PNGs into WebP showcase tiles at
// public/showcase/{slug}-{640,1280}.webp. The source PNGs live outside the
// Vite root (would need server.fs.allow) and are multi-MB, so this script —
// not a Vite import — is the pipeline (design.md decision #10). Manual-
// regenerate posture, same as build-pptx.mjs / render-diagrams.mjs: never
// run in CI/deploy, re-run by hand when the curated source list changes.
//
// This list mirrors features/landing/config/showcase.ts (slug + source) —
// that file also carries the i18nKey used by the Vue layer, which this
// script does not need. Keep both lists in sync by hand when the curation
// changes; neither is generated from the other.
//
// Run with: pnpm build-showcase
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(__dirname, '../../..')
const FLOWS = path.join(REPO_ROOT, 'docs/slides/flows')
const OUT = path.join(__dirname, '../public/showcase')

const WIDTHS = [640, 1280]

/** slug -> public/showcase/{slug}-{width}.webp ; source -> docs/slides/flows/{source} */
const ITEMS = [
  { slug: 'auth', source: 'auth/08-logout-menu.png' },
  { slug: 'bank-accounts', source: 'bank-accounts/03-create-success.png' },
  { slug: 'budget-execution', source: 'budget-execution/06-matrix-updated.png' },
  { slug: 'budget-management', source: 'budget-management/01-budget-list.png' },
  { slug: 'budget-structure-categories', source: 'budget-structure-categories/06-create-category-success.png' },
  { slug: 'budget-structure-cycles', source: 'budget-structure-cycles/07-set-active-success.png' },
  { slug: 'budget-structure-periods-lines', source: 'budget-structure-periods-lines/14-line-edit-success.png' },
  { slug: 'current-situation', source: 'current-situation/04-save-success.png' },
  { slug: 'dashboard', source: 'dashboard/01-lifetime-trend.png' },
]

async function buildOne({ slug, source }) {
  const inputPath = path.join(FLOWS, source)
  if (!fs.existsSync(inputPath)) {
    console.error(`Missing source PNG for "${slug}": ${inputPath}`)
    process.exitCode = 1
    return
  }

  for (const width of WIDTHS) {
    const outPath = path.join(OUT, `${slug}-${width}.webp`)
    await sharp(inputPath)
      .resize({ width, withoutEnlargement: true })
      .webp({ quality: 82 })
      .toFile(outPath)
    console.log(`Wrote ${path.relative(REPO_ROOT, outPath)}`)
  }
}

async function main() {
  fs.mkdirSync(OUT, { recursive: true })
  for (const item of ITEMS) {
    await buildOne(item)
  }
  if (process.exitCode) {
    console.error('\nbuild-showcase finished with errors — see above.')
  } else {
    console.log(`\nWrote ${ITEMS.length * WIDTHS.length} WebP files to ${path.relative(REPO_ROOT, OUT)}`)
  }
}

await main()
