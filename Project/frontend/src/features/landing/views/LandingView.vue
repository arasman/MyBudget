<template>
  <!--
    Placeholder stub for PR 1 (router restructure only). PR 4 replaces this with the
    real landing page: PublicBackdrop + LandingHero + FlowShowcase + LandingCta + LandingLinks.
    Must never issue any /api/* call — anonymous visitors reach this with zero auth state.
  -->
  <div data-testid="landing-view" class="min-h-screen flex items-center justify-center bg-base-200 px-4">
    <h1 class="text-2xl font-semibold">{{ $t('common.appName') }}</h1>
  </div>
</template>

<script setup lang="ts"></script>
