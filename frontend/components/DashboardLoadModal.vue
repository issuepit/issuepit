<template>
  <div class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50" @click.self="$emit('close')">
    <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl flex flex-col max-h-[80vh]">
      <h2 class="text-lg font-bold text-white mb-4">Load Dashboard Layout</h2>

      <!-- Import from file -->
      <div class="mb-4">
        <p class="text-sm text-gray-400 mb-2">Import from JSON file:</p>
        <label class="flex items-center gap-2 cursor-pointer">
          <input type="file" accept=".json,application/json" class="hidden" @change="handleFileImport" ref="fileInput" />
          <button
            type="button"
            @click="(fileInput as HTMLInputElement)?.click()"
            class="flex items-center gap-2 text-xs bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
            </svg>
            Choose JSON file
          </button>
          <span v-if="importFileName" class="text-xs text-gray-400">{{ importFileName }}</span>
        </label>
        <p v-if="importError" class="mt-1.5 text-xs text-red-400">{{ importError }}</p>
      </div>

      <div class="border-t border-gray-800 my-2" />

      <!-- Saved templates -->
      <p class="text-sm text-gray-400 mb-2">Saved templates:</p>

      <div v-if="loading" class="flex justify-center py-6">
        <div class="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
      </div>

      <div v-else-if="templates.length === 0" class="text-sm text-gray-500 py-4 text-center">
        No saved templates yet. Save your current layout first.
      </div>

      <div v-else class="overflow-y-auto flex-1 space-y-2 pr-1">
        <div
          v-for="t in templates"
          :key="t.id"
          class="flex items-center gap-3 bg-gray-800 hover:bg-gray-750 border border-gray-700 rounded-lg px-4 py-3 group">
          <div class="flex-1 min-w-0">
            <p class="text-sm font-medium text-white truncate">{{ t.name }}</p>
            <p class="text-xs text-gray-500 mt-0.5">
              <span :class="scopeClass(t.scope)" class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-xs font-medium">
                {{ scopeLabel(t.scope) }}
              </span>
              <span class="ml-2 text-gray-600"><DateDisplay :date="t.updatedAt" mode="auto" resolution="date" /></span>
            </p>
          </div>
          <div class="flex items-center gap-1 shrink-0">
            <button
              @click="handleApply(t)"
              class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1.5 rounded-lg transition-colors">
              Apply
            </button>
            <button
              @click="handleDelete(t)"
              class="text-xs text-gray-600 hover:text-red-400 px-2 py-1.5 rounded-lg transition-colors">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
            </button>
          </div>
        </div>
      </div>

      <div class="flex justify-end mt-4">
        <button @click="$emit('close')"
          class="bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium px-4 py-2 rounded-lg transition-colors">
          Close
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { DashboardLayoutTemplate } from '~/composables/useDashboardTemplates'

const props = defineProps<{
  dashboardType: string
  projectId?: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'apply', layoutJson: string): void
}>()

const { templates, loading, fetchTemplates, deleteTemplate } = useDashboardTemplates()

const fileInput = ref<HTMLInputElement | null>(null)
const importFileName = ref<string | null>(null)
const importError = ref<string | null>(null)

onMounted(() => {
  fetchTemplates(props.dashboardType, props.projectId)
})

function handleApply(t: DashboardLayoutTemplate) {
  emit('apply', t.layoutJson)
  emit('close')
}

async function handleDelete(t: DashboardLayoutTemplate) {
  await deleteTemplate(t.id, props.dashboardType, props.projectId)
}

function handleFileImport(e: Event) {
  importError.value = null
  importFileName.value = null
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  importFileName.value = file.name
  const reader = new FileReader()
  reader.onload = (ev) => {
    const text = ev.target?.result as string
    if (!text) {
      importError.value = 'Could not read file'
      return
    }
    emit('apply', text)
    emit('close')
  }
  reader.onerror = () => { importError.value = 'Error reading file' }
  reader.readAsText(file)
}

function scopeLabel(scope: string) {
  if (scope === 'user') return 'Personal'
  if (scope === 'project_default') return 'Project default'
  return 'Shared'
}

function scopeClass(scope: string) {
  if (scope === 'user') return 'bg-blue-900/60 text-blue-300'
  if (scope === 'project_default') return 'bg-amber-900/60 text-amber-300'
  return 'bg-green-900/60 text-green-300'
}

</script>
