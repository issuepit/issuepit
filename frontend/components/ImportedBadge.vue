<template>
  <span
    v-if="sourceFile"
    class="ml-2 inline-flex items-center gap-1 text-xs text-blue-400 bg-blue-900/20 border border-blue-700/40 px-1.5 py-0.5 rounded font-mono align-middle"
    :title="`Set by config file: ${sourceFile}`"
  >
    <svg class="w-3 h-3 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
    </svg>
    {{ sourceFile }}
  </span>
</template>

<script setup lang="ts">
defineProps<{
  sourceFile?: string | null
}>()
</script>
