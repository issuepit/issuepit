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
    <!-- Tab with next -->
    <button
      v-if="canTab"
      @click.stop="$emit('tab-toggle')"
      class="text-xs px-1.5 py-0.5 rounded transition-colors bg-gray-800 hover:bg-gray-700"
      :class="isTabbed ? 'text-brand-400' : 'text-gray-500 hover:text-gray-300'"
      :title="isTabbed ? 'Ungroup from next' : 'Combine with next as tabs'">
      {{ isTabbed ? '⊖ Ungroup' : '⊕ Tab with ↓' }}
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
  hidden?: boolean
}>()

defineEmits<{
  'display-mode-change': [mode: string]
  'max-items-change': [n: number]
  'width-change': [value: string]
  'tab-toggle': []
  hide: []
  show: []
}>()
</script>
