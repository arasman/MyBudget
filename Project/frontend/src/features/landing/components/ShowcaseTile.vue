<!--
  One "train tracks" showcase tile (LANDING-2): a curated flow screenshot
  framed to a uniform aspect ratio (design.md decision #9) plus i18n
  title/caption. Consumes public/showcase/{slug}-{640,1280}.webp generated
  by scripts/build-showcase.mjs (PR 3) — never imports the raw source PNG.
-->
<template>
  <figure data-testid="showcase-tile" class="flex flex-col gap-2">
    <picture>
      <source
        type="image/webp"
        :srcset="`/showcase/${item.slug}-640.webp 640w, /showcase/${item.slug}-1280.webp 1280w`"
        sizes="(min-width: 1024px) 33vw, (min-width: 640px) 50vw, 100vw"
      />
      <img
        :src="`/showcase/${item.slug}-1280.webp`"
        :alt="$t(`${item.i18nKey}.title`)"
        loading="lazy"
        width="1280"
        height="800"
        class="aspect-[16/10] w-full object-cover object-top rounded-lg border border-base-300 shadow"
      />
    </picture>
    <figcaption>
      <h3 class="font-semibold">{{ $t(`${item.i18nKey}.title`) }}</h3>
      <p class="text-sm text-base-content/70">{{ $t(`${item.i18nKey}.caption`) }}</p>
    </figcaption>
  </figure>
</template>

<script setup lang="ts">
import type { ShowcaseItem } from '../config/showcase'

defineProps<{ item: ShowcaseItem }>()
</script>
