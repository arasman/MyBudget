<template>
  <div class="container mx-auto max-w-2xl px-4 py-8">
    <!-- No budgets -->
    <div v-if="memberships.length === 0" class="text-center py-16">
      <p class="text-base-content/60 text-lg">
        You are not a member of any budget yet.
      </p>
    </div>

    <!-- Multiple budgets: show selection list -->
    <template v-else>
      <h1 class="text-2xl font-semibold mb-6">Select a budget</h1>
      <ul class="space-y-3">
        <li
          v-for="m in memberships"
          :key="m.budgetId"
        >
          <button
            type="button"
            class="card card-border w-full text-left hover:bg-base-200 transition-colors cursor-pointer"
            @click="selectBudget(m.budgetId, m.budgetName)"
          >
            <div class="card-body flex-row items-center justify-between py-4">
              <span class="font-medium text-base">{{ m.budgetName }}</span>
              <span class="badge badge-outline capitalize">{{ m.role }}</span>
            </div>
          </button>
        </li>
      </ul>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth.store'
import { useLayoutStore } from '@/stores/layout.store'

const authStore = useAuthStore()
const layoutStore = useLayoutStore()
const router = useRouter()

const memberships = computed(() => authStore.user?.memberships ?? [])

function selectBudget(budgetId: string, budgetName: string): void {
  layoutStore.setActiveBudget(budgetId, budgetName)
  router.push({ name: 'CycleList', params: { budgetId } })
}

onMounted(() => {
  // Auto-redirect when the user belongs to exactly one budget.
  if (memberships.value.length === 1) {
    const m = memberships.value[0]!
    selectBudget(m.budgetId, m.budgetName)
  }
})
</script>
