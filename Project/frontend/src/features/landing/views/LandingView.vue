<template>
  <PublicBackdrop>
    <!--
      Real landing page (PR 4, task 4.9): LandingHero + FlowShowcase + LandingCta +
      LandingLinks inside the PublicBackdrop + AppFooter shell wired in PR 1/PR 2.
      Must never issue any /api/* call — anonymous visitors reach this with zero auth state
      (LANDING-1). LanguageSwitcher is mounted here (not via PublicLayout) because RootGate
      renders LandingView directly, bypassing PublicLayout (design.md Decision 1) — LANDING-6
      still requires it to work on the landing.
    -->
    <div
      data-testid="landing-view"
      class="min-h-screen flex flex-col"
    >
      <header class="flex justify-end px-4 py-4">
        <LanguageSwitcher />
      </header>
      <main class="flex-1">
        <LandingHero />
        <FlowShowcase />
        <LandingCta />
        <LandingLinks />
      </main>
      <AppFooter />
    </div>
  </PublicBackdrop>
</template>

<script setup lang="ts">
import PublicBackdrop from '@/components/PublicBackdrop.vue'
import AppFooter from '@/components/AppFooter.vue'
import LanguageSwitcher from '@/components/LanguageSwitcher.vue'
import LandingHero from '../components/LandingHero.vue'
import FlowShowcase from '../components/FlowShowcase.vue'
import LandingCta from '../components/LandingCta.vue'
import LandingLinks from '../components/LandingLinks.vue'
</script>
