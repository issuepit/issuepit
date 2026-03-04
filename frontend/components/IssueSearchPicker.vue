<template>
  <div class="relative">
    <input
      v-model="search"
      type="text"
      :placeholder="placeholder"
      autofocus
      class="w-full bg-gray-800 border border-gray-700 rounded px-2.5 py-1.5 text-xs text-white placeholder-gray-600 focus:outline-none focus:ring-1 focus:ring-brand-500"
      @keyup.escape="$emit('cancel')"
    >
    <div v-if="search.trim() || showAll" class="mt-1 bg-gray-800 border border-gray-700 rounded overflow-hidden max-h-56 overflow-y-auto">
      <template v-if="currentProjectResults.length">
        <div class="px-2.5 py-1 text-xs text-gray-600 uppercase tracking-wide border-b border-gray-700/60">
          This project
        </div>
        <button
          v-for="issue in currentProjectResults"
          :key="issue.id"
          type="button"
          class="w-full flex items-center gap-2 px-2.5 py-1.5 text-xs text-gray-300 hover:bg-gray-700 hover:text-white text-left transition-colors"
          @click="$emit('select', issue)"
        >
          <span class="text-gray-600 shrink-0">#{{ issue.number }}</span>
          <span class="truncate">{{ issue.title }}</span>
        </button>
      </template>
      <template v-if="otherProjectResults.length">
        <div class="px-2.5 py-1 text-xs text-gray-600 uppercase tracking-wide border-b border-gray-700/60" :class="{ 'border-t border-gray-700/60 mt-0.5': currentProjectResults.length }">
          Other projects
        </div>
        <button
          v-for="issue in otherProjectResults"
          :key="issue.id"
          type="button"
          class="w-full flex items-center gap-2 px-2.5 py-1.5 text-xs text-gray-300 hover:bg-gray-700 hover:text-white text-left transition-colors"
          @click="$emit('select', issue)"
        >
          <span class="text-gray-500 shrink-0 truncate max-w-[80px]">{{ issue.projectName }}</span>
          <span class="text-gray-600 shrink-0">#{{ issue.number }}</span>
          <span class="truncate">{{ issue.title }}</span>
        </button>
      </template>
      <div v-if="!currentProjectResults.length && !otherProjectResults.length" class="px-2.5 py-2 text-xs text-gray-600">
        No matching issues
      </div>
    </div>
    <div v-else class="mt-1 text-xs text-gray-600">
      Type to search issues…
    </div>
    <button
      type="button"
      class="mt-2 text-xs text-gray-600 hover:text-gray-400 transition-colors"
      @click="$emit('cancel')"
    >
      Cancel
    </button>
  </div>
</template>

<script setup lang="ts">
interface OrgIssue {
  id: string
  number: number
  title: string
  projectId: string
  projectName?: string
}

const props = withDefaults(defineProps<{
  modelValue: string
  issues: OrgIssue[]
  currentProjectId: string
  placeholder?: string
  showAll?: boolean
}>(), { showAll: false, placeholder: 'Search issues...' })

const emit = defineEmits<{
  'update:modelValue': [value: string]
  select: [issue: OrgIssue]
  cancel: []
}>()

const search = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v),
})

const filteredIssues = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return props.issues
  return props.issues.filter(
    i =>
      i.title.toLowerCase().includes(q) ||
      String(i.number).includes(q) ||
      (i.projectName?.toLowerCase().includes(q) ?? false),
  )
})

const currentProjectResults = computed(() =>
  filteredIssues.value.filter(i => i.projectId === props.currentProjectId),
)

const otherProjectResults = computed(() =>
  filteredIssues.value.filter(i => i.projectId !== props.currentProjectId),
)
</script>
