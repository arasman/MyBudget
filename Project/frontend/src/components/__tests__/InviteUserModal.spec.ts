// REQ-LSYNC-4: Role <option> labels in InviteUserModal must use i18n keys
import { describe, it, expect, vi } from 'vitest'
import { render } from '@testing-library/vue'
import { createI18n } from 'vue-i18n'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@/api/axios', () => ({
  default: {
    post: vi.fn().mockResolvedValue({ data: {} }),
  },
}))

import InviteUserModal from '../budget/InviteUserModal.vue'

function makeI18n(locale: 'en' | 'es' = 'en') {
  return createI18n({
    legacy: false,
    locale,
    messages: {
      en: {
        common: { cancel: 'Cancel', error: 'An error occurred' },
        invitation: {
          modal: {
            title: 'Invite a User',
            emailLabel: 'Email address',
            roleLabel: 'Role',
            submit: 'Send Invitation',
            successMessage: 'Invitation sent successfully.',
            error: { alreadyMember: 'Already a member.' },
          },
        },
        enums: {
          role: { admin: 'Admin', operator: 'Operator', readOnly: 'Read Only' },
        },
      },
      es: {
        common: { cancel: 'Cancelar', error: 'Ocurrió un error' },
        invitation: {
          modal: {
            title: 'Invitar a un usuario',
            emailLabel: 'Correo electrónico',
            roleLabel: 'Rol',
            submit: 'Enviar invitación',
            successMessage: 'Invitación enviada exitosamente.',
            error: { alreadyMember: 'Ya es miembro.' },
          },
        },
        enums: {
          role: { admin: 'Administrador', operator: 'Operador', readOnly: 'Solo lectura' },
        },
      },
    },
  })
}

function getRoleOptionLabels(container: Element): string[] {
  const select = container.querySelector('select')
  if (!select) throw new Error('No <select> found in InviteUserModal')
  return Array.from(select.querySelectorAll('option')).map(
    (o) => o.textContent?.trim() ?? '',
  )
}

describe('InviteUserModal role options', () => {
  it('renders role options with English labels when locale=en', () => {
    setActivePinia(createPinia())
    const i18n = makeI18n('en')
    const { container } = render(InviteUserModal, {
      props: { budgetId: 'budget-1' },
      global: { plugins: [i18n] },
    })

    const labels = getRoleOptionLabels(container)
    expect(labels).toContain('Admin')
    expect(labels).toContain('Operator')
    expect(labels).toContain('Read Only')
    expect(labels).not.toContain('admin')
    expect(labels).not.toContain('operator')
    expect(labels).not.toContain('read-only')
  })

  it('renders role options with Spanish labels when locale=es', () => {
    setActivePinia(createPinia())
    const i18n = makeI18n('es')
    i18n.global.locale.value = 'es'
    const { container } = render(InviteUserModal, {
      props: { budgetId: 'budget-1' },
      global: { plugins: [i18n] },
    })

    const labels = getRoleOptionLabels(container)
    expect(labels).toContain('Administrador')
    expect(labels).toContain('Operador')
    expect(labels).toContain('Solo lectura')
    expect(labels).not.toContain('Admin')
    expect(labels).not.toContain('Operator')
    expect(labels).not.toContain('Read Only')
  })
})
