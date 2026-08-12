<!--
  Landing hero: value-proposition headline + subhead. Presentational only —
  no auth calls, no store reads (LANDING-1).
-->
<template>
  <section class="px-4 pt-16 pb-10 text-center max-w-3xl mx-auto">
    <h1 class="text-4xl sm:text-5xl font-bold tracking-tight">
      {{ $t('landing.hero.title') }}
    </h1>
    <p class="mt-4 text-lg text-base-content/70">
      {{ $t('landing.hero.subtitle') }}
    </p>
  </section>
</template>
