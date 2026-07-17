import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import CreateBudgetModal from '../CreateBudgetModal.vue'

// IMPORTANT: vi.mock factories are hoisted by Vitest.
// Use vi.hoisted() for named mock references so they are available inside the factory.
const { mockCreateBudget } = vi.hoisted(() => ({
  mockCreateBudget: vi.fn(),
}))

vi.mock('../../api/budgets.api', () => ({
  createBudget: mockCreateBudget,
}))

const i18nMessages = {
  en: {
    common: { cancel: 'Cancel', error: 'An error occurred' },
    budgetStructure: {
      selection: {
        createBudget: 'New Budget',
        createBudgetTitle: 'Create Budget',
        budgetNameLabel: 'Budget name',
        budgetNamePlaceholder: 'Enter budget name',
        budgetNameRequired: 'Budget name is required',
        budgetNameTooLong: 'Budget name must be 200 characters or fewer',
      },
    },
  },
}

function makeI18n() {
  return createI18n({ legacy: false, locale: 'en', messages: i18nMessages })
}

function renderModal() {
  // jsdom does not implement HTMLDialogElement — patch to make it visible in tests
  if (!HTMLDialogElement.prototype.showModal) {
    HTMLDialogElement.prototype.showModal = function () {
      this.setAttribute('open', '')
    }
  }
  if (!HTMLDialogElement.prototype.close) {
    HTMLDialogElement.prototype.close = function () {
      this.removeAttribute('open')
    }
  }

  const pinia = createPinia()
  const wrapper = render(CreateBudgetModal, {
    global: { plugins: [pinia, makeI18n()] },
  })

  // Simulate opening the modal by setting the open attribute directly
  const dialog = document.querySelector('dialog')!
  dialog.setAttribute('open', '')

  return wrapper
}

describe('CreateBudgetModal', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('renders title and name input', () => {
    renderModal()
    expect(screen.getByText('Create Budget')).toBeTruthy()
    expect(document.querySelector('#budget-name')).not.toBeNull()
  })

  it('shows inline error when submitting with empty name (no API call)', async () => {
    renderModal()

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('Budget name is required')).toBeTruthy()
    })
    expect(mockCreateBudget).not.toHaveBeenCalled()
  })

  it('shows inline error when name exceeds 200 characters (no API call)', async () => {
    renderModal()

    const nameInput = document.querySelector('#budget-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'a'.repeat(201))

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('Budget name must be 200 characters or fewer')).toBeTruthy()
    })
    expect(mockCreateBudget).not.toHaveBeenCalled()
  })

  it('disables submit button while request is in-flight', async () => {
    let resolveCreate!: (value: { id: string; name: string }) => void
    mockCreateBudget.mockReturnValue(
      new Promise<{ id: string; name: string }>((res) => {
        resolveCreate = res
      }),
    )

    renderModal()

    const nameInput = document.querySelector('#budget-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'My Budget')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      const submitBtn = document.querySelector('button[type="submit"]') as HTMLButtonElement
      expect(submitBtn.disabled).toBe(true)
    })

    // Resolve to clean up the pending promise
    resolveCreate({ id: 'b-1', name: 'My Budget' })
  })

  it('emits created event with result on successful submission', async () => {
    mockCreateBudget.mockResolvedValue({ id: 'b-1', name: 'My Budget' })

    const { emitted } = renderModal()

    const nameInput = document.querySelector('#budget-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'My Budget')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(emitted()['created']).toBeTruthy()
      expect(emitted()['created']![0]).toEqual([{ id: 'b-1', name: 'My Budget' }])
    })
  })

  it('shows server error on API failure', async () => {
    mockCreateBudget.mockRejectedValue({ response: { data: { detail: '' }, status: 500 } })

    renderModal()

    const nameInput = document.querySelector('#budget-name') as HTMLInputElement
    await fireEvent.update(nameInput, 'My Budget')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('An error occurred')).toBeTruthy()
    })
  })
})
