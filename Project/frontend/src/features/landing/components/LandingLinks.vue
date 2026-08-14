<!--
  LANDING-4: secondary outbound evaluation links (repo, README, guide, deck) —
  never styled as buttons, so they stay visually subordinate to LandingCta's
  sign-up CTA. URLs are plain string consts, not i18n keys (design.md
  decision #11); only the link labels are translated. The guide link is the
  one scoped exception — its href is locale-aware (ADR-UGD-09).
-->
<template>
  <section
    data-testid="landing-links"
    class="px-4 pb-12 flex flex-col sm:flex-row items-center justify-center gap-4 text-sm"
  >
    <a
      data-testid="link-github"
      :href="REPO_URL"
      target="_blank"
      rel="noopener noreferrer"
      class="link link-hover text-base-content/70"
    >
      {{ $t('landing.links.github') }}
    </a>
    <a
      data-testid="link-readme"
      :href="README_URL"
      target="_blank"
      rel="noopener noreferrer"
      class="link link-hover text-base-content/70"
    >
      {{ $t('landing.links.readme') }}
    </a>
    <a
      data-testid="link-guide"
      :href="guideHref"
      target="_blank"
      rel="noopener noreferrer"
      class="link link-hover text-base-content/70"
    >
      {{ $t('landing.links.guide') }}
    </a>
    <a
      data-testid="link-deck"
      :href="DECK_URL"
      target="_blank"
      rel="noopener noreferrer"
      class="link link-hover text-base-content/70"
    >
      {{ $t('landing.links.deck') }}
    </a>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { storeToRefs } from 'pinia'
import { useLocaleStore } from '@/stores/locale.store'
import { REPO_URL, README_URL, DECK_URL, guideUrl } from '../config/links'

// storeToRefs (not `useLocaleStore().locale`) preserves reactivity when the visitor flips the
// language switcher without navigating away from the landing page (ADR-UGD-09).
const { locale } = storeToRefs(useLocaleStore())
const guideHref = computed(() => guideUrl(locale.value))
</script>
