<template>
  <div ref="containerRef" class="relative">
    <button
      type="button"
      class="flex items-center gap-1.5 bg-gray-900 border border-gray-700 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:border-brand-500 min-w-28 max-w-[36ch]"
      :class="{ 'border-brand-500': open }"
      @click="toggleOpen"
    >
      <span class="flex-1 truncate text-left" :class="modelValue.length ? 'text-gray-300' : 'text-gray-500'">{{ displayLabel }}</span>
      <svg class="w-3.5 h-3.5 text-gray-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
      </svg>
    </button>

    <div
      v-if="open"
      class="absolute z-50 mt-1 bg-gray-900 border border-gray-800 rounded-xl shadow-xl min-w-full overflow-hidden"
    >
      <!-- Search input (optional) -->
      <div v-if="showSearch" class="p-2 border-b border-gray-800">
        <input
          ref="searchInputRef"
          v-model="search"
          type="text"
          :placeholder="`Search ${placeholder.toLowerCase()}...`"
          class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
          @keydown.escape="open = false"
        >
      </div>

      <!-- Options list -->
      <ul class="max-h-56 overflow-y-auto py-1">
        <li v-if="filteredOptions.length === 0" class="px-3 py-2 text-sm text-gray-500">
          No options found
        </li>
        <li
          v-for="option in filteredOptions"
          :key="option.value"
          class="flex items-center gap-2 px-3 py-1.5 text-sm cursor-pointer transition-colors select-none"
          :class="isSelected(option.value) ? 'bg-brand-600/10 text-gray-200' : 'text-gray-300 hover:bg-gray-800'"
          @click="toggleOption(option.value)"
        >
          <!-- Checkbox -->
          <span
            class="flex-shrink-0 w-3.5 h-3.5 border rounded flex items-center justify-center"
            :class="isSelected(option.value) ? 'bg-brand-500 border-brand-500' : 'border-gray-600'"
          >
            <svg v-if="isSelected(option.value)" class="w-2.5 h-2.5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7" />
            </svg>
          </span>
          <!-- Colored dot (e.g. for status) -->
          <span v-if="option.dotClass" class="w-1.5 h-1.5 rounded-full shrink-0" :class="option.dotClass" />
          <span class="flex-1 truncate">{{ option.label }}</span>
        </li>
      </ul>
    </div>
  </div>
</template>

<script setup lang="ts">
export interface MultiSelectOption {
  value: string
  label: string
  dotClass?: string
}

const props = withDefaults(defineProps<{
  modelValue: string[]
  options: MultiSelectOption[]
  placeholder: string
  showSearch?: boolean
}>(), {
  showSearch: true,
})

const emit = defineEmits<{
  'update:modelValue': [value: string[]]
}>()

const open = ref(false)
const search = ref('')
const containerRef = ref<HTMLElement | null>(null)
const searchInputRef = ref<HTMLInputElement | null>(null)

const filteredOptions = computed(() => {
  if (!props.showSearch || !search.value) return props.options
  const q = search.value.toLowerCase()
  return props.options.filter(o => o.label.toLowerCase().includes(q))
})

const displayLabel = computed(() => {
  if (props.modelValue.length === 0) return props.placeholder
  if (props.modelValue.length === 1) {
    return props.options.find(o => o.value === props.modelValue[0])?.label ?? props.placeholder
  }
  return `${props.modelValue.length} selected`
})

function toggleOpen() {
  open.value = !open.value
  if (open.value) {
    search.value = ''
    nextTick(() => searchInputRef.value?.focus())
  }
}

function isSelected(value: string): boolean {
  return props.modelValue.includes(value)
}

function toggleOption(value: string) {
  const current = [...props.modelValue]
  const idx = current.indexOf(value)
  if (idx >= 0) {
    current.splice(idx, 1)
  }
  else {
    current.push(value)
  }
  emit('update:modelValue', current)
}

onMounted(() => {
  document.addEventListener('mousedown', onOutsideClick)
})

onUnmounted(() => {
  document.removeEventListener('mousedown', onOutsideClick)
})

function onOutsideClick(e: MouseEvent) {
  if (containerRef.value && !containerRef.value.contains(e.target as Node)) {
    open.value = false
  }
}
</script>
