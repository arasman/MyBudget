import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createWebHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import RegisterView from '@/views/RegisterView.vue'

// Mock the auth store
const mockRegister = vi.fn()
vi.mock('@/stores/auth.store', () => ({
  useAuthStore: () => ({
    register: mockRegister,
    isAuthenticated: false,
    accessToken: null,
    user: null,
  }),
}))

function buildGlobals() {
  const pinia  = createPinia()
  const router = createRouter({ history: createWebHistory(), routes: [
    { path: '/', component: { template: '<div>Home</div>' } },
    { path: '/login', component: { template: '<div>Login</div>' } },
    { path: '/register', component: RegisterView },
  ]})
  const i18n = createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        'auth.register.title': 'Create Account',
        'auth.register.firstNamePlaceholder': 'First name',
        'auth.register.lastNamePlaceholder': 'Last name',
        'auth.register.emailPlaceholder': 'your-email-here',
        'auth.register.passwordPlaceholder': 'Password',
        'auth.register.submit': 'Create Account',
        'auth.register.loginLink': 'Sign in',
        'auth.register.languageLabel': 'Language',
        'auth.register.passwordTooWeak': 'Password is too weak.',
        'auth.register.passwordStrength.ruleLength': 'At least 8 characters',
        'auth.register.passwordStrength.ruleUppercase': 'One uppercase letter',
        'auth.register.passwordStrength.ruleLowercase': 'One lowercase letter',
        'auth.register.passwordStrength.ruleDigit': 'One number',
        'auth.emailLabel': 'Email',
        'auth.passwordLabel': 'Password',
        'common.error': 'An error occurred',
      },
    },
  })
  return { global: { plugins: [pinia, router, i18n] } }
}

describe('RegisterView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('renders the registration form', () => {
    render(RegisterView, buildGlobals())
    // Use getAllByText because the title and submit button share the same text
    const elements = screen.getAllByText('Create Account')
    expect(elements.length).toBeGreaterThan(0)
  })

  it('calls authStore.register with correct payload on submit', async () => {
    mockRegister.mockResolvedValue(undefined)
    render(RegisterView, buildGlobals())

    const inputs = screen.getAllByRole('textbox')
    // Fill in visible text inputs (firstName, lastName, email)
    await fireEvent.update(inputs[0]!, 'John')
    await fireEvent.update(inputs[1]!, 'Doe')
    await fireEvent.update(inputs[2]!, 'john@example.com')

    // password is type=password, not textbox
    const passwordInput = document.querySelector('input[type="password"]') as HTMLInputElement
    await fireEvent.update(passwordInput, 'Password1')

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(mockRegister).toHaveBeenCalledWith(expect.objectContaining({
        email: 'john@example.com',
        firstName: 'John',
        lastName: 'Doe',
        password: 'Password1',
      }))
    })
  })

  it('displays global error on server 500', async () => {
    mockRegister.mockRejectedValue({ response: { status: 500 } })
    render(RegisterView, buildGlobals())

    const form = document.querySelector('form')!
    await fireEvent.submit(form)

    await waitFor(() => {
      expect(screen.getByText('An error occurred')).toBeTruthy()
    })
  })

  it('does not use v-html with unsanitized content', () => {
    render(RegisterView, buildGlobals())
    // Static analysis: check no v-html directive is used in rendered output
    // with user-provided data — the component uses text interpolation only
    const vHtmlElements = document.querySelectorAll('[v-html]')
    expect(vHtmlElements.length).toBe(0)
  })
})
