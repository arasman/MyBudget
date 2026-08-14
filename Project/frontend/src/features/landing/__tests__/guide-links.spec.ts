// Integration walker over the COMMITTED public/guide/** tree (design.md Testing Strategy,
// "Integration — committed output"; tasks.md 18.1-18.3). This intentionally reads the real
// generated artifact on disk rather than importing scripts/guide/chapters.mjs — the guide's
// authoring surface (scripts/) sits outside this project's tsconfig `include` (src/**), and this
// walker's whole point is to catch drift in the SHIPPED tree, independent of the generator's
// internals. It discharges proposal risks 2 and 3 (sidebar drift, dead links) for real.
import { describe, it, expect } from 'vitest'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
// src/features/landing/__tests__/ -> frontend root -> public/guide
const GUIDE_DIR = path.resolve(__dirname, '../../../../public/guide')
const LOCALES = ['en', 'es']

function listFiles(dir: string): string[] {
  const out: string[] = []
  function walk(d: string, rel: string): void {
    for (const entry of fs.readdirSync(d, { withFileTypes: true })) {
      const relPath = rel ? `${rel}/${entry.name}` : entry.name
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

function chapterPages(locale: string): string[] {
  return fs
    .readdirSync(path.join(GUIDE_DIR, locale))
    .filter((f) => f.endsWith('.html') && f !== 'index.html')
    .sort()
}

describe('guide-links — committed public/guide/** integration walker', () => {
  it('EN and ES file trees are identical', () => {
    const en = listFiles(path.join(GUIDE_DIR, 'en'))
    const es = listFiles(path.join(GUIDE_DIR, 'es'))

    expect(en.length).toBeGreaterThan(0)
    expect(en).toEqual(es)
  })

  it('every <img src>, sidebar href, and locale-toggle href resolves to an existing file', () => {
    const missing: string[] = []

    for (const locale of LOCALES) {
      const localeDir = path.join(GUIDE_DIR, locale)
      const pages = fs.readdirSync(localeDir).filter((f) => f.endsWith('.html'))

      for (const file of pages) {
        const html = fs.readFileSync(path.join(localeDir, file), 'utf-8')
        const refs = [...html.matchAll(/(?:href|src)="([^"]+)"/g)].map((m) => m[1] as string)

        for (const ref of refs) {
          // Absolute app links ("/") are intentional exits from the guide (ADR-UGD-04) — not
          // guide-internal targets to validate here. Fragment-only refs (the skip link's
          // "#content") point within the same document, not at another file.
          if (ref.startsWith('/') || ref.startsWith('#')) continue
          const resolved = path.resolve(localeDir, ref)
          if (!fs.existsSync(resolved)) {
            missing.push(`${locale}/${file} -> "${ref}"`)
          }
        }
      }
    }

    expect(missing).toEqual([])
  })

  it('every page sidebar lists every chapter (all published, all linkable)', () => {
    const expectedCount = chapterPages('en').length

    for (const locale of LOCALES) {
      const localeDir = path.join(GUIDE_DIR, locale)
      const pages = fs.readdirSync(localeDir).filter((f) => f.endsWith('.html'))

      for (const file of pages) {
        const html = fs.readFileSync(path.join(localeDir, file), 'utf-8')
        // Scope to the <nav class="sidebar">...</nav> region only — index.html legitimately
        // repeats the chapter list a second time in its body (index-body.html's
        // {{CHAPTER_LIST}}), which is not the sidebar this requirement (UG-3) is about.
        const navMatch = html.match(/<nav class="sidebar"[\s\S]*?<\/nav>/)
        expect(navMatch).not.toBeNull()
        const nav = navMatch![0]
        const items = nav.match(/<li>/g) ?? []
        const disabled = nav.match(/<span class="disabled">/g) ?? []

        expect(items).toHaveLength(expectedCount)
        // Post-PR5: every chapter is published — a stale `published: false` entry would
        // regress this to a non-empty disabled-span count.
        expect(disabled).toHaveLength(0)
      }
    }
  })

  it('heading counts match across EN/ES for every chapter', () => {
    for (const slug of chapterPages('en').map((f) => f.replace(/\.html$/, ''))) {
      const enHtml = fs.readFileSync(path.join(GUIDE_DIR, 'en', `${slug}.html`), 'utf-8')
      const esHtml = fs.readFileSync(path.join(GUIDE_DIR, 'es', `${slug}.html`), 'utf-8')
      const countHeadings = (html: string) => (html.match(/<h2>/g) ?? []).length

      expect(countHeadings(enHtml)).toBe(countHeadings(esHtml))
      expect(countHeadings(enHtml)).toBeGreaterThan(0)
    }
  })

  it('members.html has zero <img> elements in both locales (UG-6)', () => {
    for (const locale of LOCALES) {
      const html = fs.readFileSync(path.join(GUIDE_DIR, locale, 'members.html'), 'utf-8')
      expect(html).not.toMatch(/<img\b/)
    }
  })
})
