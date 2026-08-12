<script setup lang="ts">
import { useToastStore } from '@/stores/toast.store'
import type { ToastType } from '@/stores/toast.store'

const toastStore = useToastStore()

function alertClass(type: ToastType): string {
  switch (type) {
    case 'success':
      return 'alert-success'
    case 'error':
      return 'alert-error'
    case 'warning':
      return 'alert-warning'
    case 'info':
    default:
      return 'alert-info'
  }
}
</script>

<template>
  <!-- DaisyUI toast container: bottom-right, above modals (z-[1000]) -->
  <!-- aria-live must be on the static container (present at page load) so screen readers register it -->
  <div
    aria-live="polite"
    aria-atomic="false"
    class="toast toast-end z-[1000] fixed bottom-4 right-4 flex flex-col gap-2"
  >
    <TransitionGroup
      name="toast"
      tag="div"
      class="flex flex-col gap-2"
    >
      <div
        v-for="toast in toastStore.toasts"
        :key="toast.id"
        :class="['alert', alertClass(toast.type), 'shadow-lg min-w-64 max-w-sm']"
        role="alert"
      >
        <span class="flex-1">
          <span class="font-semibold">{{ toast.title }}</span>
          <span
            v-if="toast.message"
            class="block text-sm opacity-80"
          >{{ toast.message }}</span>
        </span>
        <button
          type="button"
          class="btn btn-ghost btn-xs btn-circle ml-2"
          :aria-label="'Close'"
          @click="toastStore.dismiss(toast.id)"
        >
          ✕
        </button>
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toast-enter-active,
.toast-leave-active {
  transition:
    opacity 0.2s ease,
    transform 0.2s ease;
}

.toast-enter-from {
  opacity: 0;
  transform: translateX(100%);
}

.toast-leave-to {
  opacity: 0;
  transform: translateX(100%);
}
</style>
