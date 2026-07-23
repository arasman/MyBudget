import { defineStore } from 'pinia'
import { ref } from 'vue'
import { i18n } from '@/i18n'
import http from '@/api/axios'

export type SupportedLocale = 'en' | 'es'

export const useLocaleStore = defineStore('locale', () => {
  const locale = ref<SupportedLocale>(
    (localStorage.getItem('locale') as SupportedLocale) ?? 'en',
  )

  function setLocale(lang: SupportedLocale): void {
    locale.value = lang
    i18n.global.locale.value = lang
    localStorage.setItem('locale', lang)
    http.defaults.headers.common['Accept-Language'] = lang
  }

  return {
    locale,
    setLocale,
  }
})
