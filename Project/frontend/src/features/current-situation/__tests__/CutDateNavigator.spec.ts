import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import CutDateNavigator from '../components/CutDateNavigator.vue'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (k: string) => {
      const map: Record<string, string> = {
        'currentSituation.navigation.previous': 'Previous',
        'currentSituation.navigation.next': 'Next',
      }
      return map[k] ?? k
    },
  }),
}))

function renderNav(props: { currentDate: string | null; hasPrevious: boolean; hasNext: boolean }) {
  return render(CutDateNavigator, { props })
}

describe('CutDateNavigator', () => {
  it('displays the current date', () => {
    renderNav({ currentDate: '2026-07-25', hasPrevious: true, hasNext: true })
    expect(screen.queryByText('2026-07-25')).not.toBeNull()
  })

  it('disables prev button when hasPrevious is false', () => {
    renderNav({ currentDate: '2026-07-20', hasPrevious: false, hasNext: true })
    const prevBtn = screen.getByRole('button', { name: 'Previous' }) as HTMLButtonElement
    expect(prevBtn.disabled).toBe(true)
  })

  it('disables next button when hasNext is false', () => {
    renderNav({ currentDate: '2026-07-28', hasPrevious: true, hasNext: false })
    const nextBtn = screen.getByRole('button', { name: 'Next' }) as HTMLButtonElement
    expect(nextBtn.disabled).toBe(true)
  })

  it('enables both buttons when in the middle', () => {
    renderNav({ currentDate: '2026-07-25', hasPrevious: true, hasNext: true })
    expect((screen.getByRole('button', { name: 'Previous' }) as HTMLButtonElement).disabled).toBe(false)
    expect((screen.getByRole('button', { name: 'Next' }) as HTMLButtonElement).disabled).toBe(false)
  })

  it('emits navigate with "previous" when prev is clicked', async () => {
    const { emitted } = renderNav({ currentDate: '2026-07-25', hasPrevious: true, hasNext: true })
    await fireEvent.click(screen.getByRole('button', { name: 'Previous' }))
    expect(emitted()['navigate']).toBeTruthy()
    expect(emitted()['navigate']![0]).toEqual(['previous'])
  })

  it('emits navigate with "next" when next is clicked', async () => {
    const { emitted } = renderNav({ currentDate: '2026-07-25', hasPrevious: true, hasNext: true })
    await fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    expect(emitted()['navigate']![0]).toEqual(['next'])
  })
})
