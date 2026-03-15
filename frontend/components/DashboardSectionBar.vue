<template>
  <div class="mb-1.5 rounded-lg bg-gray-900/60 border border-gray-800 px-2 py-1.5 flex flex-wrap items-center gap-x-3 gap-y-1">
    <!-- Drag handle + label -->
    <div class="flex items-center gap-1.5 text-xs text-amber-400/80 cursor-grab mr-auto">
      <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 24 24">
        <circle cx="9" cy="6" r="1.5"/><circle cx="9" cy="12" r="1.5"/><circle cx="9" cy="18" r="1.5"/>
        <circle cx="15" cy="6" r="1.5"/><circle cx="15" cy="12" r="1.5"/><circle cx="15" cy="18" r="1.5"/>
      </svg>
      <span class="font-semibold">{{ label }}</span>
    </div>
    <!-- Display mode pills -->
    <div v-if="displayModes?.length" class="flex items-center gap-0.5">
      <button
        v-for="mode in displayModes" :key="mode"
        @click.stop="$emit('display-mode-change', mode)"
        :class="currentDisplayMode === mode ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
        class="text-xs px-1.5 py-0.5 rounded transition-colors capitalize">{{ mode }}</button>
    </div>
    <!-- Max items -->
    <div v-if="hasMaxItems && currentDisplayMode !== 'count'" class="flex items-center gap-0.5">
      <span class="text-xs text-gray-600">#</span>
      <button
        v-for="n in maxItemsOptions" :key="n"
        @click.stop="$emit('max-items-change', n)"
        :class="currentMaxItems === n ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
        class="text-xs w-5 h-5 flex items-center justify-center rounded transition-colors">{{ n }}</button>
    </div>
    <!-- Width buttons -->
    <div v-if="widths.length" class="flex items-center gap-0.5">
      <button
        v-for="w in widths" :key="w.value"
        @click.stop="$emit('width-change', w.value)"
        :class="currentWidth === w.value ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
        class="text-xs px-1.5 py-0.5 rounded transition-colors">{{ w.label }}</button>
    </div>
    <!-- Tab with next (click or drop) -->
    <button
      v-if="canTab"
      @click.stop="$emit('tab-toggle')"
      @dragover.prevent="tabDragOver = true"
      @dragleave="tabDragOver = false"
      @drop.prevent="onTabDrop"
      class="text-xs px-1.5 py-0.5 rounded transition-colors bg-gray-800 hover:bg-gray-700"
      :class="[
        isTabbed ? 'text-brand-400' : 'text-gray-500 hover:text-gray-300',
        tabDragOver || dragHover ? 'ring-2 ring-brand-400 text-brand-300 bg-gray-700' : '',
      ]"
      :title="isTabbed ? 'Ungroup from next' : 'Combine with next as tabs (or drop a section here)'">
      {{ isTabbed ? '⊖ Ungroup' : '⊕ Tab with ↓' }}
    </button>
    <!-- Stack with next (click or drop) -->
    <button
      v-if="canStack"
      @click.stop="$emit('stack-toggle')"
      @dragover.prevent="stackDragOver = true"
      @dragleave="stackDragOver = false"
      @drop.prevent="onStackDrop"
      class="text-xs px-1.5 py-0.5 rounded transition-colors bg-gray-800 hover:bg-gray-700"
      :class="[
        isStacked ? 'text-teal-400' : 'text-gray-500 hover:text-gray-300',
        stackDragOver || dragHover ? 'ring-2 ring-teal-400 text-teal-300 bg-gray-700' : '',
      ]"
      :title="isStacked ? 'Unstack from next' : 'Stack with next section (or drop a section here)'">
      {{ isStacked ? '⊖ Unstack' : '⇕ Stack with ↓' }}
    </button>
    <!-- Hide/Show -->
    <button
      @click.stop="$emit(hidden ? 'show' : 'hide')"
      :class="hidden ? 'text-green-400' : 'text-gray-400 hover:text-red-400'"
      class="text-xs px-1.5 py-0.5 rounded bg-gray-800 hover:bg-gray-700 transition-colors">
      {{ hidden ? '+ Show' : '✕ Hide' }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

defineProps<{
  label: string
  displayModes?: string[]
  currentDisplayMode?: string
  hasMaxItems?: boolean
  maxItemsOptions: number[]
  currentMaxItems?: number
  widths: { value: string; label: string }[]
  currentWidth: string
  canTab?: boolean
  isTabbed?: boolean
  canStack?: boolean
  isStacked?: boolean
  hidden?: boolean
  dragHover?: boolean
}>()

const emit = defineEmits<{
  'display-mode-change': [mode: string]
  'max-items-change': [n: number]
  'width-change': [value: string]
  'tab-toggle': []
  'tab-drop': [droppedSid: string]
  'stack-toggle': []
  'stack-drop': [droppedSid: string]
  hide: []
  show: []
}>()

const tabDragOver = ref(false)
const stackDragOver = ref(false)

function onTabDrop(e: DragEvent) {
  tabDragOver.value = false
  const sid = e.dataTransfer?.getData('text/plain')
  if (sid) emit('tab-drop', sid)
}

function onStackDrop(e: DragEvent) {
  stackDragOver.value = false
  const sid = e.dataTransfer?.getData('text/plain')
  if (sid) emit('stack-drop', sid)
}
</script>
