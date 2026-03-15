<template>
  <div class="mb-1.5 rounded-lg bg-gray-900/60 border border-gray-800 px-2 py-1.5 flex items-center gap-3 flex-wrap">
    <!-- Drag handle + label -->
    <div class="flex items-center gap-1.5 text-xs text-amber-400/80 cursor-grab">
      <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 24 24">
        <circle cx="9" cy="6" r="1.5"/><circle cx="9" cy="12" r="1.5"/><circle cx="9" cy="18" r="1.5"/>
        <circle cx="15" cy="6" r="1.5"/><circle cx="15" cy="12" r="1.5"/><circle cx="15" cy="18" r="1.5"/>
      </svg>
      <span class="font-semibold text-amber-300">Tab group:</span>
      <span class="text-amber-400/80">{{ sections.map(s => sectionLabels[s] ?? s).join(' + ') }}</span>
    </div>
    <!-- Width selector -->
    <div v-if="widths.length" class="flex items-center gap-0.5">
      <button
        v-for="w in widths" :key="w.value"
        @click.stop="$emit('width-change', w.value)"
        :class="currentWidth === w.value ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
        class="text-xs px-1.5 py-0.5 rounded transition-colors">{{ w.label }}</button>
    </div>
    <!-- Split button -->
    <button
      @click.stop="$emit('split')"
      class="text-xs px-2 py-0.5 rounded bg-gray-800 hover:bg-gray-700 text-brand-400 hover:text-red-400 transition-colors ml-auto">
      ⊖ Split tabs
    </button>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  sections: string[]
  sectionLabels: Record<string, string>
  widths: { value: string; label: string }[]
  currentWidth: string
}>()

defineEmits<{
  split: []
  'width-change': [value: string]
}>()
</script>
