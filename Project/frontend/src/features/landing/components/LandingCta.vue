<!--
  LANDING-3: sign-up is the primary CTA, sign-in is present but visually
  secondary (design.md decision #12).
-->
<template>
  <section
    data-testid="landing-cta"
    class="px-4 py-8 flex flex-col sm:flex-row items-center justify-center gap-3"
  >
    <RouterLink
      to="/register"
      class="btn btn-primary btn-lg"
    >
      {{ $t('landing.cta.primary') }}
    </RouterLink>
    <RouterLink
      to="/login"
      class="btn btn-ghost"
    >
      {{ $t('landing.cta.secondary') }}
    </RouterLink>
  </section>
</template>
