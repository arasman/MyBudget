<template>
  <div class="flex flex-col items-center justify-center py-16 text-center">
    <div class="card bg-base-200 shadow-sm max-w-sm w-full">
      <div class="card-body items-center">
        <!-- Icon -->
        <div class="text-base-content/30 mb-2">
          <svg
            xmlns="http://www.w3.org/2000/svg"
            class="h-16 w-16"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="1"
              d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"
            />
          </svg>
        </div>

        <!-- Title -->
        <h3 class="card-title text-base-content/70">
          {{ title }}
        </h3>

        <!-- Description -->
        <p class="text-base-content/50 text-sm">
          {{ description }}
        </p>

        <!-- Optional action button -->
        <div
          v-if="actionLabel && action"
          class="card-actions mt-2"
        >
          <button
            type="button"
            class="btn btn-primary"
            @click="action"
          >
            {{ actionLabel }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  title: string
  description: string
  actionLabel?: string
  action?: () => void
}>()
</script>
