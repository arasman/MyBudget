import { describe, it, expect } from 'vitest'
import { render, fireEvent, waitFor } from '@testing-library/vue'
import { createI18n } from 'vue-i18n'
import CategoryGroupForm from '../CategoryGroupForm.vue'

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        budgetStructure: {
          categoryGroups: {
            create: 'New Group',
            edit: 'Edit Group',
            name: 'Name',
            validation: {
              nameRequired: 'Name is required',
              nameTooLong: 'Name must be 200 characters or fewer',
            },
          },
          common: { save: 'Save', cancel: 'Cancel' },
        },
      },
    },
  })
}

function renderForm(modelValue = null) {
  return render(CategoryGroupForm, {
    props: { modelValue },
    global: { plugins: [makeI18n()] },
  })
}

// jsdom marks <dialog> content as inaccessible; query via document.querySelector
describe('CategoryGroupForm — validation (REQ-FORM-INLINE-VAL-1)', () => {
  it('shows nameRequired error when name is empty on submit', async () => {
    renderForm()
    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(document.querySelector('.text-error')?.textContent).toContain('Name is required')
    })
  })

  it('shows nameTooLong error when name exceeds 200 chars', async () => {
    renderForm()
    const input = document.querySelector('#group-name') as HTMLInputElement
    await fireEvent.update(input, 'a'.repeat(201))
    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(document.querySelector('.text-error')?.textContent).toContain('Name must be 200 characters or fewer')
    })
  })

  it('emits submit with trimmed name when valid', async () => {
    const { emitted } = renderForm()
    const input = document.querySelector('#group-name') as HTMLInputElement
    await fireEvent.update(input, '  My Group  ')
    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(emitted()['submit']).toBeTruthy()
      expect(emitted()['submit']![0]).toEqual([{ name: 'My Group' }])
    })
  })

  it('accepts name of exactly 200 chars', async () => {
    const { emitted } = renderForm()
    const input = document.querySelector('#group-name') as HTMLInputElement
    await fireEvent.update(input, 'a'.repeat(200))
    const submitBtn = document.querySelector('button[type="submit"]')!
    await fireEvent.click(submitBtn)
    await waitFor(() => {
      expect(emitted()['submit']).toBeTruthy()
    })
  })
})
