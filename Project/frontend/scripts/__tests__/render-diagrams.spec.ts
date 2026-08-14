import { describe, it, expect } from 'vitest'
import path from 'node:path'
import { parseArgs, resolveOptions, buildMermaidArgv } from '../render-diagrams.mjs'

// NOTE: these are pure unit tests — no `npx`/mermaid-cli process is ever spawned.
// `main()` stays behind the `import.meta.url` entry guard (ADR-UGD-07), so importing
// this module for its exports never triggers a real render.

describe('render-diagrams — resolveOptions defaults (byte-identical argv preservation)', () => {
  it('zero-arg options reproduce today\'s hardcoded defaults', () => {
    const options = resolveOptions({})

    expect(options.format).toBe('png')
    expect(options.width).toBe(1400)
    expect(options.scale).toBe(2)
    expect(options.input.endsWith(path.join('docs', 'slides', 'presentation', 'flows.md'))).toBe(true)
    expect(options.outDir.endsWith(path.join('docs', 'slides', 'presentation', 'diagrams'))).toBe(true)
  })

  it('zero-arg buildMermaidArgv is byte-identical to the current hardcoded argv', () => {
    const options = resolveOptions({})
    const argv = buildMermaidArgv(options, '/tmp/x.mmd', '/tmp/x.png', '/tmp/config.json')

    expect(argv).toEqual([
      '-i', '/tmp/x.mmd',
      '-o', '/tmp/x.png',
      '-c', '/tmp/config.json',
      '-b', 'white',
      '-s', '2',
      '-w', '1400',
    ])
  })
})

describe('render-diagrams — --format svg', () => {
  it('drops -s/--scale from argv and keeps -b/-w, targeting an .svg output', () => {
    const options = resolveOptions({ format: 'svg' })
    const argv = buildMermaidArgv(options, '/tmp/x.mmd', '/tmp/x.svg', '/tmp/config.json')

    expect(argv).toEqual([
      '-i', '/tmp/x.mmd',
      '-o', '/tmp/x.svg',
      '-c', '/tmp/config.json',
      '-b', 'white',
      '-w', '1400',
    ])
    expect(argv).not.toContain('-s')
  })
})

describe('render-diagrams — parseArgs', () => {
  it('accepts short flags for every option', () => {
    const args = parseArgs(['-i', 'a.md', '-o', 'out', '-f', 'svg', '-w', '800', '-s', '3'])

    expect(args).toEqual({ input: 'a.md', outDir: 'out', format: 'svg', width: '800', scale: '3' })
  })

  it('accepts long flags for every option', () => {
    const args = parseArgs([
      '--input', 'a.md',
      '--out-dir', 'out',
      '--format', 'svg',
      '--width', '800',
      '--scale', '3',
    ])

    expect(args).toEqual({ input: 'a.md', outDir: 'out', format: 'svg', width: '800', scale: '3' })
  })
})

describe('render-diagrams — resolveOptions cwd resolution', () => {
  it('resolves relative paths against the given cwd, not __dirname', () => {
    const cwd = path.resolve('/repo/somewhere')
    const options = resolveOptions({ input: 'my.md', outDir: 'out' }, { cwd })

    expect(options.input).toBe(path.resolve(cwd, 'my.md'))
    expect(options.outDir).toBe(path.resolve(cwd, 'out'))
  })
})

// The `npx` subprocess call is still made with `{ shell: true }` (CWE-78 risk if
// left unvalidated): on Windows, `npx` resolves to `npx.cmd`, and Node's
// child_process cannot invoke a `.cmd` file without going through a shell —
// confirmed locally: `execFileSync('npx', ..., { shell: true !== true })` fails
// with ENOENT for `npx.cmd`, while `{ shell: true }` succeeds. Since the shell
// re-parses the joined argv as one command string, `resolveOptions` defensively
// rejects `--input`/`--out-dir` values containing shell metacharacters instead.
describe('render-diagrams — shell metacharacter rejection (CWE-78 guard, since npx requires shell:true on Windows)', () => {
  it('rejects a --out-dir value containing shell metacharacters before any exec', () => {
    expect(() => resolveOptions({ outDir: 'out; rm -rf /' })).toThrow(/unsafe/i)
  })

  it('rejects a --input value containing shell metacharacters before any exec', () => {
    expect(() => resolveOptions({ input: 'a.md`whoami`' })).toThrow(/unsafe/i)
  })

  it('rejects command-substitution and pipe characters too', () => {
    expect(() => resolveOptions({ outDir: 'out$(whoami)' })).toThrow(/unsafe/i)
    expect(() => resolveOptions({ outDir: 'out | cat /etc/passwd' })).toThrow(/unsafe/i)
  })

  it('accepts an ordinary path containing spaces (not a shell metacharacter)', () => {
    expect(() => resolveOptions({ outDir: 'my diagrams' })).not.toThrow()
  })
})
