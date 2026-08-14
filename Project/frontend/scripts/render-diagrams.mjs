// Extracts each ```mermaid fenced block from a source markdown file, in order, and
// renders it to a numbered PNG (or SVG) in an output directory. The source file stays
// the single editable input — the rendered images are generated output.
//
// Run with: pnpm render-diagrams
//   [-i | --input    <markdown-file>]   default docs/slides/presentation/flows.md
//   [-o | --out-dir  <directory>]       default docs/slides/presentation/diagrams
//   [-f | --format   png|svg]           default png
//   [-w | --width    <px>]              default 1400
//   [-s | --scale    <n>]               default 2   (png only; ignored for svg)
//
// Default-preservation constraint (design.md ADR-UGD-07): a zero-argument invocation
// must produce an argv identical to this script's pre-generalization behavior —
// `-i <mmd> -o <png> -c <config> -b white -s 2 -w 1400` — asserted by
// scripts/__tests__/render-diagrams.spec.ts, not by re-reading this file.
import fs from 'node:fs'
import path from 'node:path'
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const DEFAULT_INPUT = path.resolve(__dirname, '../../../docs/slides/presentation/flows.md')
const DEFAULT_OUT_DIR = path.resolve(__dirname, '../../../docs/slides/presentation/diagrams')
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

/** Pure — no I/O. Maps CLI argv to a flat options bag; unknown flags are ignored. */
export function parseArgs(argv) {
  const args = {}
  for (let i = 0; i < argv.length; i++) {
    switch (argv[i]) {
      case '-i':
      case '--input':
        args.input = argv[++i]
        break
      case '-o':
      case '--out-dir':
        args.outDir = argv[++i]
        break
      case '-f':
      case '--format':
        args.format = argv[++i]
        break
      case '-w':
      case '--width':
        args.width = argv[++i]
        break
      case '-s':
      case '--scale':
        args.scale = argv[++i]
        break
      default:
        break
    }
  }
  return args
}

/**
 * Pure — no I/O. Applies defaults identical to the pre-generalization hardcoded
 * constants. Relative paths resolve against `cwd` (default `process.cwd()`), not
 * `__dirname` — the *defaults* still resolve against the script location so
 * `pnpm render-diagrams` keeps working from any directory (design.md ADR-UGD-07).
 */
export function resolveOptions(args, { cwd = process.cwd() } = {}) {
  const input = args.input ? path.resolve(cwd, args.input) : DEFAULT_INPUT
  const outDir = args.outDir ? path.resolve(cwd, args.outDir) : DEFAULT_OUT_DIR
  const format = args.format === 'svg' ? 'svg' : 'png'
  const width = args.width ? Number(args.width) : 1400
  const scale = args.scale ? Number(args.scale) : 2
  return { input, outDir, format, width, scale }
}

/**
 * Pure — no I/O. Builds the exact argv passed to `@mermaid-js/mermaid-cli`.
 * `-s/--scale` is a raster-only concept and is omitted for `format: 'svg'`.
 */
export function buildMermaidArgv(options, mmdPath, outPath, configPath) {
  const argv = ['-i', mmdPath, '-o', outPath, '-c', configPath, '-b', 'white']
  if (options.format !== 'svg') {
    argv.push('-s', String(options.scale))
  }
  argv.push('-w', String(options.width))
  return argv
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
  const args = parseArgs(process.argv.slice(2))
  const options = resolveOptions(args)
  const ext = options.format === 'svg' ? 'svg' : 'png'

  const markdown = fs.readFileSync(options.input, 'utf-8')
  const diagrams = extractDiagrams(markdown)

  if (diagrams.length === 0) {
    console.error(`No mermaid blocks found in ${options.input}`)
    process.exit(1)
  }

  fs.mkdirSync(options.outDir, { recursive: true })
  fs.mkdirSync(TMP_DIR, { recursive: true })

  const configPath = path.join(TMP_DIR, 'mermaid-config.json')
  fs.writeFileSync(configPath, JSON.stringify(MERMAID_CONFIG, null, 2))

  for (const { num, title, code } of diagrams) {
    const slug = slugify(title)
    const mmdPath = path.join(TMP_DIR, `${num}-${slug}.mmd`)
    const outPath = path.join(options.outDir, `${num}-${slug}.${ext}`)
    fs.writeFileSync(mmdPath, code)

    console.log(`Rendering ${num}-${slug}.${ext} ...`)
    execFileSync(
      'npx',
      ['--yes', '@mermaid-js/mermaid-cli', ...buildMermaidArgv(options, mmdPath, outPath, configPath)],
      { stdio: 'inherit', shell: true },
    )
  }

  fs.rmSync(TMP_DIR, { recursive: true, force: true })
  console.log(`\nWrote ${diagrams.length} diagrams to ${options.outDir}`)
}

// Entry guard (design.md ADR-UGD-07 testability): only run when invoked directly,
// never as a side effect of another module importing this file (e.g. its test file).
const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) {
  main()
}
