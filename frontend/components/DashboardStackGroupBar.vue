<template>
  <div data-no-reorder class="mb-1.5 rounded-lg bg-gray-900/60 border border-gray-800 px-2 py-1.5 flex items-center gap-3 flex-wrap">
    <!-- Drag handle + label -->
    <div class="flex items-center gap-1.5 text-xs text-teal-400/80 cursor-grab">
      <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 24 24">
        <circle cx="9" cy="6" r="1.5"/><circle cx="9" cy="12" r="1.5"/><circle cx="9" cy="18" r="1.5"/>
        <circle cx="15" cy="6" r="1.5"/><circle cx="15" cy="12" r="1.5"/><circle cx="15" cy="18" r="1.5"/>
      </svg>
      <span class="font-semibold text-teal-300">Stack group:</span>
      <span class="text-teal-400/80">{{ sections.map(s => sectionLabels[s] ?? s).join(' + ') }}</span>
    </div>
    <!-- Width selector -->
    <div v-if="widths.length" class="flex items-center gap-0.5">
      <button
        v-for="w in widths" :key="w.value"
        @click.stop="$emit('width-change', w.value)"
        :class="currentWidth === w.value ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
        class="text-xs px-1.5 py-0.5 rounded transition-colors">{{ w.label }}</button>
    </div>
    <!-- Normal unstack button (shown when not dragging) -->
    <button
      v-if="!isDragging"
      @click.stop="$emit('split')"
      class="text-xs px-2 py-0.5 rounded bg-gray-800 hover:bg-gray-700 text-teal-400 hover:text-red-400 transition-colors ml-auto">
      ⊖ Unstack
    </button>
    <!-- Drop zone — shown while another card is being dragged (becomes "Stack here") -->
    <button
      v-else
      @dragover.prevent
      @dragenter.prevent="dropHover = true"
      @dragleave="dropHover = false"
      @drop.prevent="onDrop"
      :class="dropHover
        ? 'ring-2 ring-teal-400 bg-teal-900/40 text-teal-300'
        : 'bg-gray-800/60 text-teal-400/60 border border-dashed border-teal-700/50'"
      class="text-xs px-2 py-0.5 rounded transition-all ml-auto">
      ⊕ Stack here
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

defineProps<{
  sections: string[]
  sectionLabels: Record<string, string>
  widths: { value: string; label: string }[]
  currentWidth: string
  isDragging: boolean
}>()

const emit = defineEmits<{
  split: []
  'width-change': [value: string]
  'stack-drop': [droppedSid: string]
}>()

const dropHover = ref(false)

function onDrop(e: DragEvent) {
  dropHover.value = false
  const sid = e.dataTransfer?.getData('text/plain')
  if (sid) emit('stack-drop', sid)
}
</script>
