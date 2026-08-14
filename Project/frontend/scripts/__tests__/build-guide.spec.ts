import { describe, it, expect, vi } from 'vitest'
import {
  renderSidebar,
  localeToggleHref,
  buildPageTitle,
  buildAtomically,
  fillTemplate,
  validateAssetPath,
  validateManifest,
  validateLocaleParity,
} from '../build-guide.mjs'

// NOTE: every function under test here is a PURE function — no filesystem I/O — so these
// are true unit tests. The `readFragment`/`sourceExists`/`fragmentExists` dependencies are
// injected fixtures, never the real repo tree (design.md Testing Strategy: "Unit — generator").

describe('build-guide — renderSidebar', () => {
  const CHAPTERS_FIXTURE = [
    { slug: 'auth', label: { en: 'Account & sign-in', es: 'Cuenta e inicio de sesión' }, published: true },
    { slug: 'dashboard', label: { en: 'Dashboard', es: 'Panel' }, published: false },
    { slug: 'members', label: { en: 'Members', es: 'Miembros' }, published: false },
  ]

  it('marks exactly one chapter as aria-current="page" for the current page', () => {
    const html = renderSidebar({ chapters: CHAPTERS_FIXTURE, locale: 'en', currentSlug: 'auth' })
    const currentMatches = html.match(/aria-current="page"/g) ?? []

    expect(currentMatches).toHaveLength(1)
    expect(html).toContain('<span aria-current="page">Account &amp; sign-in</span>')
  })

  it('renders no <a> element for unpublished chapters (dimmed non-link)', () => {
    const html = renderSidebar({ chapters: CHAPTERS_FIXTURE, locale: 'en', currentSlug: 'auth' })

    expect(html).not.toContain('<a href="dashboard.html">')
    expect(html).not.toContain('<a href="members.html">')
    expect(html).toContain('<span class="disabled">Dashboard</span>')
    expect(html).toContain('<span class="disabled">Members</span>')
  })

  it('renders a real <a> link for a published, non-current chapter', () => {
    const html = renderSidebar({ chapters: CHAPTERS_FIXTURE, locale: 'en', currentSlug: 'members' })

    expect(html).toContain('<a href="auth.html">Account &amp; sign-in</a>')
  })
})

describe('build-guide — localeToggleHref', () => {
  it('builds a same-filename sibling-locale href', () => {
    expect(localeToggleHref('en', 'dashboard.html')).toBe('../es/dashboard.html')
    expect(localeToggleHref('es', 'dashboard.html')).toBe('../en/dashboard.html')
  })
})

describe('build-guide — buildPageTitle (no self-duplicated <title> on index pages)', () => {
  it('composes "chapter · guide" when a chapter title is given', () => {
    expect(buildPageTitle({ guideTitle: 'MyBudget User Guide', chapterTitle: 'Account &amp; sign-in' })).toBe(
      'Account &amp; sign-in · MyBudget User Guide',
    )
  })

  it('is just the guide title alone for the index page (no chapter title) — never self-duplicated', () => {
    expect(buildPageTitle({ guideTitle: 'MyBudget User Guide' })).toBe('MyBudget User Guide')
    expect(buildPageTitle({ guideTitle: 'MyBudget User Guide' })).not.toBe(
      'MyBudget User Guide · MyBudget User Guide',
    )
  })
})

describe('build-guide — buildAtomically (mid-build failure never touches outDir)', () => {
  it('does not copy into outDir when the build step throws, leaving outDir untouched', () => {
    const copyInto = vi.fn()
    const removeDir = vi.fn()

    expect(() =>
      buildAtomically({
        outDir: '/fake/out',
        mkTempDir: () => '/fake/tmp',
        build: () => {
          throw new Error('boom mid-build')
        },
        copyInto,
        removeDir,
      }),
    ).toThrow(/boom mid-build/)

    expect(copyInto).not.toHaveBeenCalled()
    expect(removeDir).toHaveBeenCalledWith('/fake/tmp')
  })

  it('copies the scratch build into outDir only after a full successful build', () => {
    const copyInto = vi.fn()
    const removeDir = vi.fn()

    const published = buildAtomically({
      outDir: '/fake/out',
      mkTempDir: () => '/fake/tmp',
      build: (dir: string) => {
        expect(dir).toBe('/fake/tmp')
        return ['auth']
      },
      copyInto,
      removeDir,
    })

    expect(published).toEqual(['auth'])
    expect(copyInto).toHaveBeenCalledWith('/fake/tmp', '/fake/out')
    expect(removeDir).toHaveBeenCalledWith('/fake/tmp')
  })
})

describe('build-guide — fillTemplate', () => {
  it('replaces every {{KEY}} token with its value', () => {
    const out = fillTemplate('<title>{{TITLE}}</title><p>{{BODY}}</p>', { TITLE: 'Hello', BODY: 'World' })

    expect(out).toBe('<title>Hello</title><p>World</p>')
  })

  it('throws when a placeholder is left unresolved', () => {
    expect(() => fillTemplate('<title>{{TITLE}}</title><p>{{BODY}}</p>', { TITLE: 'Hi' })).toThrow(/BODY/)
  })
})

describe('build-guide — asset path validation (never ../../assets/)', () => {
  it('accepts a single-relative ../assets/<slug>/<file> path', () => {
    expect(validateAssetPath('../assets/auth/01-register-empty.png')).toEqual({
      ok: true,
      ref: 'auth/01-register-empty.png',
    })
  })

  it('rejects a double-relative ../../assets/ path, even though it contains the substring "../assets/"', () => {
    const result = validateAssetPath('../../assets/auth/01-register-empty.png')

    expect(result.ok).toBe(false)
    expect(result.reason).toMatch(/\.\.\/\.\.\/assets/)
  })

  it('rejects a path that does not start with ../assets/ at all', () => {
    const result = validateAssetPath('/absolute/assets/auth/x.png')

    expect(result.ok).toBe(false)
  })
})

describe('build-guide — validateManifest', () => {
  it('fails when a manifest images[] entry has no matching source PNG', () => {
    const errors = validateManifest({
      chapters: [{ slug: 'auth', images: ['missing.png'] }],
      locales: ['en'],
      readFragment: () => null,
      sourceExists: () => false,
    })

    expect(errors).toHaveLength(1)
    expect(errors[0]).toMatch(/missing\.png/)
  })

  it('fails when a fragment references an asset not listed in images[]', () => {
    const errors = validateManifest({
      chapters: [{ slug: 'auth', images: ['01-register-empty.png'] }],
      locales: ['en'],
      readFragment: () => '<img src="../assets/auth/02-not-listed.png">',
      sourceExists: () => true,
    })

    expect(errors).toHaveLength(1)
    expect(errors[0]).toMatch(/not listed/)
  })

  it('fails when a fragment uses a double-relative ../../assets/ path', () => {
    const errors = validateManifest({
      chapters: [{ slug: 'auth', images: ['01-register-empty.png'] }],
      locales: ['en'],
      readFragment: () => '<img src="../../assets/auth/01-register-empty.png">',
      sourceExists: () => true,
    })

    expect(errors).toHaveLength(1)
    expect(errors[0]).toMatch(/\.\.\/\.\.\/assets/)
  })

  it('passes for a chapter with no images key (text-only, like members)', () => {
    const errors = validateManifest({
      chapters: [{ slug: 'members' }],
      locales: ['en', 'es'],
      readFragment: () => '<p>No images here.</p>',
      sourceExists: () => true,
    })

    expect(errors).toHaveLength(0)
  })
})

describe('build-guide — validateLocaleParity', () => {
  it('fails when a published chapter is missing its EN or ES fragment', () => {
    const errors = validateLocaleParity({
      chapters: [{ slug: 'auth', published: true }],
      locales: ['en', 'es'],
      fragmentExists: (locale: string) => locale === 'en',
    })

    expect(errors).toHaveLength(1)
    expect(errors[0]).toMatch(/es\/auth/)
  })

  it('passes when both locale fragments exist for every published chapter', () => {
    const errors = validateLocaleParity({
      chapters: [{ slug: 'auth', published: true }],
      locales: ['en', 'es'],
      fragmentExists: () => true,
    })

    expect(errors).toHaveLength(0)
  })

  it('ignores unpublished chapters entirely', () => {
    const errors = validateLocaleParity({
      chapters: [{ slug: 'dashboard', published: false }],
      locales: ['en', 'es'],
      fragmentExists: () => false,
    })

    expect(errors).toHaveLength(0)
  })
})
