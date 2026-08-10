// Regenerates docs/slides/flows/<flow>/index.md and docs/slides/flows/index.md
// from the .manifest.json files written by Playwright screenshot specs
// (e2e/screenshots/). Run with: pnpm slides:index (after pnpm e2e:slides).
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const SLIDES_ROOT = path.resolve(__dirname, '../../../docs/slides/flows')

// Narrative order for the root index — flows not listed here are appended
// alphabetically at the end.
const FLOW_ORDER = [
  'auth',
  'budget-management',
  'budget-structure-cycles',
  'budget-structure-periods-lines',
  'budget-structure-categories',
  'budget-execution',
  'current-situation',
  'dashboard',
  'bank-accounts',
]

function readManifest(flow) {
  const manifestPath = path.join(SLIDES_ROOT, flow, '.manifest.json')
  if (!fs.existsSync(manifestPath)) return null
  const entries = JSON.parse(fs.readFileSync(manifestPath, 'utf-8'))
  return [...entries].sort((a, b) => a.seq - b.seq)
}

function writeFlowIndex(flow, entries) {
  const lines = [
    `# ${flow}`,
    '',
    '| # | Image | Title | Description |',
    '|---|-------|-------|-------------|',
    ...entries.map(
      (e) => `| ${e.seq} | ![${e.title}](./${e.file}) | ${e.title} | ${e.description} |`,
    ),
    '',
  ]
  fs.writeFileSync(path.join(SLIDES_ROOT, flow, 'index.md'), lines.join('\n'))
}

function main() {
  if (!fs.existsSync(SLIDES_ROOT)) {
    console.error(`No slides found at ${SLIDES_ROOT} — run "pnpm e2e:slides" first.`)
    process.exit(1)
  }

  const flowDirs = fs
    .readdirSync(SLIDES_ROOT, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => d.name)

  const ordered = [
    ...FLOW_ORDER.filter((f) => flowDirs.includes(f)),
    ...flowDirs.filter((f) => !FLOW_ORDER.includes(f)).sort(),
  ]

  const rootLines = [
    '# MyBudget — Slide Screenshot Index',
    '',
    'Generated from Playwright E2E screenshot specs (`e2e/screenshots/`). Regenerate with `pnpm slides:index`.',
    'Build the slide deck by walking each flow in order below; each flow folder has its own `index.md` with per-image titles and descriptions.',
    '',
  ]

  let anyFlow = false
  for (const flow of ordered) {
    const entries = readManifest(flow)
    if (!entries) {
      rootLines.push(`## ${flow} — not generated yet`)
      rootLines.push('')
      continue
    }
    anyFlow = true
    writeFlowIndex(flow, entries)
    rootLines.push(`## [${flow}](./${flow}/index.md) (${entries.length} images)`)
    if (entries[0]) rootLines.push(`${entries[0].title} → ${entries[entries.length - 1].title}`)
    rootLines.push('')
  }

  fs.writeFileSync(path.join(SLIDES_ROOT, 'index.md'), rootLines.join('\n'))

  if (!anyFlow) {
    console.warn('No .manifest.json files found — run "pnpm e2e:slides" first.')
  } else {
    console.log(`Wrote ${path.join(SLIDES_ROOT, 'index.md')}`)
  }
}

main()
