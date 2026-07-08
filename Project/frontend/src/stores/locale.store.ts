import { defineStore } from 'pinia'
import { ref } from 'vue'
import { i18n } from '@/i18n'
import http from '@/api/axios'

type SupportedLocale = 'en' | 'es'

export const useLocaleStore = defineStore('locale', () => {
  const locale = ref<SupportedLocale>(
    (localStorage.getItem('locale') as SupportedLocale) ?? 'en',
  )

  function setLocale(lang: SupportedLocale) {
    locale.value = lang
    // Update vue-i18n global locale
    i18n.global.locale.value = lang
    // Persist to localStorage
    localStorage.setItem('locale', lang)
    // Update Axios default Accept-Language header
    http.defaults.headers.common['Accept-Language'] = lang
  }

  return {
    locale,
    setLocale,
  }
})
