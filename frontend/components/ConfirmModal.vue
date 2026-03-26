<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
    <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-2xl w-full max-w-md p-6 mx-4">
      <h3 class="text-lg font-semibold text-white mb-2">{{ title }}</h3>
      <p class="text-sm text-gray-400 mb-6">{{ message }}</p>
      <div class="flex items-center justify-end gap-3">
        <button
          class="text-sm text-gray-400 hover:text-gray-200 px-4 py-2 rounded-lg border border-gray-700 hover:bg-gray-800 transition-colors"
          @click="$emit('cancel')"
        >
          Cancel
        </button>
        <button
          :class="confirmClass ?? 'bg-red-600 hover:bg-red-700'"
          class="text-sm text-white px-4 py-2 rounded-lg font-medium transition-colors"
          @click="$emit('confirm')"
        >
          {{ confirmLabel ?? 'Delete' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  title: string
  message: string
  confirmLabel?: string
  confirmClass?: string
}>()

defineEmits<{
  confirm: []
  cancel: []
}>()
</script>
