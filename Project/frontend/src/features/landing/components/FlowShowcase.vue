<!--
  Curated feature showcase (LANDING-2): 9 tiles, one per feature area, in a
  responsive "train tracks" grid — 1 column on mobile, more on wider
  viewports (LANDING-7).

  LANDING-9: owns the single `activeSlug` state via `useShowcaseZoom()`
  (design.md decision #1). Each tile lives inside its own `.showcase-cell`
  (a `relative` grid cell — main.css) so the active tile's absolute
  positioning resolves its calc() percentages against one column, not the
  whole grid. `@mouseleave` is bound to the GRID CONTAINER, never a single
  tile (design.md decision #4) — the enlarged card overlaps neighbours, so
  per-tile leave would oscillate.
-->
<template>
  <section class="px-4 py-10 max-w-6xl mx-auto">
    <div
      data-testid="flow-showcase-grid"
      class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6"
      @mouseleave="deactivate"
    >
      <div
        v-for="(item, index) in SHOWCASE_ITEMS"
        :key="item.slug"
        class="showcase-cell"
        :class="{ 'showcase-cell--active': activeSlug === item.slug }"
      >
        <ShowcaseTile
          :item="item"
          :active="activeSlug === item.slug"
          :dimmed="activeSlug !== null && activeSlug !== item.slug"
          :zoom-vars="zoomVars(index)"
          @hover-in="hoverIn"
          @hover-out="hoverOut"
          @activate="activateNow"
        />
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import ShowcaseTile from './ShowcaseTile.vue'
import { SHOWCASE_ITEMS } from '../config/showcase'
import { useShowcaseZoom } from '../composables/useShowcaseZoom'

const { activeSlug, hoverIn, hoverOut, activateNow, deactivate, zoomVars } =
  useShowcaseZoom(SHOWCASE_ITEMS)
</script>
