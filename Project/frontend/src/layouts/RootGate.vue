<script setup lang="ts">
// Component gate for "/": renders the public landing page for anonymous visitors,
// and today's authenticated budget-selection experience (via AppLayout) otherwise.
// See design.md Architecture Decision #1 — this keeps "/" structurally unreachable
// from any budget URL, instead of nesting RootGate around every authed route.
import { useAuthStore } from '@/stores/auth.store'
import AppLayout from '@/layouts/AppLayout.vue'
import BudgetSelectionView from '@/features/budget-structure/views/BudgetSelectionView.vue'
import LandingView from '@/features/landing/views/LandingView.vue'

const authStore = useAuthStore()
</script>

<template>
  <AppLayout v-if="authStore.isAuthenticated">
    <BudgetSelectionView />
  </AppLayout>
  <LandingView v-else />
</template>
