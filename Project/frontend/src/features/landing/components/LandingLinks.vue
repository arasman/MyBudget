<!--
  LANDING-4: secondary outbound evaluation links (repo, README, deck) — never
  styled as buttons, so they stay visually subordinate to LandingCta's
  sign-up CTA. URLs are plain string consts, not i18n keys (design.md
  decision #11); only the link labels are translated.
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
import { REPO_URL, README_URL, DECK_URL } from '../config/links'
</script>
