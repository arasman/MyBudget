import { createI18n } from 'vue-i18n'
import en from './locales/en.json'
import es from './locales/es.json'

const supportedLocales = ['en', 'es'] as const
type SupportedLocale = (typeof supportedLocales)[number]

function detectLocale(): SupportedLocale {
  const stored = localStorage.getItem('locale')
  if (stored && supportedLocales.includes(stored as SupportedLocale)) {
    return stored as SupportedLocale
  }

  const browserLocale = navigator.language.slice(0, 2)
  if (supportedLocales.includes(browserLocale as SupportedLocale)) {
    return browserLocale as SupportedLocale
  }

  return 'en'
}

export const i18n = createI18n({
  legacy: false,
  locale: detectLocale(),
  fallbackLocale: 'en',
  messages: {
    en,
    es,
  },
})

export default i18n
