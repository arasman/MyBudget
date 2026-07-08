import eslint from '@eslint/js'
import tseslint from 'typescript-eslint'
import pluginVue from 'eslint-plugin-vue'

export default tseslint.config(
  eslint.configs.recommended,
  ...tseslint.configs.recommended,
  ...pluginVue.configs['flat/recommended'],
  {
    rules: {
      // Allow single-word component names (common in Vue 3 composition patterns)
      'vue/multi-word-component-names': 'off',
      // Security: never allow v-html with user content
      'vue/no-v-html': 'error',
      // Type safety: disallow explicit any
      '@typescript-eslint/no-explicit-any': 'error',
    },
  },
)
