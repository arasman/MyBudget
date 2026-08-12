<!--
  One "train tracks" showcase tile (LANDING-2): a curated flow screenshot
  framed to a uniform aspect ratio (design.md decision #9) plus i18n
  title/caption. Consumes public/showcase/{slug}-{640,1280}.webp generated
  by scripts/build-showcase.mjs (PR 3) — never imports the raw source PNG.

  LANDING-9: root is a real <button> (design.md decision #1/#3) so focus and
  Enter/Space activation are free — no manual @keydown wiring. `active` /
  `dimmed` / `zoomVars` are pure presentational props driven by
  FlowShowcase's `useShowcaseZoom()`; this component stays testable
  standalone, with no matchMedia/composable knowledge of its own.
-->
<template>
  <button
    type="button"
    data-testid="showcase-tile"
    :data-showcase-slug="item.slug"
    :aria-label="$t('landing.showcase.enlarge', { title: $t(`${item.i18nKey}.title`) })"
    :inert="dimmed || undefined"
    :aria-hidden="dimmed ? 'true' : undefined"
    :class="[
      'block w-full text-left bg-transparent border-0 p-0 m-0 cursor-pointer',
      active ? 'showcase-zoom-card' : '',
      dimmed ? 'sm:opacity-40' : '',
    ]"
    :style="active ? zoomVars : undefined"
    @mouseenter="emit('hover-in', item.slug)"
    @mouseleave="emit('hover-out')"
    @focus="emit('activate', item.slug)"
    @click="emit('activate', item.slug)"
  >
    <figure class="flex flex-col gap-2">
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
        <span v-if="active" class="sr-only">{{ $t('landing.showcase.dismissHint') }}</span>
      </figcaption>
    </figure>
  </button>
</template>

<script setup lang="ts">
import type { ShowcaseItem } from '../config/showcase'

withDefaults(
  defineProps<{
    item: ShowcaseItem
    active?: boolean
    dimmed?: boolean
    zoomVars?: Record<string, string>
  }>(),
  {
    active: false,
    dimmed: false,
    zoomVars: () => ({}),
  },
)

const emit = defineEmits<{
  'hover-in': [slug: string]
  'hover-out': []
  activate: [slug: string]
}>()
</script>
