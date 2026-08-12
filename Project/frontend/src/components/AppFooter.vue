<!--
  Global footer (LAYOUT-4). Plain text only — no outbound/internal links.
  Mounted once per shell (AppLayout, PublicLayout), never in App.vue.
-->
<template>
  <footer
    class="py-4 text-center text-sm"
    :style="{ color: 'var(--brand-footer-fg)' }"
  >
    © {{ currentYear }} · {{ $t('footer.poweredBy') }}
  </footer>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const currentYear = computed(() => new Date().getFullYear())
</script>
