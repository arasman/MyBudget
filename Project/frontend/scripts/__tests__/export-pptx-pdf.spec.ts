import { describe, it, expect, vi } from 'vitest'
import { checkPreconditions, checkOutput, runExport } from '../export-pptx-pdf.mjs'

// NOTE: execFileSync is injected directly into runExport() as `execFileSyncFn` in every
// test below (never the real default) — this keeps these as true unit tests that never
// spawn a real PowerShell/PowerPoint COM process, and never touch the real repo's
// docs/slides/presentation/MyBudget.pptx or MyBudget.pdf files.

describe('export-pptx-pdf — checkPreconditions (threat matrix: Subprocess — PowerPoint COM)', () => {
  it('fails on a non-Windows platform and does not invoke PowerShell', () => {
    const result = checkPreconditions({ platform: 'darwin', inputExists: true })

    expect(result.ok).toBe(false)
    expect(result.reason).toMatch(/windows/i)
  })

  it('fails when the input .pptx does not exist', () => {
    const result = checkPreconditions({ platform: 'win32', inputExists: false })

    expect(result.ok).toBe(false)
    expect(result.reason).toMatch(/not found/i)
  })

  it('passes when the platform is win32 and the input exists', () => {
    const result = checkPreconditions({ platform: 'win32', inputExists: true })

    expect(result.ok).toBe(true)
  })
})

describe('export-pptx-pdf — checkOutput (zero-byte/absent output must never be reported as success)', () => {
  it('fails when the output file is absent', () => {
    const result = checkOutput({ exists: false, size: 0 })

    expect(result.ok).toBe(false)
  })

  it('fails when the output file exists but is zero-byte', () => {
    const result = checkOutput({ exists: true, size: 0 })

    expect(result.ok).toBe(false)
  })

  it('passes when the output file exists and has size > 0', () => {
    const result = checkOutput({ exists: true, size: 12345 })

    expect(result.ok).toBe(true)
  })
})

describe('export-pptx-pdf — runExport orchestration', () => {
  it('never invokes PowerShell when preconditions fail (non-Windows)', () => {
    const execFileSyncFn = vi.fn()

    const result = runExport({
      platform: 'linux',
      inputPath: 'fake-input.pptx',
      outputPath: 'fake-output.pdf',
      existsSync: vi.fn(() => true),
      statSync: vi.fn(),
      execFileSyncFn,
    })

    expect(result.ok).toBe(false)
    expect(execFileSyncFn).not.toHaveBeenCalled()
  })

  it('never invokes PowerShell when the input .pptx is missing', () => {
    const execFileSyncFn = vi.fn()

    const result = runExport({
      platform: 'win32',
      inputPath: 'fake-input.pptx',
      outputPath: 'fake-output.pdf',
      existsSync: vi.fn(() => false),
      statSync: vi.fn(),
      execFileSyncFn,
    })

    expect(result.ok).toBe(false)
    expect(execFileSyncFn).not.toHaveBeenCalled()
  })

  it('reports failure — never success — when the (mocked) COM call "succeeds" but the output is zero-byte/absent', () => {
    const execFileSyncFn = vi.fn(() => Buffer.from(''))
    const existsSync = vi
      .fn()
      .mockReturnValueOnce(true) // input exists
      .mockReturnValueOnce(true) // output "exists" ...
    const statSync = vi.fn(() => ({ size: 0 }) as unknown as ReturnType<typeof import('node:fs').statSync>) // ...but zero bytes

    const result = runExport({
      platform: 'win32',
      inputPath: 'fake-input.pptx',
      outputPath: 'fake-output.pdf',
      existsSync,
      statSync,
      execFileSyncFn,
    })

    expect(execFileSyncFn).toHaveBeenCalledTimes(1)
    expect(result.ok).toBe(false)
    expect(result.reason).toMatch(/no usable output/i)
  })

  it('reports success only when preconditions pass and the output is verified non-zero-byte', () => {
    const execFileSyncFn = vi.fn(() => Buffer.from(''))
    const existsSync = vi
      .fn()
      .mockReturnValueOnce(true) // input exists
      .mockReturnValueOnce(true) // output exists
    const statSync = vi.fn(() => ({ size: 54321 }) as unknown as ReturnType<typeof import('node:fs').statSync>)

    const result = runExport({
      platform: 'win32',
      inputPath: 'fake-input.pptx',
      outputPath: 'fake-output.pdf',
      existsSync,
      statSync,
      execFileSyncFn,
    })

    expect(execFileSyncFn).toHaveBeenCalledTimes(1)
    expect(result.ok).toBe(true)
  })
})
