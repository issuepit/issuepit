<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @mousedown.self="$emit('close')">
    <div class="bg-gray-900 border border-gray-800 rounded-xl w-full max-w-lg mx-4 shadow-xl">
      <!-- Header -->
      <div class="flex items-center justify-between px-5 py-4 border-b border-gray-800">
        <h2 class="text-base font-semibold text-white">Trigger CI/CD Run</h2>
        <button @click="$emit('close')" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <!-- Body -->
      <div class="px-5 py-4 space-y-4">
        <!-- Commit SHA (shown as info when provided, editable when empty) -->
        <div v-if="commitSha">
          <div class="flex items-center gap-2 text-sm">
            <span class="text-gray-400">Commit:</span>
            <code class="text-gray-200 font-mono bg-gray-800 px-2 py-0.5 rounded">{{ commitSha.slice(0, 7) }}</code>
            <span v-if="branch" class="text-gray-500 text-xs">on {{ branch }}</span>
          </div>
        </div>
        <div v-else>
          <!-- Mode toggle: Branch vs SHA -->
          <div class="flex gap-1 mb-3 bg-gray-800 rounded-lg p-1 w-fit">
            <button
              v-for="mode in refModes"
              :key="mode.value"
              @click="refMode = mode.value"
              :class="[
                'px-3 py-1 text-xs rounded-md transition-colors font-medium',
                refMode === mode.value
                  ? 'bg-brand-600 text-white'
                  : 'text-gray-400 hover:text-gray-200',
              ]">
              {{ mode.label }}
            </button>
          </div>

          <!-- Branch input -->
          <div v-if="refMode === 'branch'">
            <label class="block text-sm font-medium text-gray-300 mb-1">
              Branch <span class="text-red-400">*</span>
            </label>
            <input v-model="manualBranch" type="text" placeholder="e.g. main, feature/my-branch"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white font-mono placeholder-gray-600 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>

          <!-- SHA input -->
          <div v-else>
            <label class="block text-sm font-medium text-gray-300 mb-1">
              Commit SHA <span class="text-red-400">*</span>
            </label>
            <input v-model="manualCommitSha" type="text" placeholder="e.g. abc123def456…"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white font-mono placeholder-gray-600 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
        </div>

        <!-- Event type selector -->
        <div>
          <label class="block text-sm font-medium text-gray-300 mb-1">Event Type</label>
          <select v-model="selectedEvent"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500">
            <option v-for="ev in eventOptions" :key="ev.value" :value="ev.value">{{ ev.label }}</option>
          </select>
        </div>

        <!-- Workflow selector -->
        <div>
          <label class="block text-sm font-medium text-gray-300 mb-1">
            Workflow <span class="text-gray-500">(optional — leave empty to run all)</span>
          </label>
          <select v-model="selectedWorkflow"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500">
            <option value="">All workflows</option>
            <option v-for="wf in filteredWorkflows" :key="wf.fileName" :value="wf.fileName">
              {{ wf.fileName }}
              <template v-if="wf.triggers.length">({{ wf.triggers.join(', ') }})</template>
            </option>
          </select>
          <p v-if="loadingWorkflows" class="text-xs text-gray-500 mt-1">Loading workflows…</p>
        </div>

        <!-- workflow_dispatch inputs -->
        <div v-if="selectedEvent === 'workflow_dispatch' && dispatchInputs.length > 0" class="space-y-3">
          <p class="text-xs font-medium text-gray-400 uppercase tracking-wide">Workflow Inputs</p>
          <div v-for="input in dispatchInputs" :key="input.name">
            <label class="block text-sm font-medium text-gray-300 mb-1">
              {{ input.name }}
              <span v-if="input.required" class="text-red-400 ml-0.5">*</span>
              <span v-if="input.description" class="text-gray-500 font-normal ml-1">— {{ input.description }}</span>
            </label>
            <!-- choice -->
            <select v-if="input.type === 'choice' && input.options"
              v-model="inputValues[input.name]"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option v-for="opt in input.options" :key="opt" :value="opt">{{ opt }}</option>
            </select>
            <!-- boolean -->
            <div v-else-if="input.type === 'boolean'" class="flex items-center gap-2">
              <input type="checkbox" v-model="inputBooleans[input.name]"
                class="w-4 h-4 rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500" />
              <span class="text-sm text-gray-400">{{ inputBooleans[input.name] ? 'true' : 'false' }}</span>
            </div>
            <!-- string / number / environment / default -->
            <input v-else
              v-model="inputValues[input.name]"
              :type="input.type === 'number' ? 'number' : 'text'"
              :placeholder="input.default ?? ''"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-600 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
        </div>

        <p v-if="triggerError" class="text-sm text-red-400">{{ triggerError }}</p>
      </div>

      <!-- Footer -->
      <div class="flex items-center justify-end gap-2 px-5 py-3 border-t border-gray-800">
        <button @click="$emit('close')"
          class="text-sm text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-lg transition-colors">
          Cancel
        </button>
        <button @click="triggerRun" :disabled="triggering"
          class="text-sm bg-brand-600 hover:bg-brand-700 text-white px-4 py-1.5 rounded-lg transition-colors disabled:opacity-50 flex items-center gap-2">
          <svg v-if="triggering" class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          {{ triggering ? 'Triggering…' : 'Trigger Run' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { WorkflowInfo, WorkflowInput } from '~/types'
import { useCiCdRunsStore } from '~/stores/cicdRuns'

const props = defineProps<{
  projectId: string
  /** When provided, the SHA is shown as read-only; when empty the user must type it. */
  commitSha?: string
  branch?: string
}>()

const emit = defineEmits<{
  close: []
  triggered: []
}>()

const ACT_CONTAINER_STORAGE_KEY = 'cicd-act-container-image'

const cicdStore = useCiCdRunsStore()

const eventOptions = [
  { value: 'push', label: 'push' },
  { value: 'pull_request', label: 'pull_request' },
  { value: 'workflow_dispatch', label: 'workflow_dispatch' },
  { value: 'workflow_call', label: 'workflow_call' },
  { value: 'merge_group', label: 'merge_group' },
  { value: 'release', label: 'release' },
]

const refModes = [
  { value: 'branch', label: 'Branch' },
  { value: 'sha', label: 'Commit SHA' },
] as const

type RefMode = typeof refModes[number]['value']

const refMode = ref<RefMode>('branch')
const selectedEvent = ref('push')
const selectedWorkflow = ref('')
const manualBranch = ref('')
const manualCommitSha = ref('')
const inputValues = ref<Record<string, string>>({})
const inputBooleans = ref<Record<string, boolean>>({})
const triggering = ref(false)
const triggerError = ref<string | null>(null)
const loadingWorkflows = ref(false)
const workflows = ref<WorkflowInfo[]>([])

// Workflows that support the currently selected event (or all if none match)
const filteredWorkflows = computed(() => {
  if (!selectedEvent.value) return workflows.value
  const matching = workflows.value.filter(w => w.triggers.includes(selectedEvent.value))
  return matching.length > 0 ? matching : workflows.value
})

// workflow_dispatch inputs for the currently selected workflow
const dispatchInputs = computed<WorkflowInput[]>(() => {
  if (selectedEvent.value !== 'workflow_dispatch') return []
  if (!selectedWorkflow.value) {
    // Show inputs from the first workflow_dispatch-enabled workflow
    const wf = workflows.value.find(w => w.triggers.includes('workflow_dispatch'))
    return wf?.dispatchInputs ?? []
  }
  return workflows.value.find(w => w.fileName === selectedWorkflow.value)?.dispatchInputs ?? []
})

// Reset inputs when workflow or event changes
watch([selectedWorkflow, selectedEvent], () => {
  inputValues.value = {}
  inputBooleans.value = {}
  // Pre-fill defaults
  for (const input of dispatchInputs.value) {
    if (input.type === 'boolean') {
      inputBooleans.value[input.name] = input.default === 'true'
    } else if (input.default) {
      inputValues.value[input.name] = input.default
    }
  }
})

// Load workflows on mount
onMounted(async () => {
  loadingWorkflows.value = true
  workflows.value = await cicdStore.fetchWorkflows(props.projectId)
  loadingWorkflows.value = false
})

async function triggerRun() {
  triggerError.value = null

  // When commitSha prop is provided (e.g. triggered from the code view), use it directly.
  // Otherwise use the user's manual input based on the selected mode.
  const sha = props.commitSha || (refMode.value === 'sha' ? manualCommitSha.value.trim() : undefined)
  const branchName = props.branch || (refMode.value === 'branch' ? manualBranch.value.trim() : undefined)

  if (!sha && !branchName) {
    triggerError.value = refMode.value === 'branch' ? 'Branch name is required' : 'Commit SHA is required'
    return
  }

  triggering.value = true
  try {
    // Build inputs dict (only for workflow_dispatch)
    const inputs: Record<string, string> | undefined =
      selectedEvent.value === 'workflow_dispatch' && dispatchInputs.value.length > 0
        ? Object.fromEntries([
            ...Object.entries(inputValues.value),
            ...Object.entries(inputBooleans.value).map(([k, v]) => [k, v ? 'true' : 'false']),
          ])
        : undefined

    await cicdStore.triggerRun({
      projectId: props.projectId,
      commitSha: sha,
      eventName: selectedEvent.value,
      branch: branchName,
      workflow: selectedWorkflow.value || undefined,
      inputs,
      customImage: import.meta.client ? (localStorage.getItem(ACT_CONTAINER_STORAGE_KEY) ?? undefined) : undefined,
    })
    emit('triggered')
  } catch (e: unknown) {
    triggerError.value = e instanceof Error ? e.message : 'Failed to trigger run'
  } finally {
    triggering.value = false
  }
}
</script>
