<template>
  <div class="relative" ref="rootRef">
    <div
      class="flex items-center w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm cursor-pointer focus-within:ring-2 focus-within:ring-brand-500"
      :class="open ? 'border-brand-500' : 'border-gray-700'"
      data-testid="agent-search-dropdown-trigger"
      @click="toggleOpen"
    >
      <span v-if="selectedAgent" class="flex items-center gap-2 flex-1 text-white">
        <span class="w-5 h-5 rounded-full bg-brand-700 flex items-center justify-center text-xs text-brand-200 shrink-0">
          {{ selectedAgent.name.charAt(0).toUpperCase() }}
        </span>
        <span class="truncate">{{ selectedAgent.name }}</span>
      </span>
      <span v-else-if="modelValue === ''" class="flex-1 text-gray-400 italic">Unassigned</span>
      <span v-else class="flex-1 text-gray-500">{{ placeholder }}</span>
      <svg class="w-4 h-4 text-gray-500 shrink-0 ml-2 transition-transform" :class="open ? 'rotate-180' : ''" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
      </svg>
    </div>

    <Teleport to="body">
      <div
        v-if="open"
        class="fixed z-[9999] bg-gray-850 border border-gray-700 rounded-lg shadow-xl overflow-hidden"
        :style="dropdownStyle"
      >
        <!-- Search input -->
        <div class="p-2 border-b border-gray-700">
          <input
            ref="searchRef"
            v-model="search"
            type="text"
            placeholder="Search agents..."
            class="w-full bg-gray-800 border border-gray-700 rounded px-2.5 py-1.5 text-xs text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
            @click.stop
            @keydown.escape.stop="open = false"
          />
        </div>

        <!-- Options list -->
        <div class="max-h-52 overflow-y-auto">
          <!-- Unassigned option -->
          <button
            type="button"
            class="w-full flex items-center gap-2 px-3 py-2 text-sm text-left transition-colors hover:bg-gray-700"
            :class="modelValue === '' ? 'bg-gray-700/50 text-white' : 'text-gray-400'"
            @click="selectAgent(null)"
          >
            <span class="w-5 h-5 rounded-full bg-gray-700 flex items-center justify-center text-xs shrink-0">—</span>
            <span class="italic">Unassigned</span>
            <svg v-if="modelValue === ''" class="w-3 h-3 ml-auto text-brand-400 shrink-0" fill="currentColor" viewBox="0 0 20 20">
              <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 00-1.414 0L8 12.586 4.707 9.293a1 1 0 00-1.414 1.414l4 4a1 1 0 001.414 0l8-8a1 1 0 000-1.414z" clip-rule="evenodd" />
            </svg>
          </button>

          <!-- Agent options -->
          <button
            v-for="agent in filteredAgents"
            :key="agent.id"
            type="button"
            class="w-full flex items-center gap-2 px-3 py-2 text-sm text-left transition-colors hover:bg-gray-700"
            :class="modelValue === agent.id ? 'bg-gray-700/50 text-white' : 'text-gray-300'"
            @click="selectAgent(agent)"
          >
            <span class="w-5 h-5 rounded-full bg-brand-700 flex items-center justify-center text-xs text-brand-200 shrink-0">
              {{ agent.name.charAt(0).toUpperCase() }}
            </span>
            <span class="truncate">{{ agent.name }}</span>
            <svg v-if="modelValue === agent.id" class="w-3 h-3 ml-auto text-brand-400 shrink-0" fill="currentColor" viewBox="0 0 20 20">
              <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 00-1.414 0L8 12.586 4.707 9.293a1 1 0 00-1.414 1.414l4 4a1 1 0 001.414 0l8-8a1 1 0 000-1.414z" clip-rule="evenodd" />
            </svg>
          </button>

          <div v-if="!filteredAgents.length" class="px-3 py-2 text-xs text-gray-500">
            No agents found
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import type { Agent } from '~/types'

const props = withDefaults(defineProps<{
  modelValue: string | null | undefined
  agents: Agent[]
  placeholder?: string
}>(), {
  placeholder: 'Select agent...',
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const open = ref(false)
const search = ref('')
const rootRef = ref<HTMLElement | null>(null)
const searchRef = ref<HTMLInputElement | null>(null)
const dropdownStyle = ref<Record<string, string>>({})

const DROPDOWN_HEIGHT = 260

const selectedAgent = computed(() =>
  props.modelValue ? props.agents.find(a => a.id === props.modelValue) ?? null : null,
)

const filteredAgents = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return props.agents
  return props.agents.filter(a => a.name.toLowerCase().includes(q))
})

function updateDropdownPosition() {
  if (!rootRef.value) return
  const rect = rootRef.value.getBoundingClientRect()
  const spaceBelow = window.innerHeight - rect.bottom
  if (spaceBelow >= DROPDOWN_HEIGHT || spaceBelow >= 120) {
    dropdownStyle.value = {
      top: `${rect.bottom + window.scrollY + 4}px`,
      left: `${rect.left + window.scrollX}px`,
      width: `${rect.width}px`,
    }
  } else {
    dropdownStyle.value = {
      bottom: `${window.innerHeight - rect.top + window.scrollY + 4}px`,
      left: `${rect.left + window.scrollX}px`,
      width: `${rect.width}px`,
    }
  }
}

async function toggleOpen() {
  open.value = !open.value
  if (open.value) {
    updateDropdownPosition()
    await nextTick()
    searchRef.value?.focus()
  }
}

function selectAgent(agent: Agent | null) {
  emit('update:modelValue', agent?.id ?? '')
  open.value = false
  search.value = ''
}

function handleClickOutside(e: MouseEvent) {
  if (!rootRef.value) return
  const target = e.target as Node
  // Check if clicked inside trigger
  if (rootRef.value.contains(target)) return
  // Check if clicked inside dropdown (Teleported, so outside rootRef but still ours)
  // We close on any outside click since dropdown is in body
  open.value = false
}

onMounted(() => {
  document.addEventListener('mousedown', handleClickOutside)
})

onBeforeUnmount(() => {
  document.removeEventListener('mousedown', handleClickOutside)
})
</script>
