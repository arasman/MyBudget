// Exports docs/slides/presentation/MyBudget.pptx to PDF via PowerPoint COM automation
// (PowerShell, ppSaveAsPDF = 32). Windows + PowerPoint only — never run in CI/deploy,
// same posture as render-diagrams.mjs / e2e:slides.
//
// Preflight: platform must be win32 and the input .pptx must exist.
// Postflight: the output PDF must exist and be non-zero bytes, or this reports
// failure rather than a silent success (threat matrix: Subprocess — PowerPoint COM,
// openspec/changes/landing-page-and-visual-polish/design.md). On failure, the deck
// link falls back to the .pptx file (LANDING-5).
//
// Run with: pnpm export-pptx-pdf (after pnpm build-pptx).
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(__dirname, '../../..')
const INPUT = path.join(REPO_ROOT, 'docs/slides/presentation/MyBudget.pptx')
const OUTPUT = path.join(REPO_ROOT, 'docs/slides/presentation/MyBudget.pdf')

const PP_SAVE_AS_PDF = 32

/** Pure guard — no I/O. Testable without spawning PowerShell. */
export function checkPreconditions({ platform, inputExists }) {
  if (platform !== 'win32') {
    return {
      ok: false,
      reason: `export-pptx-pdf requires Windows + PowerPoint COM automation; current platform is "${platform}". Link the .pptx file directly instead (LANDING-5 fallback).`,
    }
  }
  if (!inputExists) {
    return {
      ok: false,
      reason: `Input file not found: ${INPUT}. Run "pnpm build-pptx" first.`,
    }
  }
  return { ok: true }
}

/** Pure guard — no I/O. A zero-byte/absent output is always a failure, never a silent success. */
export function checkOutput({ exists, size }) {
  if (!exists || size <= 0) {
    return {
      ok: false,
      reason: `Export produced no usable output (exists=${exists}, size=${size ?? 0}). Falling back to the .pptx link (LANDING-5).`,
    }
  }
  return { ok: true }
}

/**
 * Open(readOnly) -> SaveAs(out, 32) -> Close()/Quit(), always in try/finally so a
 * failed SaveAs never leaves an orphaned POWERPNT.EXE process behind.
 */
function buildPowerShellScript(inputPath, outputPath) {
  return [
    '$ErrorActionPreference = "Stop"',
    '$ppt = New-Object -ComObject PowerPoint.Application',
    '$pres = $null',
    'try {',
    `  $pres = $ppt.Presentations.Open("${inputPath}", $true, $false, $false)`,
    `  $pres.SaveAs("${outputPath}", ${PP_SAVE_AS_PDF})`,
    '} finally {',
    '  if ($pres) { $pres.Close() }',
    '  $ppt.Quit()',
    '}',
  ].join('\n')
}

function runPowerShellExport({ inputPath, outputPath, execFileSyncFn }) {
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'export-pptx-pdf-'))
  const scriptPath = path.join(tmpDir, 'export.ps1')
  try {
    fs.writeFileSync(scriptPath, buildPowerShellScript(inputPath, outputPath))
    // argv array, no shell string interpolation — inputPath/outputPath are
    // module-relative constants, never user input.
    execFileSyncFn('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', scriptPath], {
      stdio: 'inherit',
    })
  } finally {
    fs.rmSync(tmpDir, { recursive: true, force: true })
  }
}

export function runExport({
  platform = process.platform,
  inputPath = INPUT,
  outputPath = OUTPUT,
  existsSync = fs.existsSync,
  statSync = fs.statSync,
  execFileSyncFn = execFileSync,
} = {}) {
  const pre = checkPreconditions({ platform, inputExists: existsSync(inputPath) })
  if (!pre.ok) {
    console.error(pre.reason)
    return pre
  }

  runPowerShellExport({ inputPath, outputPath, execFileSyncFn })

  const exists = existsSync(outputPath)
  const size = exists ? statSync(outputPath).size : 0
  const post = checkOutput({ exists, size })
  if (!post.ok) {
    console.error(post.reason)
    return post
  }

  console.log(`Wrote ${outputPath} (${size} bytes)`)
  return { ok: true }
}

const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) {
  const result = runExport()
  process.exit(result.ok ? 0 : 1)
}
