<template>
  <div
    v-if="password.length > 0"
    class="mt-2 space-y-2"
  >
    <!-- Progress bar -->
    <progress
      class="progress w-full h-2 transition-all duration-300"
      :class="progressClass"
      :value="score"
      max="4"
    />

    <!-- Rule checklist -->
    <ul class="space-y-1">
      <li
        v-for="rule in rules"
        :key="rule.key"
        class="flex items-center gap-2 text-xs transition-colors duration-200"
        :class="rule.met ? 'text-success' : 'text-base-content/50'"
      >
        <span
          class="text-sm leading-none"
          aria-hidden="true"
        >{{ rule.met ? '✓' : '○' }}</span>
        <span>{{ rule.label }}</span>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{ password: string }>()

const { t } = useI18n()

const rules = computed(() => [
  {
    key: 'length',
    label: t('auth.register.passwordStrength.ruleLength'),
    met: props.password.length >= 8,
  },
  {
    key: 'uppercase',
    label: t('auth.register.passwordStrength.ruleUppercase'),
    met: /[A-Z]/.test(props.password),
  },
  {
    key: 'lowercase',
    label: t('auth.register.passwordStrength.ruleLowercase'),
    met: /[a-z]/.test(props.password),
  },
  {
    key: 'digit',
    label: t('auth.register.passwordStrength.ruleDigit'),
    met: /[0-9]/.test(props.password),
  },
])

const score = computed(() => rules.value.filter((r) => r.met).length)

const progressClass = computed(() => {
  if (score.value <= 1) return 'progress-error'
  if (score.value <= 2) return 'progress-warning'
  if (score.value === 3) return 'progress-warning'
  return 'progress-success'
})
</script>
