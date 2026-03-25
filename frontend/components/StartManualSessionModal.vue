<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @mousedown.self="$emit('close')">
    <div class="bg-gray-900 border border-gray-800 rounded-xl w-full max-w-md mx-4 shadow-xl">
      <!-- Header -->
      <div class="flex items-center justify-between px-5 py-4 border-b border-gray-800">
        <h2 class="text-base font-semibold text-white flex items-center gap-2">
          <svg class="w-4 h-4 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
          </svg>
          Start Manual Session
        </h2>
        <button @click="$emit('close')" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <!-- Body -->
      <div class="px-5 py-4 space-y-4">
        <!-- Agent -->
        <div>
          <label class="block text-xs font-medium text-gray-400 mb-1.5">Agent</label>
          <select v-model="selectedAgentId"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-brand-500">
            <option value="">— select a manual-mode agent —</option>
            <option v-for="a in manualAgents" :key="a.id" :value="a.id">{{ a.name }}</option>
          </select>
          <p v-if="manualAgents.length === 0 && !agentsLoading" class="mt-1 text-xs text-yellow-400">
            No manual-mode agents found. Enable "Manual Mode" on an agent first.
          </p>
        </div>

        <!-- Branch -->
        <div>
          <label class="block text-xs font-medium text-gray-400 mb-1.5">Branch <span class="text-gray-600">(optional — uses project default if empty)</span></label>
          <div class="flex gap-2">
            <input v-model="branch" type="text" placeholder="e.g. main or feature/my-branch"
              class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-600 focus:outline-none focus:border-brand-500" />
            <button v-if="branches.length" @click="branchPickerOpen = !branchPickerOpen"
              class="px-2 py-2 bg-gray-800 border border-gray-700 rounded-lg hover:border-gray-500 transition-colors text-gray-400 hover:text-gray-200 text-xs">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
              </svg>
            </button>
          </div>
          <!-- Branch picker dropdown -->
          <div v-if="branchPickerOpen && branches.length" class="mt-1 bg-gray-800 border border-gray-700 rounded-lg max-h-48 overflow-y-auto">
            <button v-for="b in filteredBranches" :key="b.name"
              class="w-full text-left px-3 py-2 text-xs text-gray-300 hover:bg-gray-700 hover:text-white transition-colors font-mono"
              @click="branch = b.name; branchPickerOpen = false">
              {{ b.name }}
            </button>
          </div>
        </div>

        <!-- Description (optional) -->
        <div>
          <label class="block text-xs font-medium text-gray-400 mb-1.5">Description <span class="text-gray-600">(optional)</span></label>
          <input v-model="description" type="text" placeholder="e.g. Debug authentication flow"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-600 focus:outline-none focus:border-brand-500" />
        </div>

        <!-- Error -->
        <div v-if="error" class="rounded-lg bg-red-900/40 border border-red-700/50 px-3 py-2 text-sm text-red-300">
          {{ error }}
        </div>
      </div>

      <!-- Footer -->
      <div class="flex items-center justify-end gap-2 px-5 py-3 border-t border-gray-800">
        <button @click="$emit('close')"
          class="text-sm text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-lg transition-colors">
          Cancel
        </button>
        <button @click="start" :disabled="!selectedAgentId || starting"
          class="text-sm bg-brand-600 hover:bg-brand-700 text-white px-4 py-1.5 rounded-lg transition-colors disabled:opacity-50 flex items-center gap-2">
          <svg v-if="starting" class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          {{ starting ? 'Starting…' : 'Start Session' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { GitBranch } from '~/types'

const props = defineProps<{
  projectId: string
}>()

const emit = defineEmits<{
  close: []
  started: [sessionId: string]
}>()

const api = useApi()
const agentsStore = useAgentsStore()

const selectedAgentId = ref('')
const branch = ref('')
const description = ref('')
const starting = ref(false)
const error = ref<string | null>(null)
const branches = ref<GitBranch[]>([])
const branchPickerOpen = ref(false)
const agentsLoading = ref(false)

const manualAgents = computed(() => agentsStore.agents.filter(a => a.manualMode))

const filteredBranches = computed(() =>
  branch.value
    ? branches.value.filter(b => b.name.toLowerCase().includes(branch.value.toLowerCase()))
    : branches.value,
)

onMounted(async () => {
  agentsLoading.value = true
  await agentsStore.fetchAgents()
  agentsLoading.value = false

  // Pre-select the first manual-mode agent if there's only one
  if (manualAgents.value.length === 1) {
    selectedAgentId.value = manualAgents.value[0].id
  }

  // Load branches for the picker
  try {
    branches.value = await api.get<GitBranch[]>(`/api/projects/${props.projectId}/git/branches`)
  } catch {
    // Non-fatal: branch picker will be hidden
  }
})

async function start() {
  if (!selectedAgentId.value) return
  starting.value = true
  error.value = null
  try {
    const result = await api.post<{ sessionId: string }>('/api/agent-sessions/start-manual', {
      agentId: selectedAgentId.value,
      projectId: props.projectId,
      branch: branch.value || null,
      description: description.value || null,
    })
    emit('started', result.sessionId)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to start session'
  } finally {
    starting.value = false
  }
}
</script>
