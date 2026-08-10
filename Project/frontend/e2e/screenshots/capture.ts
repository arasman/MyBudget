import type { Page } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

/** Repo root: e2e/screenshots -> e2e -> frontend -> Project -> MyBudget */
export const SLIDES_ROOT = path.resolve(__dirname, '../../../../docs/slides/flows')

export interface ManifestEntry {
  seq: number
  file: string
  title: string
  description: string
}

const manifests = new Map<string, ManifestEntry[]>()

/**
 * Captures a full-page screenshot for a slide flow and records it in that
 * flow's manifest (flushed to disk by flushManifest / a test.afterAll hook).
 *
 * @param flow    kebab-case flow name -> docs/slides/<flow>/
 * @param seq     sequence number in the flow (drives the filename prefix and slide order)
 * @param name    kebab-case slug for the filename, e.g. 'create-success'
 * @param title   short human title for the slide index (e.g. "Create cycle — success")
 * @param description one-sentence caption explaining what the slide shows and why
 */
export async function shoot(
  page: Page,
  flow: string,
  seq: number,
  name: string,
  title: string,
  description: string,
): Promise<void> {
  const dir = path.join(SLIDES_ROOT, flow)
  fs.mkdirSync(dir, { recursive: true })

  // Settle CSS transitions (e.g. AppToast's 200ms slide-in) before capturing —
  // expect().toBeVisible() passes mid-transition (opacity/transform don't
  // block Playwright's visibility check), so shooting right after an
  // assertion can catch an element still animating into place.
  await page.waitForTimeout(300)

  // AppLayout/PublicLayout roots use Tailwind's `min-h-screen` (min-height:
  // 100vh) so short pages still fill the window in normal use. That's
  // self-defeating for the scrollHeight-based resize below: scrollHeight of
  // a min-h-screen page is *at least* the current viewport height, so
  // measuring it just echoes back whatever height is already set and never
  // shrinks. Neutralize the floor for this capture only (doesn't touch the
  // app's actual styling — this stylesheet is injected into the test page,
  // not the source).
  await page.addStyleTag({ content: '.min-h-screen { min-height: 0 !important; }' })

  // Every dialog in this app (native <dialog ref> + .showModal(), or
  // `v-if` + class="modal modal-open") renders daisyUI's `.modal`, which is
  // `position: fixed` — removed from document flow, so it does NOT expand
  // `document.documentElement.scrollHeight` and is invisible to the
  // scrollHeight-resize path below. When one is open, screenshot it
  // directly: exactly its own rendered size, immune to viewport height.
  const openModal = page.locator('dialog.modal-open, dialog[open]').first()

  // PublicLayout (login/register/accept-invitation/...) centers a single
  // `.card` in a `min-h-screen` flex wrapper. Screenshotting the `.card`
  // element directly captures exactly its own bounding box — immune to
  // however tall the surrounding viewport/wrapper is.
  const hasNavbar = (await page.locator('.navbar').count()) > 0
  const card = page.locator('.card').first()

  let raw: Buffer
  if ((await openModal.count()) > 0) {
    // Reset to a generous fixed height first: an earlier shoot() in this
    // same test may have shrunk the viewport to fit a short page (see the
    // scrollHeight branch below), and daisyUI's `.modal-box` caps its own
    // height relative to the *current* viewport — a small leftover height
    // would visibly clip a taller modal.
    const { width } = page.viewportSize() ?? { width: 1280, height: 900 }
    await page.setViewportSize({ width, height: 900 })
    raw = await openModal.screenshot()
  } else if (!hasNavbar && (await card.count()) > 0) {
    raw = await card.screenshot()
  } else {
    // Plain AppLayout pages (no modal open): resize the viewport to the
    // page's actual content height, then take an ordinary (non-fullPage)
    // screenshot, instead of fullPage:true on a fixed tall viewport. That
    // fixed-tall approach had two failure modes: (1) `position: fixed`
    // elements anchored to the viewport edge (the toast, `bottom-4
    // right-4`) get pushed far down a tall viewport, stranding real content
    // near the bottom that sharp.trim() can't collapse away since it can
    // only crop a uniform *border*, not a gap *between* two content blocks;
    // (2) Chromium's fullPage capture path can duplicate `position: sticky`
    // elements. Matching the viewport to scrollHeight makes every capture
    // an ordinary single-viewport shot, so neither failure mode applies.
    const { width } = page.viewportSize() ?? { width: 1280, height: 800 }
    // body.scrollHeight, not documentElement.scrollHeight — <html> inherits
    // height:100% and reports at least the current viewport height
    // regardless of actual content, defeating this measurement entirely.
    const contentHeight = await page.evaluate(() => document.body.scrollHeight)
    // The toast is `position: fixed` — outside document flow, so it's
    // invisible to scrollHeight and re-anchors to whatever viewport height
    // gets set next. Pad extra room for it so it lands below the real
    // content instead of overlapping it.
    const hasToast = (await page.locator('.toast .alert').count()) > 0
    // Floor of 100: page.screenshot hard-errors on a 0-height viewport,
    // which a contentHeight of 0 would otherwise produce (e.g. a shoot()
    // called before the SPA has finished its first paint after navigation).
    const targetHeight = Math.max(Math.min(contentHeight + (hasToast ? 100 : 0), 8000), 100)
    await page.setViewportSize({ width, height: targetHeight })
    raw = await page.screenshot({ fullPage: false })
  }

  // Trim any residual uniform-color border and re-pad with the detected
  // background color so content doesn't touch the image edge.
  const cornerPixel = await sharp(raw).extract({ left: 0, top: 0, width: 1, height: 1 }).raw().toBuffer()
  const background = { r: cornerPixel[0]!, g: cornerPixel[1]!, b: cornerPixel[2]!, alpha: 1 }
  const trimmed = await sharp(raw).trim({ threshold: 10 }).toBuffer()

  const file = `${String(seq).padStart(2, '0')}-${name}.png`
  await sharp(trimmed)
    .extend({ top: 24, bottom: 24, left: 24, right: 24, background })
    .toFile(path.join(dir, file))

  const list = manifests.get(flow) ?? []
  list.push({ seq, file, title, description })
  manifests.set(flow, list)
}

/**
 * Writes the accumulated manifest for one flow to docs/slides/<flow>/.manifest.json.
 * Call from test.afterAll(() => flushManifest('flow-name')) once all shoot() calls
 * for that flow have run. generate-slide-index.mjs reads these to build index.md.
 */
export function flushManifest(flow: string): void {
  const list = [...(manifests.get(flow) ?? [])].sort((a, b) => a.seq - b.seq)
  if (list.length === 0) return

  const dir = path.join(SLIDES_ROOT, flow)
  fs.mkdirSync(dir, { recursive: true })
  fs.writeFileSync(path.join(dir, '.manifest.json'), JSON.stringify(list, null, 2))
}
