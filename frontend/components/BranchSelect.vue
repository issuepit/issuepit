<template>
  <div ref="containerRef" class="relative">
    <button
      type="button"
      :class="[
        'flex items-center gap-1.5 bg-gray-900 border border-gray-800 rounded-lg px-3 py-1.5 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500 overflow-hidden',
        props.full ? 'w-full' : 'min-w-28 max-w-[50ch]',
        open ? 'ring-2 ring-brand-500' : '',
      ]"
      :aria-expanded="open"
      @click="toggleOpen"
    >
      <svg class="w-3.5 h-3.5 text-gray-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 7a2 2 0 012-2h1a2 2 0 012 2v1a2 2 0 01-2 2H5a2 2 0 01-2-2V7zm0 10a2 2 0 012-2h1a2 2 0 012 2v1a2 2 0 01-2 2H5a2 2 0 01-2-2v-1zm11-9a2 2 0 012-2h1a2 2 0 012 2v1a2 2 0 01-2 2h-1a2 2 0 01-2-2V8zm-3.5 5a1.5 1.5 0 100-3 1.5 1.5 0 000 3zm0 0v3m-3-3h6" />
      </svg>
      <span class="flex-1 truncate text-left">{{ modelValue || placeholder }}</span>
      <svg class="w-3.5 h-3.5 text-gray-500 shrink-0 ml-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
      </svg>
    </button>

    <div
      v-if="open"
      class="absolute z-50 mt-1 bg-gray-900 border border-gray-800 rounded-xl shadow-xl min-w-full max-w-[50ch] overflow-hidden"
    >
      <!-- Search input -->
      <div class="p-2 border-b border-gray-800">
        <input
          ref="searchInputRef"
          v-model="search"
          type="text"
          aria-label="Search branches"
          placeholder="Search branches..."
          class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
          @keydown.escape="open = false"
          @keydown.down.prevent="moveFocus(1)"
          @keydown.up.prevent="moveFocus(-1)"
          @keydown.enter.prevent="selectFocused"
        >
      </div>

      <!-- Branch list -->
      <ul ref="listRef" class="max-h-56 overflow-y-auto py-1">
        <!-- Free-form entry: when no branch exactly matches the typed query -->
        <li
          v-if="props.allowFreeForm && search && !filtered.some(b => b.name === search)"
          class="flex items-center gap-2 px-3 py-1.5 text-sm cursor-pointer text-brand-300 hover:bg-gray-800 transition-colors"
          @click="select(search)"
        >
          <span class="font-mono truncate flex-1">{{ search }}</span>
          <span class="text-xs text-gray-500 shrink-0">Use this</span>
        </li>
        <li v-if="filtered.length === 0 && !(props.allowFreeForm && search)" class="px-3 py-2 text-sm text-gray-500">No branches found</li>
        <li
          v-for="(branch, i) in filtered"
          :key="branch.name"
          class="flex items-center gap-2 px-3 py-1.5 text-sm cursor-pointer transition-colors"
          :class="[
            branch.name === modelValue ? 'bg-brand-600/20 text-brand-300' : 'text-gray-300 hover:bg-gray-800',
            focusedIndex === i ? 'bg-gray-800' : ''
          ]"
          @click="select(branch.name)"
        >
          <span class="flex-1 truncate font-mono">{{ branch.name }}</span>
          <span v-if="branch.isRemote" class="text-xs text-gray-600 shrink-0">remote</span>
          <svg v-if="branch.name === modelValue" class="w-3.5 h-3.5 text-brand-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
          </svg>
        </li>
      </ul>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { GitBranch } from '~/types'

const props = withDefaults(defineProps<{
  modelValue: string
  branches: GitBranch[]
  placeholder?: string
  /** When true, the user can type any value and press Enter to accept it even if it's not in the list. */
  allowFreeForm?: boolean
  /** When true, the trigger button fills its container width. */
  full?: boolean
}>(), {
  placeholder: 'Select branch',
  allowFreeForm: false,
  full: false,
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const open = ref(false)
const search = ref('')
const focusedIndex = ref(-1)
const containerRef = ref<HTMLElement | null>(null)
const searchInputRef = ref<HTMLInputElement | null>(null)
const listRef = ref<HTMLElement | null>(null)

const filtered = computed(() => {
  const q = search.value.toLowerCase()
  if (!q) return props.branches
  return props.branches.filter(b => b.name.toLowerCase().includes(q))
})

function toggleOpen() {
  open.value = !open.value
  if (open.value) {
    search.value = ''
    focusedIndex.value = -1
    nextTick(() => searchInputRef.value?.focus())
  }
}

function select(name: string) {
  emit('update:modelValue', name)
  open.value = false
}

function moveFocus(delta: number) {
  const max = filtered.value.length - 1
  if (max < 0) return
  focusedIndex.value = Math.max(0, Math.min(max, focusedIndex.value + delta))
  nextTick(() => {
    const items = listRef.value?.querySelectorAll('li')
    items?.[focusedIndex.value]?.scrollIntoView({ block: 'nearest' })
  })
}

function selectFocused() {
  if (focusedIndex.value >= 0 && focusedIndex.value < filtered.value.length) {
    select(filtered.value[focusedIndex.value].name)
  } else if (props.allowFreeForm && search.value.trim()) {
    select(search.value.trim())
  }
}

// Close on outside click
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

// Reset focused index when search changes
watch(search, () => {
  focusedIndex.value = -1
})
</script>
