// Extracts each ```mermaid fenced block from docs/slides/presentation/flows.md,
// in order, and renders it to a numbered PNG in docs/slides/presentation/diagrams/.
// flows.md stays the single editable source — the PNGs are generated output.
// Run with: pnpm render-diagrams
import fs from 'node:fs'
import path from 'node:path'
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const FLOWS_MD = path.resolve(__dirname, '../../../docs/slides/presentation/flows.md')
const DIAGRAMS_DIR = path.resolve(__dirname, '../../../docs/slides/presentation/diagrams')
const TMP_DIR = path.resolve(__dirname, '../.tmp-diagrams')

const MERMAID_CONFIG = {
  theme: 'base',
  themeVariables: {
    primaryColor: '#ede9fe',
    primaryBorderColor: '#7c3aed',
    primaryTextColor: '#1f2937',
    lineColor: '#6b7280',
    background: '#ffffff',
    fontFamily: 'Arial, sans-serif',
  },
}

function extractDiagrams(markdown) {
  const blocks = []
  const regex = /^##\s+(\d+)\.\s+(.+)$[\s\S]*?```mermaid\n([\s\S]*?)```/gm
  let match
  while ((match = regex.exec(markdown)) !== null) {
    const [, num, title, code] = match
    blocks.push({ num: num.padStart(2, '0'), title: title.trim(), code: code.trim() })
  }
  return blocks
}

function slugify(title) {
  return title
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/(^-|-$)/g, '')
}

function main() {
  const markdown = fs.readFileSync(FLOWS_MD, 'utf-8')
  const diagrams = extractDiagrams(markdown)

  if (diagrams.length === 0) {
    console.error(`No mermaid blocks found in ${FLOWS_MD}`)
    process.exit(1)
  }

  fs.mkdirSync(DIAGRAMS_DIR, { recursive: true })
  fs.mkdirSync(TMP_DIR, { recursive: true })

  const configPath = path.join(TMP_DIR, 'mermaid-config.json')
  fs.writeFileSync(configPath, JSON.stringify(MERMAID_CONFIG, null, 2))

  for (const { num, title, code } of diagrams) {
    const slug = slugify(title)
    const mmdPath = path.join(TMP_DIR, `${num}-${slug}.mmd`)
    const pngPath = path.join(DIAGRAMS_DIR, `${num}-${slug}.png`)
    fs.writeFileSync(mmdPath, code)

    console.log(`Rendering ${num}-${slug}.png ...`)
    execFileSync(
      'npx',
      [
        '--yes',
        '@mermaid-js/mermaid-cli',
        '-i', mmdPath,
        '-o', pngPath,
        '-c', configPath,
        '-b', 'white',
        '-s', '2',
        '-w', '1400',
      ],
      { stdio: 'inherit', shell: true },
    )
  }

  fs.rmSync(TMP_DIR, { recursive: true, force: true })
  console.log(`\nWrote ${diagrams.length} diagrams to ${DIAGRAMS_DIR}`)
}

main()
