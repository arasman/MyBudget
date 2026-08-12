<!--
  Curated feature showcase (LANDING-2): 9 tiles, one per feature area, in a
  responsive "train tracks" grid — 1 column on mobile, more on wider
  viewports (LANDING-7).
-->
<template>
  <section class="px-4 py-10 max-w-6xl mx-auto">
    <div
      data-testid="flow-showcase-grid"
      class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6"
    >
      <ShowcaseTile v-for="item in SHOWCASE_ITEMS" :key="item.slug" :item="item" />
    </div>
  </section>
</template>

<script setup lang="ts">
import ShowcaseTile from './ShowcaseTile.vue'
import { SHOWCASE_ITEMS } from '../config/showcase'
</script>
