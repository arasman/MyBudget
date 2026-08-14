// Stamps the bilingual static user guide (public/guide/**) from a shared template +
// per-chapter authored fragments + one manifest (scripts/guide/chapters.mjs).
// Committed generator, committed output, never run in CI or `pnpm build` — same
// manual-regenerate posture as build-showcase.mjs / render-diagrams.mjs / build-pptx.mjs
// (design.md ADR-UGD-01). Re-run by hand with `pnpm guide:build` whenever a chapter,
// the manifest, or the template changes; verify with `pnpm guide:check`.
import fs from 'node:fs'
import path from 'node:path'
import os from 'node:os'
import { fileURLToPath } from 'node:url'
import { GUIDE_TITLE, LOCALES, OTHER_LOCALE, LOCALE_LABEL, UI_STRINGS, CHAPTERS } from './guide/chapters.mjs'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(__dirname, '../../..')
const GUIDE_SRC = path.join(__dirname, 'guide')
const CONTENT_DIR = path.join(GUIDE_SRC, 'content')
const TEMPLATE_PATH = path.join(GUIDE_SRC, 'template.html')
const INDEX_BODY_PATH = path.join(GUIDE_SRC, 'index-body.html')
const FLOWS_DIR = path.join(REPO_ROOT, 'docs/slides/flows')
const OUT_DIR = path.resolve(__dirname, '../public/guide')

// ---------------------------------------------------------------------------
// Pure functions — no filesystem I/O. Covered directly by
// scripts/__tests__/build-guide.spec.ts (design.md Testing Strategy).
// ---------------------------------------------------------------------------

/** Escapes plain text for safe inclusion in HTML text nodes / attribute values. */
export function escapeHtml(text) {
  return text
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
}

/** Renders the sidebar/chapter-list <li> items for one locale + current page. */
export function renderSidebar({ chapters, locale, currentSlug }) {
  const items = chapters.map((chapter) => {
    const label = escapeHtml(chapter.label[locale])
    if (chapter.slug === currentSlug) {
      return `      <li><span aria-current="page">${label}</span></li>`
    }
    if (!chapter.published) {
      return `      <li><span class="disabled">${label}</span></li>`
    }
    return `      <li><a href="${chapter.slug}.html">${label}</a></li>`
  })
  return items.join('\n')
}

/** Same-filename, sibling-locale href (design.md ADR-UGD-04). */
export function localeToggleHref(locale, filename) {
  return `../${OTHER_LOCALE[locale]}/${filename}`
}

/** Replaces every {{KEY}} token; throws on any leftover unresolved placeholder. */
export function fillTemplate(template, values) {
  let out = template
  for (const [key, value] of Object.entries(values)) {
    out = out.replaceAll(`{{${key}}}`, value)
  }
  const unresolved = out.match(/\{\{[A-Z_]+\}\}/)
  if (unresolved) {
    throw new Error(`Unresolved template placeholder: ${unresolved[0]}`)
  }
  return out
}

/** Every <img src="..."> value found in a body fragment, in document order. */
export function extractImgSrcs(html) {
  return [...html.matchAll(/<img\b[^>]*\ssrc="([^"]+)"/g)].map((m) => m[1])
}

/**
 * Validates one asset `src` against the `../assets/<area>/<file>` convention
 * (design.md ADR-UGD-04). `../../assets/...` is a distinct, explicit failure —
 * it is NOT silently treated as valid just because it contains the substring
 * "../assets/".
 */
export function validateAssetPath(src) {
  if (src.startsWith('../../assets/')) {
    return {
      ok: false,
      reason: `asset path must be "../assets/...", found "../../assets/..." in "${src}"`,
    }
  }
  if (!src.startsWith('../assets/')) {
    return { ok: false, reason: `asset path must start with "../assets/", found "${src}"` }
  }
  return { ok: true, ref: src.slice('../assets/'.length) }
}

/**
 * Both validation directions from design.md ADR-UGD-06:
 *   (a) every manifest images[] entry has a matching source PNG
 *   (b) every ../assets/... reference in an authored fragment is in the manifest
 * A chapter with no `images` key (e.g. `members`) is valid, not an error.
 */
export function validateManifest({ chapters, locales, readFragment, sourceExists }) {
  const errors = []
  for (const chapter of chapters) {
    const images = chapter.images ?? []
    for (const file of images) {
      if (!sourceExists(chapter.slug, file)) {
        errors.push(`Missing source image for "${chapter.slug}": docs/slides/flows/${chapter.slug}/${file}`)
      }
    }
    for (const locale of locales) {
      const html = readFragment(locale, chapter.slug)
      if (html == null) continue
      for (const src of extractImgSrcs(html)) {
        const check = validateAssetPath(src)
        if (!check.ok) {
          errors.push(`content/${locale}/${chapter.slug}.html: ${check.reason}`)
          continue
        }
        const [area, file] = check.ref.split('/')
        if (area !== chapter.slug || !images.includes(file)) {
          errors.push(
            `content/${locale}/${chapter.slug}.html references "../assets/${check.ref}" not listed in "${chapter.slug}".images[]`,
          )
        }
      }
    }
  }
  return errors
}

/** Every published chapter must have a fragment in every locale (EN/ES parity). */
export function validateLocaleParity({ chapters, locales, fragmentExists }) {
  const errors = []
  for (const chapter of chapters.filter((c) => c.published)) {
    for (const locale of locales) {
      if (!fragmentExists(locale, chapter.slug)) {
        errors.push(`Missing content/${locale}/${chapter.slug}.html (EN/ES fragment set mismatch)`)
      }
    }
  }
  return errors
}

// ---------------------------------------------------------------------------
// I/O orchestration
// ---------------------------------------------------------------------------

function readFragmentFs(locale, slug) {
  const p = path.join(CONTENT_DIR, locale, `${slug}.html`)
  return fs.existsSync(p) ? fs.readFileSync(p, 'utf-8') : null
}

function sourceExistsFs(slug, file) {
  return fs.existsSync(path.join(FLOWS_DIR, slug, file))
}

function runValidation() {
  const errors = [
    ...validateManifest({
      chapters: CHAPTERS,
      locales: LOCALES,
      readFragment: readFragmentFs,
      sourceExists: sourceExistsFs,
    }),
    ...validateLocaleParity({
      chapters: CHAPTERS,
      locales: LOCALES,
      fragmentExists: (locale, slug) => readFragmentFs(locale, slug) != null,
    }),
  ]
  return errors
}

function buildChapterPage({ chapter, locale, template }) {
  const filename = `${chapter.slug}.html`
  const otherLocale = OTHER_LOCALE[locale]
  const body = readFragmentFs(locale, chapter.slug)
  const html = fillTemplate(template, {
    LANG: locale,
    OTHER_LANG: otherLocale,
    OTHER_LANG_LABEL: escapeHtml(LOCALE_LABEL[otherLocale]),
    FILENAME: filename,
    GUIDE_TITLE: escapeHtml(GUIDE_TITLE[locale]),
    CHAPTER_TITLE: escapeHtml((chapter.title ?? chapter.label)[locale]),
    NAV_LABEL: escapeHtml(UI_STRINGS[locale].navLabel),
    SKIP_LABEL: escapeHtml(UI_STRINGS[locale].skipLabel),
    BACK_TO_APP: escapeHtml(UI_STRINGS[locale].backToApp),
    SIDEBAR: renderSidebar({ chapters: CHAPTERS, locale, currentSlug: chapter.slug }),
    BODY: body,
  })
  return { filename, html }
}

function buildIndexPage({ locale, template, indexBodyTemplate }) {
  const otherLocale = OTHER_LOCALE[locale]
  const chapterList = renderSidebar({ chapters: CHAPTERS, locale, currentSlug: null })
  const body = fillTemplate(indexBodyTemplate, {
    GUIDE_TITLE: escapeHtml(GUIDE_TITLE[locale]),
    INDEX_INTRO: escapeHtml(UI_STRINGS[locale].indexIntro),
    CHAPTER_LIST: chapterList,
  })
  return fillTemplate(template, {
    LANG: locale,
    OTHER_LANG: otherLocale,
    OTHER_LANG_LABEL: escapeHtml(LOCALE_LABEL[otherLocale]),
    FILENAME: 'index.html',
    GUIDE_TITLE: escapeHtml(GUIDE_TITLE[locale]),
    CHAPTER_TITLE: escapeHtml(GUIDE_TITLE[locale]),
    NAV_LABEL: escapeHtml(UI_STRINGS[locale].navLabel),
    SKIP_LABEL: escapeHtml(UI_STRINGS[locale].skipLabel),
    BACK_TO_APP: escapeHtml(UI_STRINGS[locale].backToApp),
    SIDEBAR: renderSidebar({ chapters: CHAPTERS, locale, currentSlug: null }),
    BODY: body,
  })
}

/** Writes every locale/index/chapter page + copies curated images into `outDir`. */
function buildToDir(outDir) {
  const template = fs.readFileSync(TEMPLATE_PATH, 'utf-8')
  const indexBodyTemplate = fs.readFileSync(INDEX_BODY_PATH, 'utf-8')
  const published = CHAPTERS.filter((c) => c.published)

  for (const locale of LOCALES) {
    const localeDir = path.join(outDir, locale)
    fs.mkdirSync(localeDir, { recursive: true })
    fs.writeFileSync(path.join(localeDir, 'index.html'), buildIndexPage({ locale, template, indexBodyTemplate }))
    for (const chapter of published) {
      const { filename, html } = buildChapterPage({ chapter, locale, template })
      fs.writeFileSync(path.join(localeDir, filename), html)
    }
  }

  for (const chapter of published) {
    const images = chapter.images ?? []
    if (images.length === 0) continue
    const destDir = path.join(outDir, 'assets', chapter.slug)
    fs.mkdirSync(destDir, { recursive: true })
    for (const file of images) {
      fs.copyFileSync(path.join(FLOWS_DIR, chapter.slug, file), path.join(destDir, file))
    }
  }

  return published
}

function collectFiles(dir, excludeSet) {
  const out = []
  function walk(d, rel) {
    if (!fs.existsSync(d)) return
    for (const entry of fs.readdirSync(d, { withFileTypes: true })) {
      const relPath = rel ? `${rel}/${entry.name}` : entry.name
      if (excludeSet.has(relPath)) continue
      const abs = path.join(d, entry.name)
      if (entry.isDirectory()) {
        walk(abs, relPath)
      } else {
        out.push(relPath)
      }
    }
  }
  walk(dir, '')
  return out.sort()
}

/** Regenerate-and-diff: compares two guide output trees, excluding hand-written assets. */
export function diffDirs(dirA, dirB, { exclude = [] } = {}) {
  const excludeSet = new Set(exclude)
  const filesA = collectFiles(dirA, excludeSet)
  const filesB = collectFiles(dirB, excludeSet)
  const setA = new Set(filesA)
  const setB = new Set(filesB)
  const diffs = []

  for (const f of filesA) {
    if (!setB.has(f)) {
      diffs.push(`only in regenerated output: ${f}`)
      continue
    }
    const a = fs.readFileSync(path.join(dirA, f))
    const b = fs.readFileSync(path.join(dirB, f))
    if (!a.equals(b)) {
      diffs.push(`content differs: ${f}`)
    }
  }
  for (const f of filesB) {
    if (!setA.has(f)) {
      diffs.push(`only in committed output: ${f}`)
    }
  }
  return diffs
}

function runBuild() {
  const errors = runValidation()
  if (errors.length > 0) {
    console.error('build-guide: validation failed:\n' + errors.map((e) => `  - ${e}`).join('\n'))
    process.exit(1)
  }

  const published = buildToDir(OUT_DIR)
  console.log(`build-guide: wrote ${published.length} chapter(s) x ${LOCALES.length} locale(s) + index pages.`)
}

function runCheck() {
  const errors = runValidation()
  if (errors.length > 0) {
    console.error('guide:check: validation failed:\n' + errors.map((e) => `  - ${e}`).join('\n'))
    return false
  }

  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'guide-check-'))
  try {
    buildToDir(tmpDir)
    const diffs = diffDirs(tmpDir, OUT_DIR, { exclude: ['assets/guide.css'] })
    if (diffs.length > 0) {
      console.error(
        'guide:check: committed public/guide/** is stale — run "pnpm guide:build" and commit the result:\n' +
          diffs.map((d) => `  - ${d}`).join('\n'),
      )
      return false
    }
    console.log('guide:check: clean — committed public/guide/** matches the manifest + fragments.')
    return true
  } finally {
    fs.rmSync(tmpDir, { recursive: true, force: true })
  }
}

// Entry guard: only run when invoked directly (never as a side effect of a test import).
const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) {
  const mode = process.argv.includes('--check') ? 'check' : 'build'
  if (mode === 'check') {
    process.exit(runCheck() ? 0 : 1)
  } else {
    runBuild()
  }
}
