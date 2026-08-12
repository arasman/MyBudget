<template>
  <PublicBackdrop>
    <div class="min-h-screen flex flex-col">
      <!--
        Placeholder stub for PR 1 (router restructure only). PR 4 (task 4.9) replaces this
        inner content with the real landing page: LandingHero + FlowShowcase + LandingCta +
        LandingLinks. The PublicBackdrop + AppFooter shell wiring stays as-is.
        Must never issue any /api/* call — anonymous visitors reach this with zero auth state.
      -->
      <div
        data-testid="landing-view"
        class="flex-1 flex items-center justify-center bg-base-200 px-4"
      >
        <h1 class="text-2xl font-semibold">{{ $t('common.appName') }}</h1>
      </div>
      <AppFooter />
    </div>
  </PublicBackdrop>
</template>

<script setup lang="ts">
import PublicBackdrop from '@/components/PublicBackdrop.vue'
import AppFooter from '@/components/AppFooter.vue'
</script>
