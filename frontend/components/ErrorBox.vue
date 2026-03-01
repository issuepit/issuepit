<template>
  <div v-if="error" class="mb-4 bg-red-900/20 border border-red-800 rounded-lg px-4 py-3 text-sm text-red-400">
    {{ error }}
  </div>
</template>

<script setup lang="ts">
defineProps<{ error: string | null | undefined }>()
</script>
