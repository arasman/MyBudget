import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import DeleteCutModal from '../components/DeleteCutModal.vue'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (k: string, params?: Record<string, string>) => {
      if (k === 'currentSituation.deleteModal.instruction') {
        return `Type the date ${params?.['date'] ?? ''} to confirm.`
      }
      const map: Record<string, string> = {
        'currentSituation.deleteModal.title': 'Delete Cut Record',
        'currentSituation.deleteModal.typeDate': 'Confirm cut date',
        'currentSituation.deleteModal.confirm': 'Delete permanently',
        'common.cancel': 'Cancel',
      }
      return map[k] ?? k
    },
  }),
}))

function renderModal(props: { cutDate: string; loading?: boolean }) {
  return render(DeleteCutModal, { props })
}

describe('DeleteCutModal', () => {
  it('renders the modal with title', () => {
    renderModal({ cutDate: '2026-07-25' })
    expect(screen.queryByText('Delete Cut Record')).not.toBeNull()
  })

  it('has delete button disabled initially', () => {
    renderModal({ cutDate: '2026-07-25' })
    const deleteBtn = screen.getByRole('button', { name: 'Delete permanently' }) as HTMLButtonElement
    expect(deleteBtn.disabled).toBe(true)
  })

  it('keeps delete button disabled when wrong date is typed', async () => {
    renderModal({ cutDate: '2026-07-25' })
    await fireEvent.update(screen.getByRole('textbox'), '2026-07-20')
    const deleteBtn = screen.getByRole('button', { name: 'Delete permanently' }) as HTMLButtonElement
    expect(deleteBtn.disabled).toBe(true)
  })

  it('enables delete button when correct date is typed', async () => {
    renderModal({ cutDate: '2026-07-25' })
    await fireEvent.update(screen.getByRole('textbox'), '2026-07-25')
    const deleteBtn = screen.getByRole('button', { name: 'Delete permanently' }) as HTMLButtonElement
    expect(deleteBtn.disabled).toBe(false)
  })

  it('emits confirm when delete clicked after correct date typed', async () => {
    const { emitted } = renderModal({ cutDate: '2026-07-25' })
    await fireEvent.update(screen.getByRole('textbox'), '2026-07-25')
    await fireEvent.click(screen.getByRole('button', { name: 'Delete permanently' }))
    expect(emitted()['confirm']).toBeTruthy()
  })

  it('emits cancel when cancel button clicked', async () => {
    const { emitted } = renderModal({ cutDate: '2026-07-25' })
    const cancelBtn = screen.queryByText('Cancel')
    expect(cancelBtn).not.toBeNull()
    await fireEvent.click(cancelBtn!)
    expect(emitted()['cancel']).toBeTruthy()
  })
})
