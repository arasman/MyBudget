<!--
  Shared decorative backdrop for anonymous-facing shells (PublicLayout, LandingView).
  Decision 7: a viewport-fixed gradient layer behind a `relative` slot wrapper
  renders identically behind a short centered auth card and a long scrolling
  landing page — a single component serves both without per-page tuning.
-->
<template>
  <div class="relative">
    <div
      class="fixed inset-0 -z-10 pointer-events-none"
      style="background: radial-gradient(circle at top, var(--brand-backdrop-from), transparent 60%)"
      aria-hidden="true"
    />
    <slot />
  </div>
</template>
