<template>
  <dialog
    open
    class="modal modal-open"
  >
    <div class="modal-box">
      <h3 class="font-bold text-lg mb-2">
        {{ t('currentSituation.deleteModal.title') }}
      </h3>
      <p class="text-sm text-base-content/70 mb-4">
        {{ t('currentSituation.deleteModal.instruction', { date: cutDate }) }}
      </p>

      <div class="flex flex-col gap-1 mb-4">
        <span class="label-text text-sm">{{ t('currentSituation.deleteModal.typeDate') }}</span>
        <input
          v-model="typedDate"
          type="text"
          class="input input-bordered w-full"
          :placeholder="cutDate"
          autocomplete="off"
        >
      </div>

      <div class="flex justify-end gap-2">
        <button
          class="btn btn-ghost"
          @click="emit('cancel')"
        >
          {{ t('common.cancel') }}
        </button>
        <button
          class="btn btn-error"
          :disabled="!isConfirmed || loading"
          @click="emit('confirm')"
        >
          <span
            v-if="loading"
            class="loading loading-spinner loading-xs"
          />
          {{ t('currentSituation.deleteModal.confirm') }}
        </button>
      </div>
    </div>
    <form
      method="dialog"
      class="modal-backdrop"
      @click="emit('cancel')"
    >
      <button>close</button>
    </form>
  </dialog>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{
  cutDate: string
  loading?: boolean
}>()

const emit = defineEmits<{
  confirm: []
  cancel: []
}>()

const { t } = useI18n()

const typedDate = ref('')

const isConfirmed = computed(() => typedDate.value === props.cutDate)
</script>
